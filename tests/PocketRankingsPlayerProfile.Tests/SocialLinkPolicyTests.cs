using PocketRankingsPlayerProfile.Models;
using PocketRankingsPlayerProfile.Services;

namespace PocketRankingsPlayerProfile.Tests;

public sealed class SocialLinkPolicyTests
{
    [Theory]
    [InlineData("http://x.com/player")]
    [InlineData("https://x.com.evil.example/player")]
    [InlineData("https://x.com/")]
    public void Normalize_RejectsUnsafeXAddresses(string candidate) => Assert.Throws<ArgumentException>(() => SocialLinkPolicy.Normalize(SocialProvider.X, candidate));

    [Theory]
    [InlineData("https://facebook.com.example.test/player")]
    [InlineData("javascript:alert(1)")]
    public void Normalize_RejectsUnsafeFacebookAddresses(string candidate) => Assert.Throws<ArgumentException>(() => SocialLinkPolicy.Normalize(SocialProvider.Facebook, candidate));

    // Removes query tracking and fragments before a public URL is persisted or embedded.
    [Fact]
    public void Normalize_StripsQueryAndFragment() => Assert.Equal("https://x.com/Example", SocialLinkPolicy.Normalize(SocialProvider.X, "https://X.com/Example?utm_source=test#posts"));
}
