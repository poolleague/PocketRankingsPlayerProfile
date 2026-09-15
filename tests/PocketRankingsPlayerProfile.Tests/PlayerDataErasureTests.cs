using PocketRankingsPlayerProfile.Models;
using PocketRankingsPlayerProfile.Services;

namespace PocketRankingsPlayerProfile.Tests;

public sealed class PlayerDataErasureTests
{
    [Fact]
    public async Task ErasureDeletesProfileAndSuppressesFutureProjection()
    {
        var store = new DevelopmentPlayerProfileStore();
        var profile = (await store.ListAllAsync(default)).Single(item => item.PersonId is not null);
        var requestId = Guid.NewGuid();

        var erased = await store.ErasePlayerDataAsync(new(requestId, profile.PersonId!.Value, Guid.NewGuid(), DateTimeOffset.UtcNow), default);
        var projection = new ProfileProjectionEvent(Guid.NewGuid(), "player.result", 1, SourceProduct.Tournament, "future", "entrant-1", profile.PersonId, DateTimeOffset.UtcNow, "{}");

        Assert.True(erased.Completed);
        Assert.Equal(1, erased.ProfilesDeleted);
        Assert.Null(await store.FindByIdAsync(profile.Id, default));
        Assert.Equal("suppressed_privacy", (await store.ApplyProjectionAsync(projection, default)).Outcome);
    }

    [Fact]
    public async Task RepeatedRequestIsIdempotent()
    {
        var store = new DevelopmentPlayerProfileStore();
        var profile = (await store.ListAllAsync(default)).Single(item => item.PersonId is not null);
        var directive = new PlayerDataErasureDirective(Guid.NewGuid(), profile.PersonId!.Value, Guid.NewGuid(), DateTimeOffset.UtcNow);
        await store.ErasePlayerDataAsync(directive, default);

        var repeated = await store.ErasePlayerDataAsync(directive, default);

        Assert.True(repeated.Duplicate);
    }
}
