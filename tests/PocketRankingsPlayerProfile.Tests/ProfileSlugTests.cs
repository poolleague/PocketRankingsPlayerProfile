using PocketRankingsPlayerProfile.Services;

namespace PocketRankingsPlayerProfile.Tests;

public sealed class ProfileSlugTests
{
    [Theory]
    [InlineData("Avery Brooks", "avery-brooks")]
    [InlineData("  Local 8 Ball Hero  ", "local-8-ball-hero")]
    [InlineData("Renée O'Neil", "renee-oneil")]
    public void Create_ProducesStableRouteToken(string name, string expected) => Assert.Equal(expected, ProfileSlug.FromDisplayName(name));
}
