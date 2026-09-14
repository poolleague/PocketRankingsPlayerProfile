using PocketRankingsPlayerProfile.Models;

namespace PocketRankingsPlayerProfile.Services;

// Derives public totals exclusively from active source results so reported accolades never inflate statistics.
public static class ProfileStatisticsCalculator
{
    // Recalculates rather than retaining mutable totals, which keeps correction and replay behavior deterministic.
    public static ProfileStatistics Calculate(IEnumerable<PlayerResult> results)
    {
        var active = results.Where(result => result.RetractedAt is null).ToArray();
        var wins = active.Sum(result => result.MatchesWon);
        var losses = active.Sum(result => result.MatchesLost);
        var played = wins + losses;
        return new ProfileStatistics(
            active.Length,
            active.Count(result => result.Placement == 1),
            active.Count(result => result.Placement is >= 1 and <= 3),
            wins,
            losses,
            played == 0 ? 0 : Math.Round(wins * 100m / played, 1));
    }
}
