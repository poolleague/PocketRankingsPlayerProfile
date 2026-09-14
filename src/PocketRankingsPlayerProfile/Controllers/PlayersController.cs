using Microsoft.AspNetCore.Mvc;
using PocketRankingsPlayerProfile.Models;
using PocketRankingsPlayerProfile.Services;

namespace PocketRankingsPlayerProfile.Controllers;

// Provides the intentionally small public surface: a directory and one stable profile URL.
public sealed class PlayersController(IPlayerProfileStore store) : Controller
{
    // Lists published profiles only, keeping drafts and hidden profiles out of public responses.
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new PlayerDirectoryViewModel(await store.ListPublishedAsync(cancellationToken)));

    // Resolves by human-friendly slug while the public UUID remains the durable integration identity.
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var profile = await store.FindPublishedBySlugAsync(slug, cancellationToken);
        return profile is null ? NotFound() : View(new PlayerProfileViewModel(profile, ProfileStatisticsCalculator.Calculate(profile.Results)));
    }
}
