using System;
using TMPro;
using UnityEngine;

public class LLMModelText : MonoBehaviour
{
    TextMeshProUGUI text;
    string          baseText;
    StoryManager    storyManager;

    private void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
        baseText = text.text;

    }

    void Update()
    {
        if (storyManager == null)
        {
            storyManager = FindFirstObjectByType<StoryManager>();
            if (storyManager == null)
            {
                return;
            }
        }

        text.text = string.Format(baseText, storyManager.modelName);
    }
}
