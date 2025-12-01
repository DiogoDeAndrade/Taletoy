// SpriteSheetNameExporter.cs
// Put this file anywhere inside an "Editor" folder.

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SpriteSheetNameExporter
{
    // Context menu validation: only enabled when an asset is selected
    [MenuItem("Assets/Taletoy/Export Sprite Names to Text", true)]
    private static bool ValidateExportSpriteNames()
    {
        return Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;
    }

    [MenuItem("Assets/Taletoy/Export Sprite Names to Text")]
    private static void ExportSpriteNames()
    {
        // Project root (the folder that contains "Assets")
        string projectRoot = Application.dataPath.Substring(
            0, Application.dataPath.Length - "Assets".Length);

        int totalSpriteSheets = 0;
        int totalSprites = 0;

        foreach (string guid in Selection.assetGUIDs)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // Get all sub-assets at this path and filter to Sprites
            var sprites = AssetDatabase
                .LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Sprite>()
                .ToArray();

            if (sprites.Length == 0)
                continue; // Not a multi-sprite texture / no sprites here

            totalSpriteSheets++;
            totalSprites += sprites.Length;

            string[] lines = sprites.Select(s => s.name).ToArray();

            string dir = Path.GetDirectoryName(assetPath);
            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            string txtAssetPath = Path.Combine(dir, baseName + "_SpriteNames.txt")
                                      .Replace("\\", "/");

            // Convert "Assets/..." to absolute path
            string txtFullPath = Path.Combine(projectRoot, txtAssetPath);

            File.WriteAllLines(txtFullPath, lines);

            Debug.Log($"Exported {sprites.Length} sprite names from '{assetPath}' to '{txtAssetPath}'.");
        }

        AssetDatabase.Refresh();

        if (totalSpriteSheets == 0)
        {
            Debug.LogWarning("No sprite sheets with multiple sprites found in the current selection.");
        }
        else
        {
            Debug.Log($"Sprite name export complete. Processed {totalSpriteSheets} sprite sheet(s), " +
                      $"exported {totalSprites} sprite name(s).");
        }
    }
}
