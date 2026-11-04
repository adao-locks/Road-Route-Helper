using System.Globalization;
using System.Text;

namespace Road_Route_Helper.Application.Formatting;

public static class FileNames
{
    private const string Fallback = "viagem";

    /// <summary>Lowercase ASCII letters and digits separated by hyphens, safe for a download's file name.</summary>
    public static string Slug(string name)
    {
        var slug = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(char.ToLowerInvariant(c));
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }
        var result = slug.ToString().TrimEnd('-');
        return result.Length > 0 ? result : Fallback;
    }
}
