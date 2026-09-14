using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PocketRankingsPlayerProfile.Models;
using PocketRankingsPlayerProfile.Services;

namespace PocketRankingsPlayerProfile.Controllers;

// Centralizes profile administration while enforcing that a Player can change only their assigned profile.
[Authorize(Policy = "ProfileManager")]
[Route("manage/profiles")]
public sealed class ManageProfilesController(IPlayerProfileStore store) : Controller
{
    // Gives administrators the full list and a Player only the profile named by their signed claim.
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var all = await store.ListAllAsync(cancellationToken);
        return View(IsAdministrator() ? all : all.Where(profile => CanManage(profile.Id)).ToArray());
    }

    // Creates drafts only for administrators; publication remains an explicit later action with a reason.
    [Authorize(Roles = "Owner,ProfileAdmin")]
    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProfileInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));
        var id = await store.CreateProfileAsync(input, User.Identity?.Name ?? "unknown", cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    // Combines profile data, computed stats, and immutable audit entries for one management page.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage(id)) return Forbid();
        var profile = await store.FindByIdAsync(id, cancellationToken);
        if (profile is null) return NotFound();
        ViewBag.Audit = await store.ListAuditAsync(id, cancellationToken);
        return View(new PlayerProfileViewModel(profile, ProfileStatisticsCalculator.Calculate(profile.Results)));
    }

    // Updates narrative fields but cannot alter verified identities or imported statistics.
    [HttpPost("{id:guid}/profile"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, EditProfileInput input, CancellationToken cancellationToken)
    {
        if (!CanManage(id)) return Forbid();
        if (!ModelState.IsValid) return await Details(id, cancellationToken);
        await store.UpdateProfileAsync(id, input, User.Identity?.Name ?? "unknown", cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    // Adds a visibly player-reported accomplishment that never feeds verified totals.
    [HttpPost("{id:guid}/achievements"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAchievement(Guid id, AddAchievementInput input, CancellationToken cancellationToken)
    {
        if (!CanManage(id)) return Forbid();
        if (!ModelState.IsValid) return await Details(id, cancellationToken);
        await store.AddPlayerReportedAchievementAsync(id, input, User.Identity?.Name ?? "unknown", cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    // Accepts public profile URLs only; normalization rejects lookalike and non-HTTPS hosts.
    [HttpPost("{id:guid}/social"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSocial(Guid id, UpdateSocialLinksInput input, CancellationToken cancellationToken)
    {
        if (!CanManage(id)) return Forbid();
        try { await store.UpdateSocialLinksAsync(id, input, User.Identity?.Name ?? "unknown", cancellationToken); }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); return await Details(id, cancellationToken); }
        return RedirectToAction(nameof(Details), new { id });
    }

    // Reserves publication controls for administrative roles and records the supplied reason.
    [Authorize(Roles = "Owner,ProfileAdmin")]
    [HttpPost("{id:guid}/status"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(Guid id, ProfileStatus status, string reason, CancellationToken cancellationToken)
    {
        await store.SetStatusAsync(id, status, User.Identity?.Name ?? "unknown", reason, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    // Lets a player request a link but never promote it to verified locally.
    [HttpPost("{id:guid}/source-links"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSourceLink(Guid id, SourceProduct product, string tenantKey, string sourceEntityId, string displayLabel, CancellationToken cancellationToken)
    {
        if (!CanManage(id)) return Forbid();
        await store.AddPendingSourceLinkAsync(id, product, tenantKey, sourceEntityId, displayLabel, User.Identity?.Name ?? "unknown", cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    // Treats the profile claim as the only player-to-profile authority; display names are never consulted.
    private bool CanManage(Guid profileId) => IsAdministrator() || string.Equals(User.FindFirst("profile_id")?.Value, profileId.ToString(), StringComparison.OrdinalIgnoreCase);
    private bool IsAdministrator() => User.IsInRole("Owner") || User.IsInRole("ProfileAdmin");
}
