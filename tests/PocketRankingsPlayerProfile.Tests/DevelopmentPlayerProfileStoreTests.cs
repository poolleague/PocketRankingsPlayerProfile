using PocketRankingsPlayerProfile.Models;
using PocketRankingsPlayerProfile.Services;

namespace PocketRankingsPlayerProfile.Tests;

public sealed class DevelopmentPlayerProfileStoreTests
{
    // Protects the public directory boundary from draft fixture regressions.
    [Fact]
    public async Task ListPublished_ExcludesDrafts()
    {
        var store = new DevelopmentPlayerProfileStore();
        Assert.All(await store.ListPublishedAsync(default), profile => Assert.Equal(ProfileStatus.Published, profile.Status));
        Assert.Contains(await store.ListAllAsync(default), profile => profile.Status == ProfileStatus.Draft);
    }

    // Confirms arbitrary form input cannot promote an informal accolade to verified provenance.
    [Fact]
    public async Task AddAchievement_AlwaysUsesPlayerReported()
    {
        var store = new DevelopmentPlayerProfileStore();
        var profile = (await store.ListPublishedAsync(default)).First();
        await store.AddPlayerReportedAchievementAsync(profile.Id, new AddAchievementInput { Title="Room high run", Discipline="Straight pool", SourceLabel="Local room" }, "test-player", default);
        var updated = await store.FindByIdAsync(profile.Id, default);
        Assert.Equal(AchievementSource.PlayerReported, updated!.Achievements.First().Source);
    }

    // A pending claim cannot receive verified source projections.
    [Fact]
    public async Task Projection_DoesNotMatchPendingLink()
    {
        var store = new DevelopmentPlayerProfileStore();
        var profile = (await store.ListPublishedAsync(default)).Last();
        await store.AddPendingSourceLinkAsync(profile.Id, SourceProduct.League, "new-league", "player-9", "New League", "player", default);
        var result = await store.ApplyProjectionAsync(Event("new-league", "player-9"), default);
        Assert.Equal("staged_unmatched", result.Outcome);
    }

    // Producer retries remain harmless because event IDs are the idempotency key.
    [Fact]
    public async Task Projection_DuplicateEvent_IsReported()
    {
        var store = new DevelopmentPlayerProfileStore();
        var projection = Event("unknown", "unknown");
        await store.ApplyProjectionAsync(projection, default);
        Assert.True((await store.ApplyProjectionAsync(projection, default)).Duplicate);
    }

    // Uses fictional transport identifiers with no dependency on another product.
    private static ProfileProjectionEvent Event(string tenant, string source) => new(Guid.NewGuid(), "player.result.upsert", 1, SourceProduct.League, tenant, source, null, DateTimeOffset.UtcNow, "{}");
}
