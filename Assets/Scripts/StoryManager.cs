using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UC;
using UnityEngine;

public class StoryManager : MonoBehaviour
{
    [Header("LLM Parameters")]
    [SerializeField] string modelRelativePath = "";
    // RTX 5070 12GB -> 40–60
    // RTX 2080 6GB -> 20
    // Need to figure some sort of automatic way
    [SerializeField] int    gpuLayers = 40;
    // 2048 - Short stories (with 20 layers = 5.5 Gb)
    // 4096 - Longer stories (with 20 layers = 6.5 Gb)
    // 8192 - Long stories (with 20 layers = 8+ Gb)
    [SerializeField] int    contextSize = 2048;
    [SerializeField, Range(0.1f, 1.0f)] float topP = 0.9f;
    [SerializeField] float temperature = 0.7f;
    [SerializeField] bool enableRepetionPenalty = false;
    [SerializeField, ShowIf(nameof(enableRepetionPenalty))] float repetionPenalty = 1.1f;
    [SerializeField, ShowIf(nameof(enableRepetionPenalty))] int repetitionWindow = 64;

    [Header("UI")]
    [SerializeField] CanvasGroup     storyContainer;
    [SerializeField] TextMeshProUGUI storyText;
    [SerializeField] RectTransform   progressRect;
    [SerializeField] CanvasGroup     buttonsContainer;

    int queryId = -1;

    void Start()
    {
        string modelPath = Path.Combine(Application.streamingAssetsPath, "Models", modelRelativePath);
        var    extension = Path.GetExtension(modelPath);
        if (extension.ToLower() != ".gguf") modelPath += ".gguf";

        var status = StoryLLM.Initialize(modelPath, gpuLayers, contextSize);

        switch (status)
        {
            case StoryLLM.LLMInitStatus.Ok:
                Debug.Log("LLM init OK");
                break;

            case StoryLLM.LLMInitStatus.ModelNotFound:
                Debug.LogError("LLM model file not found: " + modelPath);
                break;
            case StoryLLM.LLMInitStatus.DllFail:
                Debug.LogError("LLM init failed due to a DLL error.");
                break;
            case StoryLLM.LLMInitStatus.Error:
            default:
                Debug.LogError("LLM init failed due to an internal error.");
                break;
        }

        storyContainer.alpha = 0.0f;
        SetText("", 0.0f);

        buttonsContainer.alpha = 0.0f;
        buttonsContainer.interactable = false;
    }

    void Update()
    {
        if (queryId == -1) return;

        var (status, answer, gen, max) = StoryLLM.GetAnswer(queryId);

        if (status == StoryLLM.STATUS_RUNNING)
        {
            SetText(answer, (float)gen / (float)max);
        }
        else if ((status == StoryLLM.STATUS_FINISHED) || (status == StoryLLM.STATUS_INTERRUPTED))
        {
            Debug.Log($"Status = {status}... Complete answer = {answer}");
            SetText(answer, 1.0f);

            buttonsContainer.FadeIn(0.5f);
            buttonsContainer.interactable = true;

            queryId = -1; // finished
        }
        else if (status == StoryLLM.STATUS_ERROR)
        {
            SetText(answer, 1.0f);
            Debug.LogError("LLM task error!");
            queryId = -1;
        }
    }

    void SetText(string text, float progress)
    {
        // Extract story content
        string extracted = ExtractStory(text);

        if (string.IsNullOrEmpty(extracted))
        {
            storyContainer.FadeOut(0.25f);
        }
        else
        {
            storyContainer.FadeIn(0.25f);
            storyText.text = extracted;
            progressRect.localScale = new Vector3(progress, 1, 1);
        }
    }

    string ExtractStory(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        const string startTag = "<story>";
        const string endTag = "</story>";

        int startIndex = text.IndexOf(startTag, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            // Check if there is a terminator only, assume the rest is complete
            int tmpIndex = text.IndexOf(endTag, 0, StringComparison.Ordinal);
            if (tmpIndex >= 0)
            {
                return text.Substring(0, tmpIndex);
            }

            // No <story> tag -> return nothing
            return "";
        }

        // Move to the content after <story>
        startIndex += startTag.Length;

        int endIndex = text.IndexOf(endTag, startIndex, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            // <story> exists but no </story> -> return from <story> to the end, excluding the possibility of "<" just beginning, so we don't even see it.
            endIndex = text.IndexOf("<", startIndex, StringComparison.Ordinal);
            if (endIndex != -1)
                return text.Substring(startIndex, endIndex - startIndex).Trim();
            else
                return text.Substring(startIndex).Trim();
        }

        // Return the inner content of the tags
        return text.Substring(startIndex, endIndex - startIndex).Trim();
    }

    void OnDestroy()
    {
        StoryLLM.Shutdown();
    }

    public void StartStory(List<LifeEvent> events)
    {
        if (queryId != -1) return;

        string systemPrompt = @"
           You are a micro-fiction generator.

           Follow these rules VERY STRICTLY:
            1. The story MUST be between 80 and 200 words.
            2. Do NOT explain anything.
            3. Your ENTIRE output MUST be in this exact format: <story>Your single-sentence horror story here.</story>
        ";

        string storyPrompt = "Write a short story based on these life events:\n";
        foreach (var evt in events)
        {
            storyPrompt += evt.GetString() + "\n";
        }
        storyPrompt += "Feel free to ignore some events, except the death event. Give a name to the main character.";

        string prompt = systemPrompt + storyPrompt;

        Debug.Log($"Prompt=[{prompt}]");

        queryId = StoryLLM.Query(prompt, 512);
        StoryLLM.SetTerminationToken(queryId, "</story>");
        StoryLLM.UseImprovedSampler(queryId, temperature, topP, enableRepetionPenalty, repetionPenalty, repetitionWindow);

        StoryLLM.Start(queryId);
    }

    [Button("Start generation")]
    void StartStory()
    {
        if (queryId != -1) return;

        string systemPrompt = @"
           You are a horror micro-fiction generator.

           Follow these rules VERY STRICTLY:
            1. The story MUST be between 40 and 80 words.
            2. Do NOT explain anything.
            3. Your ENTIRE output MUST be in this exact format: <story>Your single-sentence horror story here.</story>
        ";

        string prompt = systemPrompt + "Write a short horror story.";

        Debug.Log($"Prompt=[{prompt}]");

        queryId = StoryLLM.Query(prompt, 512);
        StoryLLM.SetTerminationToken(queryId, "</story>");
        StoryLLM.UseImprovedSampler(queryId, temperature, topP, enableRepetionPenalty, repetionPenalty, repetitionWindow);

        StoryLLM.Start(queryId);        
    }

    [Button("Stop generating")]
    void Stop()
    {
        if (queryId == -1) return;

        StoryLLM.Stop(queryId);
    }
}
