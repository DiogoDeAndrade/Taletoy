using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UC; // Hypertag

[ScriptedImporter(1, "taletoyactions")]
public class TaleToyActionsImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        string[] lines = File.ReadAllLines(ctx.assetPath);

        // Create a root dummy ScriptableObject as the main asset
        var root = ScriptableObject.CreateInstance<TaleToyAssetRoot>();
        root.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
        ctx.AddObjectToAsset("Root", root);
        ctx.SetMainObject(root);

        int index = 0;

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var line = raw.Trim();

            // Ignore comment lines starting with '#'
            if (line.StartsWith("#"))
                continue;

            // Only parse lines starting with '*'
            if (!line.StartsWith("*"))
                continue;

            // Remove '*'
            line = line.Substring(1).Trim();

            // Split at the first ':'
            int colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
            {
                Debug.LogWarning($"[TaleToyActionsImporter] Malformed line (no ':'): \"{raw}\" in {ctx.assetPath}");
                continue;
            }

            string id = line.Substring(0, colonIndex).Trim();
            string display = line.Substring(colonIndex + 1).Trim();

            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[TaleToyActionsImporter] Empty id in line: \"{raw}\" in {ctx.assetPath}");
                continue;
            }

            var tag = ScriptableObject.CreateInstance<Hypertag>();

            string assetName = SanitizeName(id);
            tag.name = assetName;
            tag.displayName = display;

            // Add every Hypertag as a sub-asset under the root
            ctx.AddObjectToAsset(assetName + "_" + index, tag);

            index++;
        }

        if (index == 0)
        {
            Debug.LogWarning($"[TaleToyActionsImporter] No Hypertags imported from {ctx.assetPath}");
        }
    }

    private static string SanitizeName(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "Hypertag";

        foreach (char c in Path.GetInvalidFileNameChars())
            id = id.Replace(c, '_');

        return id.Replace(' ', '_');
    }
}
