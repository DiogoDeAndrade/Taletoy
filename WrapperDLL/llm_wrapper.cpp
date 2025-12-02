#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <chrono>
#include <unordered_map>
#include <filesystem>
#include "llama.h"

extern "C" {

enum LLMInitStatus { LLM_INIT_OK = 0, LLM_INIT_ERROR = 1, LLM_INIT_MODEL_NOT_FOUND = 2 };

enum LLMTaskStatus { TASK_QUEUED = 0, TASK_RUNNING = 1, TASK_FINISHED = 2, TASK_ERROR = 3, TASK_INVALID_ID = 4 };

struct LLMTask {
    int           id;
    std::string   prompt;
    std::string   result;
    LLMTaskStatus status;
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
        sprintf_s((char *)&buffer, "ERROR: Failed to load file '%s'!", model_path);
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

    try {
        std::this_thread::sleep_for(std::chrono::seconds(5));

        // TODO: Replace with real llama.cpp generation
        task->result = "Generated text for prompt:\n\n" + task->prompt;

        task->status = TASK_FINISHED;
    } catch (...) {
        task->status = TASK_ERROR;
    }
}

__declspec(dllexport) int llm_query(const char * prompt) {
    if (!prompt) {
        return -1;
    }

    std::lock_guard<std::mutex> lock(g_taskMutex);

    int id = g_nextId++;

    auto task    = std::make_unique<LLMTask>();
    task->id     = id;
    task->prompt = prompt;
    task->status = TASK_QUEUED;

    LLMTask * raw = task.get();

    // Launch background thread
    raw->worker = std::thread([raw]() { run_task(raw); });
    raw->worker.detach();

    g_tasks[id] = std::move(task);

    return id;
}

__declspec(dllexport) int llm_get_answer(int query_id, char * buffer, int buffer_size) {
    std::lock_guard<std::mutex> lock(g_taskMutex);

    auto it = g_tasks.find(query_id);
    if (it == g_tasks.end()) {
        return TASK_INVALID_ID;
    }

    LLMTask * task = it->second.get();

    if (task->status == TASK_RUNNING || task->status == TASK_QUEUED) {
        return TASK_RUNNING;
    }

    if (task->status == TASK_ERROR) {
        return TASK_ERROR;
    }

    if (task->status == TASK_FINISHED) {
        // Copy result
        int len = (int) task->result.size();
        if (buffer && buffer_size > 0) {
            if (len >= buffer_size) {
                len = buffer_size - 1;
            }
            memcpy(buffer, task->result.c_str(), len);
            buffer[len] = '\0';
        }

        // Remove task from dictionary
        g_tasks.erase(it);

        return TASK_FINISHED;
    }

    return TASK_ERROR;
}

__declspec(dllexport) void llm_shutdown() {
    // NOTE: We cannot safely join detached threads,
    // but better versions can use std::async or a thread pool.
    // For now we just clear tasks (mock only).
    std::lock_guard<std::mutex> lock(g_taskMutex);
    g_tasks.clear();
}

}  // extern "C"
