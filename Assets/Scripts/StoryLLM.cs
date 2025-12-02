using System.Runtime.InteropServices;
using System.Text;

public class StoryLLM
{
    const string DllName = "llm_wrapper";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int llm_init(string modelPath, int gpuLayers, int contextSize);

    [DllImport(DllName)]
    static extern int llm_query(string prompt);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int llm_get_answer(int queryId, StringBuilder buffer, int bufferSize, out int generatedTokens, out int maxTokens);

    public const int STATUS_QUEUED = 0;
    public const int STATUS_RUNNING = 1;
    public const int STATUS_FINISHED = 2;
    public const int STATUS_ERROR = 3;
    public const int STATUS_INVALID = 4;

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

    public static int Query(string prompt)
    {
        return llm_query(prompt);
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

    }
}
