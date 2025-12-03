using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UC;
using UnityEngine;

public class StoryManager : MonoBehaviour
{
    public enum PromptType { Normal, ShortAndDirect };

    [Header("LLM Parameters")]
    [SerializeField] PromptType promptType = PromptType.Normal;
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
    [SerializeField] Hypertag storyContainerTag;
    [SerializeField] Hypertag storyTextTag;
    [SerializeField] Hypertag progressRectTag;
    [SerializeField] Hypertag buttonsGroupTag;

    CanvasGroup     storyContainer => storyContainerTag.FindFirst<CanvasGroup>();
    TextMeshProUGUI storyText => storyTextTag.FindFirst<TextMeshProUGUI>();
    RectTransform   progressRect => progressRectTag.FindFirst<RectTransform>();
    UIGroup         buttonsGroup => buttonsGroupTag.FindFirst<UIGroup>();
    CanvasGroup     buttonsContainer => buttonsGroup?.GetComponent<CanvasGroup>();

    int         queryId = -1;
    string      lastPrompt;
    string      currentModel = "";

    public string modelName => currentModel;

    static StoryManager instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            instance.StartCoroutine(ResetUICR());
            Destroy(gameObject);
            return;
        }

        modelRelativePath = CheckModel(modelRelativePath);
    }

    IEnumerator ResetUICR()
    {
        yield return null;
        ResetUI();
    }

    void Start()
    {       
        ResetLLM(modelRelativePath);

        ResetUI();
    }

    string CheckModel(string model)
    {
        // Check if the default model exists at all
        string modelPath = Path.Combine(Application.streamingAssetsPath, "Models", model);
        var extension = Path.GetExtension(modelPath);
        if (extension.ToLower() != ".gguf") modelPath += ".gguf";

        if (!File.Exists(modelPath))
        {
            // Find the first model on the streaming assets path and use that
            foreach (var file in Directory.EnumerateFiles(Path.Combine(Application.streamingAssetsPath, "Models/"), "*.gguf"))
            {
                return Path.GetFileName(file);
            }

            return "";
        }

        return model;
    }

    public void ResetLLM(string modelName)
    {
        if (currentModel == modelName) return;

        if (currentModel != "")
        {
            StoryLLM.Shutdown();
            currentModel = "";
        }

        modelName = CheckModel(modelName);

        string modelPath = Path.Combine(Application.streamingAssetsPath, "Models", modelName);
        var extension = Path.GetExtension(modelPath);
        if (extension.ToLower() != ".gguf") modelPath += ".gguf";

        var status = StoryLLM.Initialize(modelPath, gpuLayers, contextSize);

        switch (status)
        {
            case StoryLLM.LLMInitStatus.Ok:
                Debug.Log($"LLM initialized with model {modelName}");
                currentModel = modelName;
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

    }

    void ResetUI()
    {
        if (storyContainer)
        {
            storyContainer.alpha = 0.0f;
        }
        SetText("", 0.0f);

        if (buttonsContainer)
        {
            buttonsContainer.alpha = 0.0f;
            buttonsContainer.interactable = false;
        }
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
            SetText(answer, 1.0f, true);

            // If the answer is too short, retry!
            var tmp = ExtractStory(answer);
            if (tmp.Length < 20)
            {
                RetryStory();
            }

            if (buttonsContainer)
            {
                buttonsContainer.FadeIn(0.5f);
                buttonsContainer.interactable = true;
            }
            buttonsGroup.SetEnable(true);

            queryId = -1; // finished
        }
        else if (status == StoryLLM.STATUS_ERROR)
        {
            SetText(answer, 1.0f);
            Debug.LogError($"LLM task error: {answer}!");
            queryId = -1;
        }
    }

    void SetText(string text, float progress, bool complete = false)
    {
        // Extract story content
        string extracted = ExtractStory(text);

        if (string.IsNullOrEmpty(extracted))
        {
            storyContainer?.FadeOut(0.25f);

            if (complete)
            {
                // Story was rubbish, let's just commit to it and just say that
                SetText($"<story>Gibberish answer, unfortunately! A better model would work better!\n{text}</story>", progress, false);
            }
        }
        else
        {
            storyContainer?.FadeIn(0.25f);
            if (storyText) storyText.text = extracted;
            if (progressRect) progressRect.localScale = new Vector3(progress, 1, 1);
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
        if (currentModel != "")
        {
            StoryLLM.Shutdown();
        }
    }

    public void StartStory(List<LifeEvent> events)
    {
        if (queryId != -1) return;

        var genre = PlayerPrefs.GetString("LLMGenre", "Realistic");
        var pov = PlayerPrefs.GetString("LLMPOV", "3rd Person");
        var mood = PlayerPrefs.GetString("LLMMood", "Hopeful");

        switch (promptType)
        {
            case PromptType.Normal:
                lastPrompt = BuildNormalPrompt(genre, pov, mood, events);
                break;
            case PromptType.ShortAndDirect:
                lastPrompt = BuildShortAndDirectPrompt(genre, pov, mood, events);
                break;
            default:
                break;
        }

        Debug.Log($"Prompt=[{lastPrompt}]");

        RetryStory();
    }

    private string BuildNormalPrompt(string genre, string pov, string mood, List<LifeEvent> events)
    {
        string systemPrompt = @"
            You are a micro-fiction generator.

            Follow these rules STRICTLY:
        ";

        systemPrompt += $"1. The final story must be between 80 and 200 words.\n";
        systemPrompt += $"2. Write the story in {pov}\n";
        systemPrompt += $"3. Give a name to the main character.\n";
        systemPrompt += $"4. You may ignore any life events except the death event.\n";
        systemPrompt += $"5. Do NOT explain anything.\n";
        systemPrompt += $"6. There's no need to put the exact age of the character in the story.\n";
        systemPrompt += $"7. Do NOT list life events one after the other.\n";
        systemPrompt += $"8. Blend or reinterpret the events in a natural, narrative way.\n";
        systemPrompt += @"9. Your ENTIRE output must consist of EXACTLY one XML element in this format:
        <story>THE FULL STORY GOES HERE</story>

        No text, no comments, and no blank lines are allowed before or after this element.

        Do NOT begin the story yet. Think silently.
        When ready, output ONLY the <story>…</story> element and nothing else.
        ";

        string storyPrompt = $"Write a {mood} {genre} short story based on these life events:\n";
        foreach (var evt in events)
        {
            storyPrompt += evt.GetString() + "\n";
        }

        string additionalRules = "IMPORTANT: The character MUST die in the story, exactly as described by the last event.\n";

        return systemPrompt + storyPrompt + additionalRules;
    }

    private string BuildShortAndDirectPrompt(string genre, string pov, string mood, List<LifeEvent> events)
    {
        string systemPrompt = @"
            You will write a short story.

            Rules:
            - Length: 80–200 words.
            - Give a name to the main character.
            - Include the death event; other events are optional.
            - NO ages, years, numbers, or chronology.
            - Start story with <story>
            - End with </story>
        ";
        systemPrompt += $"- {pov}\n";

            string storyPrompt = $"Write a {mood} {genre} story inspired by:\n";
        foreach (var evt in events)
        {
            storyPrompt += evt.GetString() + "\n";
        }

        string additionalRules = @"Remember: The character MUST die exactly as described.

        Now begin. Output only:

        <story>";

        return systemPrompt + storyPrompt + additionalRules;
    }

    void RetryStory()
    {
        temperature = PlayerPrefs.GetFloat("LLMTemperature", temperature);

        queryId = StoryLLM.Query(lastPrompt, 512);
        StoryLLM.SetTerminationToken(queryId, "</story>");
        StoryLLM.UseImprovedSampler(queryId, temperature, topP, enableRepetionPenalty, repetionPenalty, repetitionWindow);

        StoryLLM.Start(queryId);
    }

    [Button("Start generation")]
    void StartStory()
    {
        if (queryId != -1) return;

        string prompt = "You are a micro-fiction generator.\r\n\r\n           Follow these rules VERY STRICTLY:\r\n            1. The story MUST be between 80 and 200 words.\r\n            2. Do NOT explain anything.\r\n            3. Your ENTIRE output MUST be in this exact format: <story>Your single-sentence horror story here.</story>\r\n        4. Write the story in 1st Person\r\n.Write a Whimsical Sci-Fi short story based on these life events:\r\n- explored hill castle at 13 years\r\n- cooked fresh egg at 16 years\r\n- observed wild duck at 22 years\r\n- suffered insomnia restless night at 27 years\r\n- hesitated hard choice at 31 years\r\n- died while repaired explorer boots at 34 years\r\nFeel free to ignore some events, except the death event. Give a name to the main character.";

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
