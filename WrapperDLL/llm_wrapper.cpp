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
#include <string>
#include <cstdio>
#include "llama.h"

enum LLMInitStatus { LLM_INIT_OK = 0, LLM_INIT_ERROR = 1, LLM_INIT_MODEL_NOT_FOUND = 2 };

enum LLMTaskStatus { TASK_QUEUED = 0, TASK_RUNNING = 1, TASK_FINISHED = 2, TASK_ERROR = 3, TASK_INVALID_ID = 4 };

struct LLMTask {
    int           id;
    std::string   prompt;
    std::string   result;
    LLMTaskStatus status;
    int           max_tokens       = 0;
    int           generated_tokens = 0;
    std::thread   worker;
};

static std::mutex                                        g_taskMutex;
static std::unordered_map<int, std::unique_ptr<LLMTask>> g_tasks;
static int                                               g_nextId = 1;
static llama_model *                                     g_model  = nullptr;
static llama_context *                                   g_ctx    = nullptr;
static std::mutex                                        g_llmMutex;

static std::mutex g_logMutex;
static bool       g_logInit;

static std::vector<llama_token> tokenize_prompt(llama_model * model, const std::string & text) {
    if (text.empty()) {
        return {};
    }

    // Rough upper bound: length + 8
    int32_t                  n_max_tokens = (int32_t) text.size() + 8;
    std::vector<llama_token> tokens(n_max_tokens);

    const llama_vocab * vocab = llama_model_get_vocab(g_model);

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

extern "C" {

void debug_message(const char* tmp)
{
    std::lock_guard<std::mutex> lock(g_logMutex);

    FILE* file = NULL;

    if (g_logInit)
        fopen_s(&file, "log.txt", "at");
    else {
        fopen_s(&file, "log.txt", "wt");
        g_logInit = true;    
    }

    fprintf(file, "%s\n", tmp);
}

static llama_token sample_token_greedy(llama_context * ctx, llama_model * model) {
    const float * logits  = llama_get_logits(ctx);
    const llama_vocab * vocab  = llama_model_get_vocab(g_model);
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

__declspec(dllexport) int llm_init(const char * model_path, int gpu_layers, int context_size) {
    std::lock_guard<std::mutex> lock(g_llmMutex);

    if (g_ctx) {
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
        sprintf_s((char *)&buffer, 8192, "ERROR: Failed to load file '%s'!", model_path);
        debug_message((char *)&buffer);

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

    // ---------------------------
    // 3. Context parameters
    // ---------------------------
    llama_context_params cparams = llama_context_default_params();
    cparams.n_ctx                = context_size;
    cparams.n_threads            = std::thread::hardware_concurrency();

    g_ctx = llama_init_from_model(g_model, cparams);
    if (!g_ctx) {
        llama_model_free(g_model);
        g_model = nullptr;
        return LLM_INIT_ERROR;
    }

    return LLM_INIT_OK;
}


static void run_task(LLMTask * task) {
    task->status = TASK_RUNNING;

    if (!g_ctx || !g_model) {
        task->result = "[ERROR: model not initialized]";
        task->status = TASK_ERROR;
        return;
    }

    try {
        // Only one thread may touch llama.cpp at a time
        std::lock_guard<std::mutex> lock(g_llmMutex);

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

        if (llama_decode(g_ctx, prompt_batch) != 0) {
            task->result = "[ERROR: llama_decode failed for prompt]";
            task->status = TASK_ERROR;
            return;
        }

        // ----------------------------------
        // 3. Generation loop
        // ----------------------------------
        std::string output;
        const int   max_new_tokens = 512;  // tune this for story length

        for (int i = 0; i < max_new_tokens; ++i) {
            // a) Sample next token (greedy)
            llama_token token = sample_token_greedy(g_ctx, g_model);

            // b) Stop if EOS
            const llama_vocab * vocab = llama_model_get_vocab(g_model);

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

            task->generated_tokens++;

            // d) feed token back in using llama_batch
            llama_batch tok_batch = {};
            tok_batch.n_tokens    = 1;
            tok_batch.token       = &token;
            tok_batch.pos         = nullptr;
            tok_batch.seq_id      = nullptr;
            tok_batch.n_seq_id    = nullptr;
            tok_batch.logits      = nullptr;

            if (llama_decode(g_ctx, tok_batch) != 0) {
                task->result = "[ERROR: llama_decode failed during generation]";
                task->status = TASK_ERROR;
                return;
            }
        }

        task->result = output;
        task->status = TASK_FINISHED;
    } catch (...)
    {
        task->result = "[EXCEPTION: generation crashed]";
        task->status = TASK_ERROR;
    }
}

__declspec(dllexport) int llm_query(const char * prompt) {
    if (!prompt) {
        return -1;
    }

    std::lock_guard<std::mutex> lock(g_taskMutex);

    int id = g_nextId++;

    auto task              = std::make_unique<LLMTask>();
    task->id               = id;
    task->prompt           = prompt;
    task->status           = TASK_QUEUED;
    task->max_tokens       = 512;  // same value you use in run_task
    task->generated_tokens = 0;

    LLMTask * raw = task.get();

    raw->worker = std::thread([raw]() { run_task(raw); });
    raw->worker.detach();

    g_tasks[id] = std::move(task);

    return id;
}


__declspec(dllexport) int llm_get_answer(int    query_id,
                                         char * buffer,
                                         int    buffer_size,
                                         int *  out_generated_tokens,
                                         int *  out_max_tokens) {
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
    if (status == TASK_FINISHED || status == TASK_ERROR) {
        g_tasks.erase(it);
    }

    return (int) status;
}


__declspec(dllexport) void llm_shutdown() {
    // NOTE: We cannot safely join detached threads,
    // but better versions can use std::async or a thread pool.
    // For now we just clear tasks (mock only).
    std::lock_guard<std::mutex> lock(g_taskMutex);
    g_tasks.clear();
}

}  // extern "C"
