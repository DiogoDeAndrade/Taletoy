using NaughtyAttributes;
using System.IO;
using TMPro;
using UnityEngine;
using static StoryLLM;

public class StoryManager : MonoBehaviour
{
    [SerializeField] string modelRelativePath = "";
    // RTX 5070 12GB -> 40–60
    // RTX 2080 6GB -> 20
    // Need to figure some sort of automatic way
    [SerializeField] int    gpuLayers = 40;
    // 2048 - Short stories (with 20 layers = 5.5 Gb)
    // 4096 - Longer stories (with 20 layers = 6.5 Gb)
    // 8192 - Long stories (with 20 layers = 8+ Gb)
    [SerializeField] int    contextSize = 2048;

    [SerializeField] TextMeshProUGUI storyText;
    [SerializeField] RectTransform   progressRect;

    int queryId = -1;

    void Start()
    {
        string modelPath = Path.Combine(Application.streamingAssetsPath, "Models", modelRelativePath);

        var status = StoryLLM.Initialize(modelPath, gpuLayers, contextSize);

        switch (status)
        {
            case LLMInitStatus.Ok:
                Debug.Log("LLM init OK");
                queryId = StoryLLM.Query("Please write a horror story in less than 20 words. Write ONLY the story, no explanations. Wrap the story between <story> and </story> tags.");
                break;

            case LLMInitStatus.ModelNotFound:
                Debug.LogError("LLM model file not found: " + modelPath);
                break;
            case LLMInitStatus.DllFail:
                Debug.LogError("LLM init failed due to a DLL error.");
                break;
            case LLMInitStatus.Error:
            default:
                Debug.LogError("LLM init failed due to an internal error.");
                break;
        }
    }

    void Update()
    {
        if (queryId == -1) return;

        var (status, answer, gen, max) = StoryLLM.GetAnswer(queryId);

        if (status == StoryLLM.STATUS_RUNNING)
        {
            storyText.text = answer;
            progressRect.localScale = new Vector3((float)gen / (float)max, 1, 1);
        }
        else if (status == StoryLLM.STATUS_FINISHED)
        {
            storyText.text = answer;
            queryId = -1; // finished
        }
        else if (status == StoryLLM.STATUS_ERROR)
        {
            Debug.LogError("LLM task error!");
            queryId = -1;
        }
    }

    void OnDestroy()
    {
        StoryLLM.Shutdown();
    }
}
