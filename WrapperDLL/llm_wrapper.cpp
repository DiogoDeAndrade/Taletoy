#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <chrono>
#include <unordered_map>
#include <filesystem>
#include <vector>
#include <algorithm>
#include <random>
#include <cfloat>
#include <cmath>
#include <cstdio>
#include "llama.h"

enum LLMInitStatus { LLM_INIT_OK = 0, LLM_INIT_ERROR = 1, LLM_INIT_MODEL_NOT_FOUND = 2 };

enum LLMTaskStatus {
    TASK_QUEUED     = 0,
    TASK_RUNNING    = 1,
    TASK_FINISHED   = 2,
    TASK_ERROR      = 3,
    TASK_INVALID_ID = 4,
    TASK_INTERRUPT  = 5
};

enum LLMSamplerType { SAMPLER_GREEDY = 0, SAMPLER_TEMP_TOP_P = 1 };

struct LLMTask {
    int             id               = -1;
    std::string     prompt;
    std::string     result;
    LLMTaskStatus   status;
    int             max_tokens       = 512;
    int             generated_tokens = 0;
    std::thread     worker;
    bool            interrupt        = false;
    llama_context * ctx              = nullptr;
    bool            terminator_set   = false;
    std::string     terminator;

    // Sampler config
    LLMSamplerType sampler_type           = SAMPLER_GREEDY;
    float          temperature            = 0.8f;
    float          top_p                  = 0.95f;
    bool           use_repetition_penalty = false;
    float          repetition_penalty     = 1.1f;  // >1.0 = penalize
    int            repetition_window      = 64;    // how many last tokens to look at

    std::vector<llama_token> token_history;                   
    void clear()
    {
        if (ctx)
        {
            llama_free(ctx);
            ctx = nullptr;
        }
        result.clear();
        prompt.clear();
        terminator.clear();
        generated_tokens = 0;
        max_tokens       = 0;
        interrupt        = false;
        terminator_set   = false;
        sampler_type     = SAMPLER_GREEDY;
        temperature      = 0.8f;
        top_p            = 0.95f;
        use_repetition_penalty = false;
        repetition_penalty     = 1.1f;
        repetition_window      = 64;
        token_history.clear();
    }
};

static std::mutex                                        g_taskMutex;
static std::unordered_map<int, std::unique_ptr<LLMTask>> g_tasks;
static int                                               g_nextId = 1;
static llama_model *                                     g_model  = nullptr;
static std::mutex                                        g_llmMutex;
static int                                               g_ContextSize = 2048;

static std::mutex g_logMutex;
static bool       g_logInit;

static std::vector<llama_token> tokenize_prompt(llama_model * model, const std::string & text) {
    if (text.empty()) {
        return {};
    }

    // Rough upper bound: length + 8
    int32_t                  n_max_tokens = (int32_t) text.size() + 8;
    std::vector<llama_token> tokens(n_max_tokens);

    const llama_vocab * vocab = llama_model_get_vocab(model);

    // Newer llama_tokenize has extra params (add_special, parse_special).
    // This matches current signatures; if your version differs slightly,
    // you may need to tweak the last 1–2 bools.
    int32_t n_tokens = llama_tokenize(vocab, text.c_str(), (int32_t) text.size(), tokens.data(), n_max_tokens,
                                      /* add_special */ true,
                                      /* parse_special */ false);

    if (n_tokens < 0) {
        // Error
        return {};
    }

    tokens.resize(n_tokens);
    return tokens;
}

void debug_message(const char * tmp)
{
#ifdef _DEBUG
    std::lock_guard<std::mutex> lock(g_logMutex);

    FILE * file = NULL;

    if (g_logInit) {
        fopen_s(&file, "log.txt", "at");
    } else {
        fopen_s(&file, "log.txt", "wt");
        g_logInit = true;
    }

    if (file == NULL) {
        return;
    }

    fprintf(file, "%s\n", tmp);
    fclose(file);
#endif
}

static llama_token sample_token_greedy(llama_context * ctx, const llama_vocab * vocab, LLMTask * task) {
    const float * logits  = llama_get_logits(ctx);
    const int     n_vocab = llama_vocab_n_tokens(vocab);

    int   best_token = 0;
    float best_logit = logits[0];

    for (int token_id = 1; token_id < n_vocab; ++token_id) {
        float logit = logits[token_id];
        if (logit > best_logit) {
            best_logit = logit;
            best_token = token_id;
        }
    }

    return (llama_token) best_token;
}

static llama_token sample_token_temp_top_p(llama_context * ctx, const llama_vocab * vocab, LLMTask * task)
{
    const float * logits  = llama_get_logits(ctx);
    const int     n_vocab = llama_vocab_n_tokens(vocab);

    // Copy logits so we can modify them safely
    std::vector<float> adjusted_logits(logits, logits + n_vocab);

    // ----------------------------------------------------
    // Repetition penalty: downweight tokens seen recently
    // ----------------------------------------------------
    if (task->use_repetition_penalty && task->repetition_penalty > 1.0f && !task->token_history.empty()) {
        int start_index = (int) task->token_history.size() - task->repetition_window;
        if (start_index < 0) {
            start_index = 0;
        }

        for (int i = start_index; i < (int) task->token_history.size(); ++i) {
            llama_token t = task->token_history[i];
            if (t < 0 || t >= n_vocab) {
                continue;
            }

            float & logit = adjusted_logits[t];
            if (logit > 0.0f) {
                logit /= task->repetition_penalty;
            } else {
                logit *= task->repetition_penalty;
            }
        }
    }

    // ----------------------------------------------------
    // Temperature + softmax over adjusted_logits
    // ----------------------------------------------------
    float max_logit = -FLT_MAX;
    for (int i = 0; i < n_vocab; ++i) {
        float l = adjusted_logits[i] / task->temperature;
        if (l > max_logit) {
            max_logit = l;
        }
    }

    struct Candidate {
        int   token;
        float p;
    };
     
    std::vector<Candidate> candidates;
    candidates.reserve(n_vocab);

    float sum = 0.0f;
    for (int i = 0; i < n_vocab; ++i) {
        float l = adjusted_logits[i] / task->temperature;
        float p = std::exp(l - max_logit);  // stable softmax
        if (p <= 0.0f) {
            continue;
        }
        candidates.push_back({ i, p });
        sum += p;
    }

    if (candidates.empty()) {
        // Fallback to greedy if something went wrong
        return sample_token_greedy(ctx, vocab, task);
    }

    for (auto & c : candidates) {
        c.p /= sum;
    }

    // ----------------------------------------------------
    // Top-p truncation
    // ----------------------------------------------------
    if (task->top_p > 0.0f && task->top_p < 1.0f) {
        std::sort(candidates.begin(), candidates.end(),
                  [](const Candidate & a, const Candidate & b) { return a.p > b.p; });

        float  cum    = 0.0f;
        size_t cutoff = candidates.size();
        for (size_t i = 0; i < candidates.size(); ++i) {
            cum += candidates[i].p;
            if (cum >= task->top_p) {
                cutoff = i + 1;
                break;
            }
        }
        candidates.resize(cutoff);
    }

    // ----------------------------------------------------
    // Random choice from remaining candidates
    // ----------------------------------------------------
    static thread_local std::mt19937      rng(std::random_device{}());
    std::uniform_real_distribution<float> dist(0.0f, 1.0f);

    float r   = dist(rng);
    float cum = 0.0f;
    for (const auto & c : candidates) {
        cum += c.p;
        if (r <= cum) {
            return (llama_token) c.token;
        }
    }

    return (llama_token) candidates.back().token;
}

static void run_task(LLMTask * task) {
    task->status = TASK_RUNNING;

    if (!g_model) {
        task->result = "[ERROR: model not initialized]";
        task->status = TASK_ERROR;
        return;
    }

    llama_context_params cparams = llama_context_default_params();
    cparams.n_ctx                = g_ContextSize;
    cparams.n_threads            = std::thread::hardware_concurrency();

    task->ctx = llama_init_from_model(g_model, cparams);
    if (!task->ctx) {
        task->result = "[ERROR: cant build context]";
        task->status = TASK_ERROR;
        return;
    }

    try {
        const llama_vocab * vocab = llama_model_get_vocab(g_model);

        // ----------------------------------
        // 1. Tokenize prompt
        // ----------------------------------
        std::vector<llama_token> prompt_tokens = tokenize_prompt(g_model, task->prompt);
        if (prompt_tokens.empty()) {
            task->result = "[ERROR: failed to tokenize prompt]";
            task->status = TASK_ERROR;
            return;
        }

        // ----------------------------------
        // 2. Build batch for prompt
        // ----------------------------------
        llama_batch prompt_batch = {};
        prompt_batch.n_tokens    = (int32_t) prompt_tokens.size();
        prompt_batch.token       = prompt_tokens.data();
        prompt_batch.pos         = nullptr;  // auto sequential
        prompt_batch.seq_id      = nullptr;
        prompt_batch.n_seq_id    = nullptr;
        prompt_batch.logits      = nullptr;

        if (llama_decode(task->ctx, prompt_batch) != 0) {
            task->result = "[ERROR: llama_decode failed for prompt]";
            task->status = TASK_ERROR;
            return;
        }

        // ----------------------------------
        // 3. Generation loop
        // ----------------------------------
        std::string output;
        const int   max_new_tokens = task->max_tokens;  // tune this for story length

        for (int i = 0; i < max_new_tokens; ++i) {
            // a) Sample next token (greedy)
            llama_token token;
            switch (task->sampler_type)
            {
                case SAMPLER_TEMP_TOP_P:
                    token = sample_token_temp_top_p(task->ctx, vocab, task);
                    break;
                case SAMPLER_GREEDY:
                default:
                    token = sample_token_greedy(task->ctx, vocab, task);
                    break;
            }

            // b) Stop if EOS
            llama_token eos = llama_vocab_eos(vocab);
            if (token == eos) {
                break;
            }

            // c) Convert token to text and append
            char    buf[512];  // plenty for a single token piece
            int32_t len = llama_token_to_piece(
                vocab, token, buf, (int32_t) sizeof(buf),
                /* lstrip */ 0,
                /* special */ true  // or false, depending on whether you want special tokens rendered
            );

            if (len > 0) {
                output.append(buf, len);

                // copy partial output into task->result in a threadsafe way
                {
                    std::lock_guard<std::mutex> lock(g_taskMutex);
                    task->result = output;
                }
            }

            if ((task->terminator_set) && (!task->terminator.empty()))
            {
                // Check if the terminator sequence exists in the output so far
                if (output.size() >= task->terminator.size())
                {
                    if (output.find(task->terminator) != std::string::npos)
                    {
                        // Found the terminator -> finalize output and stop right away
                        {
                            std::lock_guard<std::mutex> lock(g_taskMutex);
                            task->status = TASK_FINISHED;
                            task->result = output;  // ensure final result is saved
                        }

                        return;         // stop generation immediately
                    }
                }
            }

            task->generated_tokens++;

            // Track history for repetition penalty
            task->token_history.push_back(token);
            // Optional: bound history length to avoid unbounded growth
            if ((int) task->token_history.size() > 1024) {
                task->token_history.erase(task->token_history.begin(),
                                          task->token_history.begin() + (task->token_history.size() - 1024));
            }

            // d) feed token back in using llama_batch
            llama_batch tok_batch = {};
            tok_batch.n_tokens    = 1;
            tok_batch.token       = &token;
            tok_batch.pos         = nullptr;
            tok_batch.seq_id      = nullptr;
            tok_batch.n_seq_id    = nullptr;
            tok_batch.logits      = nullptr;

            if (llama_decode(task->ctx, tok_batch) != 0) {
                task->result = "[ERROR: llama_decode failed during generation]";
                task->status = TASK_ERROR;
                return;
            }

            {
                std::lock_guard<std::mutex> lock(g_taskMutex);

                if (task->interrupt) {
                    task->result = output;
                    task->status = TASK_INTERRUPT;
                    return;
                }
            }
        }

        task->result = output;
        task->status = TASK_FINISHED;
    } catch (...) {
        task->result = "[EXCEPTION: generation crashed]";
        task->status = TASK_ERROR;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// C API
extern "C" {

__declspec(dllexport) int llm_init(const char * model_path, int gpu_layers, int context_size) {
    std::lock_guard<std::mutex> lock(g_llmMutex);

    if (g_model) {
        // Already initialized, treat as success
        return LLM_INIT_OK;
    }

    if (model_path == nullptr || model_path[0] == '\0') {
        return LLM_INIT_ERROR;
    }

    llama_backend_init();

    // ---------------------------
    // 1. Check if file exists
    // ---------------------------
    if (!std::filesystem::exists(model_path))
    {
        char buffer[8192];
        sprintf_s(buffer, 8192, "ERROR: Failed to load file '%s'!", model_path);
        debug_message(buffer);

        return LLM_INIT_MODEL_NOT_FOUND;
    }

    // ---------------------------
    // 2. Model parameters
    // ---------------------------
    llama_model_params mparams = llama_model_default_params();
    mparams.n_gpu_layers       = gpu_layers;

    g_model = llama_model_load_from_file(model_path, mparams);
    if (!g_model) {
        return LLM_INIT_ERROR;
    }

    g_ContextSize = context_size;

    return LLM_INIT_OK;
}

__declspec(dllexport) int llm_query(const char * prompt, int maxTokens) {
    if (!prompt) {
        return -1;
    }

    std::lock_guard<std::mutex> lock(g_taskMutex);

    int id = g_nextId++;

    auto task              = std::make_unique<LLMTask>();
    task->id               = id;
    task->prompt           = prompt;
    task->status           = TASK_QUEUED;
    task->max_tokens       = maxTokens;  // same value you use in run_task
    task->generated_tokens = 0;

    LLMTask * raw = task.get();

    g_tasks[id] = std::move(task);

    return id;   
}

__declspec(dllexport) int llm_set_termination_token(int query_id, const char *terminator)
{
    std::lock_guard<std::mutex> lock(g_taskMutex);

    auto it = g_tasks.find(query_id);
    if (it == g_tasks.end()) {
        return TASK_INVALID_ID;
    }

    LLMTask * task = it->second.get();
    if (task->status == TASK_QUEUED)
    {
        task->terminator     = terminator;
        task->terminator_set = true;
    }

    return task->status;
}

__declspec(dllexport) int llm_set_sampler_improved(int query_id, float temperature, float top_p, bool enableRepetionPenalty, float repetionPenalty, int repetitionWindow) {
    std::lock_guard<std::mutex> lock(g_taskMutex);

    auto it = g_tasks.find(query_id);
    if (it == g_tasks.end()) {
        return TASK_INVALID_ID;
    }

    LLMTask * task = it->second.get();

    // Clamp a bit to avoid silly values
    if (temperature <= 0.0f) {
        temperature = 0.1f;
    }
    if (top_p <= 0.0f) {
        top_p = 1.0f;
    }
    if (top_p > 1.0f) {
        top_p = 1.0f;
    }

    task->sampler_type = SAMPLER_TEMP_TOP_P;
    task->temperature  = temperature;
    task->top_p        = top_p;
    task->use_repetition_penalty = enableRepetionPenalty;
    task->repetition_penalty     = repetionPenalty;
    task->repetition_window  = repetitionWindow;

    return TASK_QUEUED;  // or some neutral status; mainly you just need "success"
}

__declspec(dllexport) int llm_set_sampler_greedy(int query_id) {
    std::lock_guard<std::mutex> lock(g_taskMutex);

    auto it = g_tasks.find(query_id);
    if (it == g_tasks.end()) {
        return TASK_INVALID_ID;
    }

    LLMTask * task     = it->second.get();
    task->sampler_type = SAMPLER_GREEDY;
    return TASK_QUEUED;
}


__declspec(dllexport) int llm_start(int query_id)
{
    std::lock_guard<std::mutex> lock(g_taskMutex);

    auto it = g_tasks.find(query_id);
    if (it == g_tasks.end())
    {
        return TASK_INVALID_ID;
    }

    LLMTask * task  = it->second.get();
    if (task->status == TASK_QUEUED)
    {
        task->worker = std::thread([task]() { run_task(task); });
        task->worker.detach();
    }

    return TASK_RUNNING;
}

_declspec(dllexport) int llm_stop(int query_id)
{
    std::lock_guard<std::mutex> lock(g_taskMutex);

    auto it = g_tasks.find(query_id);
    if (it == g_tasks.end())
    {
        return TASK_INVALID_ID;
    }

    LLMTask* task  = it->second.get();
    task->interrupt = true;

    return TASK_INTERRUPT;
}

__declspec(dllexport) int llm_get_answer(int    query_id, char * buffer, int    buffer_size, int *  out_generated_tokens, int *  out_max_tokens)
{
    std::lock_guard<std::mutex> lock(g_taskMutex);

    auto it = g_tasks.find(query_id);
    if (it == g_tasks.end()) {
        return TASK_INVALID_ID;
    }

    LLMTask * task = it->second.get();

    // Write current text, even if still running
    if (buffer && buffer_size > 0) {
        int len = (int) task->result.size();
        if (len >= buffer_size) {
            len = buffer_size - 1;
        }
        std::memcpy(buffer, task->result.data(), len);
        buffer[len] = '\0';
    }

    if (out_generated_tokens) {
        *out_generated_tokens = task->generated_tokens;
    }

    if (out_max_tokens) {
        *out_max_tokens = task->max_tokens;
    }

    LLMTaskStatus status = task->status;

    // If finished or errored, remove task after copying
    if ((status == TASK_FINISHED) || (status == TASK_ERROR) || (status == TASK_INTERRUPT))
    {
        task->clear();

        g_tasks.erase(it);
    }

    return (int) status;
}

__declspec(dllexport) void llm_shutdown() {
    {
        std::lock_guard<std::mutex> lock(g_taskMutex);

        for (auto & kv : g_tasks)
        {
            kv.second->clear();
        }
        g_tasks.clear();
    }

    // Free llama resources
    std::lock_guard<std::mutex> lock(g_llmMutex);

    if (g_model) {
        llama_model_free(g_model);  // note: newer API name
        g_model = nullptr;
    }

    llama_backend_free();
}

}  // extern "C"
