using System.Runtime.InteropServices;
using System.Text;

public class StoryLLM
{
    const string DllName = "llm_wrapper";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int llm_init(string modelPath, int gpuLayers, int contextSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void llm_shutdown();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int llm_start(int queryId);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int llm_stop(int queryId);
    
    [DllImport(DllName)]
    static extern int llm_query(string prompt, int maxTokens);

    [DllImport(DllName)]
    static extern int llm_set_termination_token(int query_id, string terminator);

    [DllImport("llm_wrapper", CallingConvention = CallingConvention.Cdecl)]
    private static extern int llm_set_sampler_improved(int query_id, float temperature, float top_p, bool enableRepetionPenalty, float repetionPenalty, int repetitionWindow);

    [DllImport("llm_wrapper", CallingConvention = CallingConvention.Cdecl)]
    private static extern int llm_set_sampler_greedy(int queryId);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int llm_get_answer(int queryId, StringBuilder buffer, int bufferSize, out int generatedTokens, out int maxTokens);

    public const int STATUS_QUEUED = 0;
    public const int STATUS_RUNNING = 1;
    public const int STATUS_FINISHED = 2;
    public const int STATUS_ERROR = 3;
    public const int STATUS_INVALID = 4;
    public const int STATUS_INTERRUPTED = 5;

    public enum LLMInitStatus
    {
        Ok = 0,
        Error = 1,
        ModelNotFound = 2,
        DllFail = 99
    }

    public static LLMInitStatus Initialize(string modelPath, int gpuLayers, int contextSize)
    {
        try
        {
            int result = llm_init(modelPath, gpuLayers, contextSize);
            return (LLMInitStatus)result;
        }
        catch
        {
            return LLMInitStatus.DllFail;
        }
    }
    public static int Query(string prompt, int maxTokens = 512)
    {
        return llm_query(prompt, maxTokens);
    }

    public static int SetTerminationToken(int id, string terminationToken)
    {
        return llm_set_termination_token(id, terminationToken);
    }

    public static void UseImprovedSampler(int queryId, float temperature = 0.7f, float topP = 0.9f, bool enableRepetionPenalty = false, float repetionPenalty = 1.1f, int repetitionWindow = 64)
    {
        llm_set_sampler_improved(queryId, temperature, topP, enableRepetionPenalty, repetionPenalty, repetitionWindow);
    }

    public static void UseGreedySampler(int queryId)
    {
        llm_set_sampler_greedy(queryId);
    }

    public static void Start(int id)
    {
        llm_start(id);
    }

    public static void Stop(int id)
    {
        llm_stop(id);
    }

    public static (int status, string text, int generated, int max) GetAnswer(int id)
    {
        var sb = new StringBuilder(8000);
        int gen, max;
        int status = llm_get_answer(id, sb, sb.Capacity, out gen, out max);
        return (status, sb.ToString(), gen, max);
    }

    public static void Shutdown()
    {
        llm_shutdown();
    }
}
