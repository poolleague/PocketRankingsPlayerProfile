using PocketRankingsPlayerProfile.Models;

namespace PocketRankingsPlayerProfile.Services;

// Accepts only canonical public-provider HTTPS addresses so profile input cannot become an arbitrary embed.
public static class SocialLinkPolicy
{
    private static readonly HashSet<string> FacebookHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "facebook.com", "www.facebook.com", "m.facebook.com"
    };

    private static readonly HashSet<string> XHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "x.com", "www.x.com", "twitter.com", "www.twitter.com"
    };

    // Normalizes only the host and scheme while preserving the provider's public path and casing.
    public static string? Normalize(SocialProvider provider, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        if (!Uri.TryCreate(candidate.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Use a complete https address.", nameof(candidate));
        }

        var allowed = provider == SocialProvider.Facebook ? FacebookHosts : XHosts;
        if (!allowed.Contains(uri.Host) || string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
        {
            throw new ArgumentException($"Use a public {ProviderLabel(provider)} profile address.", nameof(candidate));
        }

        var builder = new UriBuilder(uri) { Host = uri.Host.ToLowerInvariant(), Query = "", Fragment = "" };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    // Keeps provider naming consistent in validation, UI, and audit descriptions.
    public static string ProviderLabel(SocialProvider provider) => provider == SocialProvider.Facebook ? "Facebook" : "X";
}
