using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

public static class Helpers 
{
    public static string ToDisplayName(this string id)
    {
        if (string.IsNullOrEmpty(id))
            return "";

        // Replace underscores with spaces
        string s = id.Replace('_', ' ');

        // Insert spaces before capital letters (but not at the beginning)
        s = Regex.Replace(s, "([a-z])([A-Z])", "$1 $2");

        // Insert spaces between acronym sequences and normal words:
        // "URLValue" -> "URL Value"
        s = Regex.Replace(s, "([A-Z]+)([A-Z][a-z])", "$1 $2");

        // Normalize multiple spaces
        s = Regex.Replace(s, @"\s+", " ").Trim();

        // Title case the whole thing
        TextInfo ti = CultureInfo.InvariantCulture.TextInfo;
        s = ti.ToTitleCase(s.ToLower());

        return s;
    }

    public static string CapitalizeFirstLowerRest(this string txt)
    {
        return txt.Substring(0, 1).ToUpper() + txt.Substring(1).ToLower();
    }
}
