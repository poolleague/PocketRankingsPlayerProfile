using PocketRankingsPlayerProfile.Models;
using PocketRankingsPlayerProfile.Services;

namespace PocketRankingsPlayerProfile.Tests;

public sealed class ProfileStatisticsCalculatorTests
{
    // Confirms retracted results cannot remain in public totals after a source correction.
    [Fact]
    public void Calculate_ExcludesRetractedResults()
    {
        var active = Result(1, 5, 0, null);
        var retracted = Result(2, 2, 2, DateTimeOffset.UtcNow);
        var stats = ProfileStatisticsCalculator.Calculate([active, retracted]);
        Assert.Equal(1, stats.EventsPlayed);
        Assert.Equal(100m, stats.MatchWinPercentage);
    }

    // Keeps a no-match profile at zero instead of emitting an undefined percentage.
    [Fact]
    public void Calculate_NoMatches_ReturnsZeroPercentage() => Assert.Equal(0m, ProfileStatisticsCalculator.Calculate([]).MatchWinPercentage);

    // Builds a compact verified result for calculator-only tests.
    private static PlayerResult Result(int place, int won, int lost, DateTimeOffset? retracted) => new(Guid.NewGuid(), SourceProduct.Tournament, "tenant", Guid.NewGuid().ToString(), "Community Open", "8-ball", DateOnly.FromDateTime(DateTime.UtcNow), place, 16, won, lost, DateTimeOffset.UtcNow, retracted);
}
