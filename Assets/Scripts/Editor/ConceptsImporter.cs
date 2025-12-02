using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NaughtyAttributes;
using UC;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

/// <summary>
/// Importer for *.concepts files.
/// - Main asset: ConceptCollection
/// - Subassets: ConceptDef + all needed Hypertag objects
/// </summary>
[ScriptedImporter(1, "concepts")]
public class ConceptsImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        string text = File.ReadAllText(ctx.assetPath);

        // ----------------------------------------------------------------
        // 1) Create root as main asset
        // ----------------------------------------------------------------
        var root = ScriptableObject.CreateInstance<ConceptCollection>();
        root.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

        ctx.AddObjectToAsset("Root", root);
        ctx.SetMainObject(root);

        var collectionIcons = new List<ConceptDef>();

        // ----------------------------------------------------------------
        // 2) Hypertag cache (per file) -> all tags are subassets of root
        // ----------------------------------------------------------------
        var tagCache = new Dictionary<string, Hypertag>(StringComparer.OrdinalIgnoreCase);

        Hypertag ResolveHypertag(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            name = name.Trim();

            if (tagCache.TryGetValue(name, out var existing))
                return existing;

            var tag = ScriptableObject.CreateInstance<Hypertag>();
            tag.name = name;
            tag.displayName = name;

            // Key just needs to be unique per importer
            string key = $"Tag_{tagCache.Count}_{name}";
            ctx.AddObjectToAsset(key, tag);

            tagCache[name] = tag;
            return tag;
        }

        var parserContext = new TaletoyParserContext(ctx, ResolveHypertag);
        var parser = new TaletoyIconFileParser(parserContext);
        var parsedIcons = parser.Parse(text);

        // ----------------------------------------------------------------
        // 3) Create IconDef subassets
        // ----------------------------------------------------------------
        int index = 0;

        foreach (var data in parsedIcons)
        {
            var iconDef = ScriptableObject.CreateInstance<ConceptDef>();
            iconDef.name = data.Name;
            iconDef.sprite = Globals.GetSpriteByName(data.IconSpriteName);
            if (iconDef.sprite == null && !string.IsNullOrEmpty(data.IconSpriteName))
            {
                parserContext.LogWarning($"Sprite '{data.IconSpriteName}' not found in Globals.");
            }
            iconDef.color = data.Color ?? Color.white;

            // Categories -> Hypertags as subassets
            iconDef.categories = new List<Hypertag>();
            foreach (var catName in data.CategoryNames)
            {
                var tag = ResolveHypertag(catName);
                if (tag != null) iconDef.categories.Add(tag);
            }

            // Death touch -> cause as Hypertag subasset too
            iconDef.deathTouch = !string.IsNullOrEmpty(data.DeathTouchTagName);
            if (iconDef.deathTouch)
            {
                var deathTag = ResolveHypertag(data.DeathTouchTagName);
                if (deathTag != null && !string.IsNullOrEmpty(data.DeathTouchDisplayName))
                {
                    deathTag.displayName = data.DeathTouchDisplayName;
                }

                iconDef.deathTouchType = deathTag;
            }

            // Actions
            iconDef.actions = new List<ConceptAction>();
            foreach (var actData in data.Actions)
            {
                Hypertag actionTag = null;
                if (!string.IsNullOrEmpty(actData.ActionTagName))
                {
                    actionTag = ResolveHypertag(actData.ActionTagName);
                    if (actionTag != null && !string.IsNullOrEmpty(actData.ActionTagDisplayName))
                    {
                        actionTag.displayName = actData.ActionTagDisplayName;
                    }
                }

                var iconAction = new ConceptAction
                {
                    actionTag = actionTag,
                    actionDuration = actData.Duration,
                    dangerMultiplier = actData.DangerMultiplier,
                    deltaDanger = actData.DeltaDanger
                };

                if (actData.Conditions != null && actData.Conditions.Count > 0)
                    iconAction.conditions = actData.Conditions.ToArray();
                else
                    iconAction.conditions = null;

                iconDef.actions.Add(iconAction);
            }

            // Register as subasset
            string key = string.IsNullOrEmpty(data.Name) ? $"Icon_{index}" : data.Name;
            ctx.AddObjectToAsset(key, iconDef);

            // Add to collection
            collectionIcons.Add(iconDef);

            index++;
        }

        // Fill collection array
        root.icons = collectionIcons.ToArray();
    }
}

// ========================================================================
// ======================= Parsing layer ==================================
// ========================================================================

public class TaletoyParserContext
{
    public AssetImportContext ImportContext { get; }
    private readonly Func<string, Hypertag> _tagResolver;

    public TaletoyParserContext(AssetImportContext importContext,
                                Func<string, Hypertag> tagResolver)
    {
        ImportContext = importContext;
        _tagResolver = tagResolver;
    }

    public Hypertag ResolveHypertag(string name) => _tagResolver?.Invoke(name);

    public void LogWarning(string msg)
    {
        if (ImportContext != null)
            ImportContext.LogImportWarning(msg);
        else
            Debug.LogWarning(msg);
    }

    public void LogError(string msg)
    {
        if (ImportContext != null)
            ImportContext.LogImportError(msg);
        else
            Debug.LogError(msg);
    }
}

/// <summary>
/// File parser for the custom .taletoyicons format.
/// Designed to be modular: action argument parsers and condition parsers
/// are pluggable.
/// </summary>
public class TaletoyIconFileParser
{
    // -------------------- Data DTOs (intermediate) ----------------------

    public class ConceptDefData
    {
        public string Name;
        public string IconSpriteName;
        public List<string> CategoryNames = new List<string>();
        public Color? Color;

        // tag name + optional display name from touch_death(...)
        public string DeathTouchTagName;
        public string DeathTouchDisplayName;

        public List<ActionData> Actions = new List<ActionData>();

        public ConceptDefData(string name)
        {
            Name = name;
        }
    }

    public class ActionData
    {
        // tag name + optional display name from action(...)
        public string ActionTagName;
        public string ActionTagDisplayName;

        public Vector2Int Duration = Vector2Int.one;
        public float DangerMultiplier = 1.0f;
        public float DeltaDanger = 0.0f;
        public List<ConceptCondition> Conditions = new List<ConceptCondition>();
    }

    // -------------------- Interfaces for modularity ---------------------

    public interface IActionArgumentParser
    {
        string Keyword { get; }
        void Apply(string argumentList, ActionData action, TaletoyParserContext ctx);
    }

    public interface IConditionParser
    {
        string Keyword { get; }
        ConceptCondition Parse(string argumentList, TaletoyParserContext ctx);
    }

    private readonly TaletoyParserContext _ctx;
    private readonly Dictionary<string, IActionArgumentParser> _actionArgParsers;
    private readonly Dictionary<string, IConditionParser> _conditionParsers;

    public TaletoyIconFileParser(TaletoyParserContext ctx)
    {
        _ctx = ctx;

        _actionArgParsers = new Dictionary<string, IActionArgumentParser>(StringComparer.OrdinalIgnoreCase)
        {
            { "IncreaseDanger", new IncreaseDangerParser() },
            { "DangerMultiplier", new DangerMultiplierParser() }
        };

        _conditionParsers = new Dictionary<string, IConditionParser>(StringComparer.OrdinalIgnoreCase)
        {
            { "RequireAge", new RequireAgeConditionParser() },
            { "RequireCategory", new RequireCategoryConditionParser() }
        };
    }

    public List<ConceptDefData> Parse(string text)
    {
        var result = new List<ConceptDefData>();

        string[] lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        ConceptDefData current = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string rawLine = lines[i];
            string lineNoComments = StripComments(rawLine);
            string trimmed = lineNoComments.TrimEnd();

            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.TrimStart().StartsWith("#"))
                continue;

            // Icon header: *name:
            if (trimmed.StartsWith("*"))
            {
                int colonIdx = trimmed.IndexOf(':');
                if (colonIdx < 0)
                {
                    _ctx.LogWarning($"Line {i + 1}: malformed icon header \"{trimmed}\"");
                    continue;
                }

                string iconName = trimmed.Substring(1, colonIdx - 1).Trim();
                if (string.IsNullOrEmpty(iconName))
                {
                    _ctx.LogWarning($"Line {i + 1}: icon name is empty.");
                    continue;
                }

                current = new ConceptDefData(iconName);
                result.Add(current);
                continue;
            }

            if (current == null)
            {
                _ctx.LogWarning($"Line {i + 1}: content before any icon header, ignoring.");
                continue;
            }

            // Property lines: key(args)
            string t = trimmed.TrimStart();
            int openIdx = t.IndexOf('(');
            int closeIdx = t.LastIndexOf(')');
            if (openIdx < 0 || closeIdx <= openIdx)
            {
                _ctx.LogWarning($"Line {i + 1}: malformed property \"{trimmed}\"");
                continue;
            }

            string keyword = t.Substring(0, openIdx).Trim();
            string argList = t.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim();

            switch (keyword)
            {
                case "icon":
                    current.IconSpriteName = argList.Trim();
                    break;

                case "categories":
                    foreach (var cat in SimpleCsvSplit(argList))
                    {
                        if (!string.IsNullOrWhiteSpace(cat))
                            current.CategoryNames.Add(cat.Trim());
                    }
                    break;

                case "color":
                    if (TryParseColor(argList, out Color col))
                        current.Color = col;
                    else
                        _ctx.LogWarning($"Line {i + 1}: invalid color \"{argList}\".");
                    break;

                case "touch_death":
                    {
                        // Allows syntax: touch_death(Impale[impaled])
                        ParseTagAndDisplay(argList, out current.DeathTouchTagName, out current.DeathTouchDisplayName);
                        break;
                    }

                case "action":
                    var action = ParseAction(argList, i + 1);
                    if (action != null)
                        current.Actions.Add(action);
                    break;

                default:
                    _ctx.LogWarning($"Line {i + 1}: unknown keyword \"{keyword}\".");
                    break;
            }
        }

        return result;
    }

    // --------------------------------------------------------------------
    // ----- Action parsing ------------------------------------------------
    // --------------------------------------------------------------------

    private ActionData ParseAction(string argList, int lineNumber)
    {
        var topLevelParts = SplitTopLevelWithCommas(argList);
        if (topLevelParts.Count < 2)
        {
            _ctx.LogWarning($"Line {lineNumber}: 'action' requires at least action name and duration.");
            return null;
        }

        var actionData = new ActionData
        {
            Duration = ParseDuration(topLevelParts[1].Trim(), lineNumber),
            DangerMultiplier = 1.0f,
            DeltaDanger = 0.0f,
            Conditions = new List<ConceptCondition>()
        };

        // First argument: tag name + optional display name, e.g. Hug[hugged]
        ParseTagAndDisplay(topLevelParts[0], out actionData.ActionTagName, out actionData.ActionTagDisplayName);


        for (int k = 2; k < topLevelParts.Count; k++)
        {
            string token = topLevelParts[k].Trim();
            if (string.IsNullOrEmpty(token)) continue;

            int openIdx = token.IndexOf('(');
            int closeIdx = token.LastIndexOf(')');

            if (openIdx < 0 || closeIdx <= openIdx)
            {
                _ctx.LogWarning($"Line {lineNumber}: malformed action argument \"{token}\"");
                continue;
            }

            string funcName = token.Substring(0, openIdx).Trim();
            string innerArgs = token.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim();

            if (_conditionParsers.TryGetValue(funcName, out var condParser))
            {
                ConceptCondition cond = condParser.Parse(innerArgs, _ctx);
                if (cond != null) actionData.Conditions.Add(cond);
                continue;
            }

            if (_actionArgParsers.TryGetValue(funcName, out var argParser))
            {
                argParser.Apply(innerArgs, actionData, _ctx);
                continue;
            }

            _ctx.LogWarning($"Line {lineNumber}: unknown action argument/condition \"{funcName}\".");
        }

        return actionData;
    }

    private static Vector2Int ParseDuration(string token, int lineNumber)
    {
        token = token.Trim();

        // Range: [a-b]
        if (token.StartsWith("[") && token.EndsWith("]"))
        {
            string inner = token.Substring(1, token.Length - 2).Trim();
            string[] parts = inner.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                int.TryParse(parts[0].Trim(), out int minVal) &&
                int.TryParse(parts[1].Trim(), out int maxVal))
            {
                return new Vector2Int(minVal, maxVal);
            }
        }

        // Single value -> same min and max
        if (int.TryParse(token, out int val))
        {
            return new Vector2Int(val, val);
        }

        Debug.LogWarning($"TaletoyIconsImporter: invalid duration token \"{token}\" at line {lineNumber}, defaulting to 1.");
        return Vector2Int.one;
    }

    // --------------------------------------------------------------------
    // ----- Color parsing -------------------------------------------------
    // --------------------------------------------------------------------

    private static bool TryParseColor(string token, out Color color)
    {
        token = token.Trim();

        if (!token.StartsWith("#"))
        {
            color = Color.white;
            return false;
        }

        return ColorUtility.TryParseHtmlString(token, out color);
    }

    // --------------------------------------------------------------------
    // ----- Helpers -------------------------------------------------------
    // --------------------------------------------------------------------

    private static string StripComments(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;

        int depthPar = 0;
        int depthBr = 0;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '(') depthPar++;
            else if (c == ')') depthPar = Math.Max(0, depthPar - 1);
            else if (c == '[') depthBr++;
            else if (c == ']') depthBr = Math.Max(0, depthBr - 1);
            else if (c == '#' && depthPar == 0 && depthBr == 0)
            {
                return line.Substring(0, i);
            }
        }

        return line;
    }

    private static IEnumerable<string> SimpleCsvSplit(string argList)
    {
        return argList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static List<string> SplitTopLevelWithCommas(string argList)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(argList)) return result;

        int depthPar = 0;
        int depthBr = 0;
        int lastStart = 0;

        for (int i = 0; i < argList.Length; i++)
        {
            char c = argList[i];
            if (c == '(') depthPar++;
            else if (c == ')') depthPar = Math.Max(0, depthPar - 1);
            else if (c == '[') depthBr++;
            else if (c == ']') depthBr = Math.Max(0, depthBr - 1);
            else if (c == ',' && depthPar == 0 && depthBr == 0)
            {
                string part = argList.Substring(lastStart, i - lastStart);
                result.Add(part);
                lastStart = i + 1;
            }
        }

        if (lastStart < argList.Length)
        {
            string part = argList.Substring(lastStart);
            result.Add(part);
        }

        return result;
    }

    // ====================================================================
    // ========== Action argument parsers =================================
    // ====================================================================

    private class IncreaseDangerParser : IActionArgumentParser
    {
        public string Keyword => "IncreaseDanger";

        public void Apply(string argumentList, ActionData action, TaletoyParserContext ctx)
        {
            var parts = SimpleCsvSplit(argumentList).ToArray();
            if (parts.Length < 1)
            {
                ctx.LogWarning("IncreaseDanger() requires a single float parameter.");
                return;
            }

            if (float.TryParse(parts[0].Trim(),
                               System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture,
                               out float val))
            {
                action.DeltaDanger += val;
            }
            else
            {
                ctx.LogWarning($"IncreaseDanger(): cannot parse \"{parts[0]}\" as float.");
            }
        }
    }

    private class DangerMultiplierParser : IActionArgumentParser
    {
        public string Keyword => "DangerMultiplier";

        public void Apply(string argumentList, ActionData action, TaletoyParserContext ctx)
        {
            var parts = SimpleCsvSplit(argumentList).ToArray();
            if (parts.Length < 1)
            {
                ctx.LogWarning("DangerMultiplier() requires a single float parameter.");
                return;
            }

            if (float.TryParse(parts[0].Trim(),
                               System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture,
                               out float val))
            {
                action.DangerMultiplier *= val;
            }
            else
            {
                ctx.LogWarning($"DangerMultiplier(): cannot parse \"{parts[0]}\" as float.");
            }
        }
    }

    // ====================================================================
    // ========== Condition parsers =======================================
    // ====================================================================

    private class RequireAgeConditionParser : IConditionParser
    {
        public string Keyword => "RequireAge";

        public ConceptCondition Parse(string argumentList, TaletoyParserContext ctx)
        {
            var parts = SimpleCsvSplit(argumentList).ToArray();
            if (parts.Length < 1)
            {
                ctx.LogWarning("RequireAge() requires an integer parameter.");
                return null;
            }

            if (!int.TryParse(parts[0].Trim(), out int age))
            {
                ctx.LogWarning($"RequireAge(): cannot parse \"{parts[0]}\" as integer.");
                return null;
            }

            return new ConceptCondition
            {
                type = ConceptCondition.Type.RequireAge,
                age = age
            };
        }
    }

    private class RequireCategoryConditionParser : IConditionParser
    {
        public string Keyword => "RequireCategory";

        public ConceptCondition Parse(string argumentList, TaletoyParserContext ctx)
        {
            var parts = SimpleCsvSplit(argumentList)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (parts.Length == 0)
            {
                ctx.LogWarning("RequireCategory() requires at least one category name.");
                return null;
            }

            var tags = new List<Hypertag>();
            foreach (var name in parts)
            {
                var tag = ctx.ResolveHypertag(name);
                if (tag != null) tags.Add(tag);
            }

            if (tags.Count == 0)
            {
                ctx.LogWarning("RequireCategory(): no valid categories could be resolved to Hypertags.");
                return null;
            }

            return new ConceptCondition
            {
                type = ConceptCondition.Type.RequireCategory,
                categories = tags.ToArray()
            };
        }
    }

    private static void ParseTagAndDisplay(string token, out string tagName, out string displayName)
    {
        tagName = null;
        displayName = null;

        if (string.IsNullOrWhiteSpace(token))
            return;

        token = token.Trim();

        int bracketIdx = token.IndexOf('[');
        if (bracketIdx >= 0 && token.EndsWith("]"))
        {
            tagName = token.Substring(0, bracketIdx).Trim();
            string inner = token.Substring(bracketIdx + 1, token.Length - bracketIdx - 2).Trim();
            displayName = string.IsNullOrEmpty(inner) ? null : inner;
        }
        else
        {
            tagName = token;
        }
    }
}
