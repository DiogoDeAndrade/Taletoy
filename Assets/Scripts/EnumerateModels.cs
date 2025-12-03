using System.Collections.Generic;
using System.IO;
using UC;
using UnityEngine;

public class EnumerateModels : MonoBehaviour
{
    void Awake()
    {
        UIStringSelector selector = GetComponent<UIStringSelector>();

        // Find all models available in StreamingAssets/Models/*.GGUF
        List<string>    models = new();
        string          modelsDir = Path.Combine(Application.streamingAssetsPath, "Models");

        if (Directory.Exists(modelsDir))
        {
            string[] files = Directory.GetFiles(modelsDir, "*.gguf", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                // Use just the filename, not the path
                string fileName = Path.GetFileNameWithoutExtension(file);
                models.Add(fileName);
            }
        }

        if (models.Count == 0)
        {
            // Avoid selector.SetOptions(models, models[0]) crash
            models.Add("No models found");
        }

        selector.SetOptions(models, models[0]);
    }
}
