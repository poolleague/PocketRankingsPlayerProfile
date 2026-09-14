using System.Text;
using System.Globalization;

namespace PocketRankingsPlayerProfile.Services;

// Creates stable, readable public routes without putting private identifiers in URLs.
public static class ProfileSlug
{
    // Limits the route alphabet so slugs remain safe across browsers, proxies, and database collations.
    public static string FromDisplayName(string displayName)
    {
        var normalized = displayName.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingDash = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (character is '\'' or '’') continue;
            if (char.IsLetterOrDigit(character))
            {
                if (pendingDash && builder.Length > 0) builder.Append('-');
                builder.Append(char.ToLowerInvariant(character));
                pendingDash = false;
            }
            else if (builder.Length > 0)
            {
                pendingDash = true;
            }
        }

        var result = builder.ToString();
        return string.IsNullOrWhiteSpace(result) ? "player" : result[..Math.Min(result.Length, 70)];
    }
}
