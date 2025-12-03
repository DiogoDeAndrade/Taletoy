using NaughtyAttributes;
using UC;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGame : MonoBehaviour
{
    [SerializeField, Scene] private string gameScene;

    public void TriggerStartGame()
    {
        var modelName = PlayerPrefs.GetString("LLMModel", "");
        if (modelName != "")
        {
            var sm = FindFirstObjectByType<StoryManager>();
            sm.ResetLLM(modelName);
        }

        FullscreenFader.FadeOut(0.5f, Color.black, () =>
        {
            SceneManager.LoadScene(gameScene);
        });
    }
}
