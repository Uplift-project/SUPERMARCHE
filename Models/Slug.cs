using System.Globalization;
using System.Text;

namespace Models;

public static class Slug
{
    /// <summary>« Fruits & Légumes » → « fruits-legumes ».</summary>
    public static string From(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        var dash = false;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            var c = ch switch { 'œ' => "oe", 'æ' => "ae", _ => ch.ToString() };
            if (c.Length == 1 && !char.IsAsciiLetterOrDigit(c[0]))
            {
                if (!dash && sb.Length > 0) { sb.Append('-'); dash = true; }
                continue;
            }
            sb.Append(c);
            dash = false;
        }
        return sb.ToString().Trim('-');
    }
}
