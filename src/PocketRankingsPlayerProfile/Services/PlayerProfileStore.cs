using PocketRankingsPlayerProfile.Models;

namespace PocketRankingsPlayerProfile.Services;

public interface IPlayerProfileStore
{
    Task<IReadOnlyList<PlayerProfile>> ListPublishedAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PlayerProfile>> ListAllAsync(CancellationToken cancellationToken);
    Task<Guid> CreateProfileAsync(CreateProfileInput input, string actor, CancellationToken cancellationToken);
    Task<PlayerProfile?> FindPublishedBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<PlayerProfile?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateProfileAsync(Guid id, EditProfileInput input, string actor, CancellationToken cancellationToken);
    Task AddPlayerReportedAchievementAsync(Guid id, AddAchievementInput input, string actor, CancellationToken cancellationToken);
    Task UpdateSocialLinksAsync(Guid id, UpdateSocialLinksInput input, string actor, CancellationToken cancellationToken);
    Task SetStatusAsync(Guid id, ProfileStatus status, string actor, string reason, CancellationToken cancellationToken);
    Task AddPendingSourceLinkAsync(Guid id, SourceProduct product, string tenantKey, string sourceEntityId, string displayLabel, string actor, CancellationToken cancellationToken);
    Task<ProjectionApplyResult> ApplyProjectionAsync(ProfileProjectionEvent projectionEvent, CancellationToken cancellationToken);
    Task<PlayerDataErasureResult> ErasePlayerDataAsync(PlayerDataErasureDirective directive, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProfileAuditEntry>> ListAuditAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record ProjectionApplyResult(bool Applied, bool Duplicate, string Outcome, Guid? ProfileId);
public sealed record ProfileAuditEntry(Guid Id, Guid ProfileId, string Action, string Actor, string Reason, DateTimeOffset OccurredAt);

// Supplies deterministic fictional profiles when Development has no PostgreSQL connection.
public sealed class DevelopmentPlayerProfileStore : IPlayerProfileStore
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, PlayerProfile> profiles;
    private readonly List<ProfileAuditEntry> audit = [];
    private readonly HashSet<Guid> processedEvents = [];
    private readonly HashSet<Guid> processedErasureRequests = [];
    private readonly HashSet<string> privacySuppressions = [];
    private readonly PrivacySuppressionHasher suppressionHasher = new("development-only-player-profile-privacy-key");

    public DevelopmentPlayerProfileStore()
    {
        profiles = SeedProfiles().ToDictionary(profile => profile.Id);
    }

    // Returns a stable alphabetical directory without exposing draft or hidden profiles.
    public Task<IReadOnlyList<PlayerProfile>> ListPublishedAsync(CancellationToken cancellationToken)
    {
        lock (sync) return Task.FromResult<IReadOnlyList<PlayerProfile>>(profiles.Values.Where(profile => profile.Status == ProfileStatus.Published).OrderBy(profile => profile.DisplayName).ToArray());
    }

    // Keeps management visibility separate from the public directory filter.
    public Task<IReadOnlyList<PlayerProfile>> ListAllAsync(CancellationToken cancellationToken)
    {
        lock (sync) return Task.FromResult<IReadOnlyList<PlayerProfile>>(profiles.Values.OrderBy(profile => profile.DisplayName).ToArray());
    }

    // Starts an empty draft with two generated identifiers so public identity never depends on its route slug.
    public Task<Guid> CreateProfileAsync(CreateProfileInput input, string actor, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (profiles.Values.Any(profile => string.Equals(profile.Slug, input.Slug, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("That profile URL is already in use.");
            var id = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            profiles[id] = new PlayerProfile(id, Guid.NewGuid(), null, input.Slug, input.DisplayName.Trim(), "", "", "", "", "", "US", input.PrimaryDiscipline.Trim(), ProfileStatus.Draft, now, now, [], [], [], []);
            RecordAudit(id, "profile_created", actor, "Draft profile created");
            return Task.FromResult(id);
        }
    }

    // Uses an ordinal slug match so route behavior is predictable on every supported database.
    public Task<PlayerProfile?> FindPublishedBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        lock (sync) return Task.FromResult(profiles.Values.SingleOrDefault(profile => profile.Status == ProfileStatus.Published && string.Equals(profile.Slug, slug, StringComparison.OrdinalIgnoreCase)));
    }

    // Allows authorized management to retrieve non-public lifecycle states.
    public Task<PlayerProfile?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (sync) return Task.FromResult(profiles.GetValueOrDefault(id));
    }

    // Limits updates to biography fields and never permits this form to claim a PersonId or verified source link.
    public Task UpdateProfileAsync(Guid id, EditProfileInput input, string actor, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            var profile = RequireProfile(id);
            profiles[id] = profile with
            {
                DisplayName = input.DisplayName.Trim(),
                Headline = input.Headline.Trim(),
                Biography = input.Biography.Trim(),
                HomeRoom = input.HomeRoom.Trim(),
                Locality = input.Locality.Trim(),
                Region = input.Region.Trim(),
                CountryCode = input.CountryCode.Trim().ToUpperInvariant(),
                PrimaryDiscipline = input.PrimaryDiscipline.Trim(),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            RecordAudit(id, "profile_updated", actor, "Player-facing profile details updated");
        }
        return Task.CompletedTask;
    }

    // Forces self-entered accomplishments into the PlayerReported class regardless of submitted labels.
    public Task AddPlayerReportedAchievementAsync(Guid id, AddAchievementInput input, string actor, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            var profile = RequireProfile(id);
            var achievement = new PlayerAchievement(Guid.NewGuid(), input.Title.Trim(), input.Description.Trim(), input.Discipline.Trim(), input.AchievedOn, AchievementSource.PlayerReported, string.IsNullOrWhiteSpace(input.SourceLabel) ? "Player reported" : input.SourceLabel.Trim(), input.EvidenceUrl?.Trim(), DateTimeOffset.UtcNow);
            profiles[id] = profile with { Achievements = profile.Achievements.Append(achievement).OrderByDescending(item => item.AchievedOn).ToArray(), UpdatedAt = DateTimeOffset.UtcNow };
            RecordAudit(id, "reported_achievement_added", actor, "Player-reported accomplishment added");
        }
        return Task.CompletedTask;
    }

    // Stores normalized public addresses only; provider credentials never enter this product.
    public Task UpdateSocialLinksAsync(Guid id, UpdateSocialLinksInput input, string actor, CancellationToken cancellationToken)
    {
        var facebook = SocialLinkPolicy.Normalize(SocialProvider.Facebook, input.FacebookUrl);
        var x = SocialLinkPolicy.Normalize(SocialProvider.X, input.XUrl);
        lock (sync)
        {
            var profile = RequireProfile(id);
            var now = DateTimeOffset.UtcNow;
            var links = new List<PlayerSocialLink>();
            if (facebook is not null) links.Add(new PlayerSocialLink(Guid.NewGuid(), SocialProvider.Facebook, facebook, input.FacebookEmbedEnabled, now));
            if (x is not null) links.Add(new PlayerSocialLink(Guid.NewGuid(), SocialProvider.X, x, input.XEmbedEnabled, now));
            profiles[id] = profile with { SocialLinks = links, UpdatedAt = now };
            RecordAudit(id, "social_links_updated", actor, "Public social addresses updated");
        }
        return Task.CompletedTask;
    }

    // Retains lifecycle evidence and requires a reason for every publication decision.
    public Task SetStatusAsync(Guid id, ProfileStatus status, string actor, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        lock (sync)
        {
            var profile = RequireProfile(id);
            profiles[id] = profile with { Status = status, UpdatedAt = DateTimeOffset.UtcNow };
            RecordAudit(id, $"profile_{status.ToString().ToLowerInvariant()}", actor, reason.Trim());
        }
        return Task.CompletedTask;
    }

    // Creates only a pending claim; verification belongs to the future signed Account/source handshake.
    public Task AddPendingSourceLinkAsync(Guid id, SourceProduct product, string tenantKey, string sourceEntityId, string displayLabel, string actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(sourceEntityId)) throw new ArgumentException("Source identifiers are required.");
        lock (sync)
        {
            var profile = RequireProfile(id);
            if (profiles.Values.SelectMany(item => item.SourceLinks).Any(link => link.Product == product && link.TenantKey == tenantKey && link.SourceEntityId == sourceEntityId && link.Status != SourceLinkStatus.Revoked))
                throw new InvalidOperationException("That local player record is already linked or pending.");
            var link = new ProfileSourceLink(Guid.NewGuid(), product, tenantKey.Trim(), sourceEntityId.Trim(), displayLabel.Trim(), SourceLinkStatus.Pending, DateTimeOffset.UtcNow, null, null);
            profiles[id] = profile with { SourceLinks = profile.SourceLinks.Append(link).ToArray(), UpdatedAt = DateTimeOffset.UtcNow };
            RecordAudit(id, "source_link_requested", actor, "Pending local player link added");
        }
        return Task.CompletedTask;
    }

    // Applies only verified link events and makes duplicate delivery an explicit successful outcome.
    public Task<ProjectionApplyResult> ApplyProjectionAsync(ProfileProjectionEvent projectionEvent, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (!processedEvents.Add(projectionEvent.EventId)) return Task.FromResult(new ProjectionApplyResult(false, true, "duplicate", null));
            if (projectionEvent.PersonId is Guid personId && privacySuppressions.Contains(suppressionHasher.Hash(personId)))
                return Task.FromResult(new ProjectionApplyResult(false, false, "suppressed_privacy", null));
            var profile = profiles.Values.SingleOrDefault(candidate =>
                (projectionEvent.PersonId is not null && candidate.PersonId == projectionEvent.PersonId) ||
                candidate.SourceLinks.Any(link => link.Status == SourceLinkStatus.Verified && link.Product == projectionEvent.Product && link.TenantKey == projectionEvent.TenantKey && link.SourceEntityId == projectionEvent.SourceEntityId));
            if (profile is null) return Task.FromResult(new ProjectionApplyResult(false, false, "staged_unmatched", null));
            return Task.FromResult(new ProjectionApplyResult(false, false, "accepted_for_projection", profile.Id));
        }
    }

    // Deletes the complete profile aggregate and keeps only one keyed suppression plus request receipt.
    public Task<PlayerDataErasureResult> ErasePlayerDataAsync(PlayerDataErasureDirective directive, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (!processedErasureRequests.Add(directive.RequestId))
                return Task.FromResult(new PlayerDataErasureResult(directive.RequestId, true, true, 0));
            privacySuppressions.Add(suppressionHasher.Hash(directive.PersonId));
            var ids = profiles.Values.Where(profile => profile.PersonId == directive.PersonId).Select(profile => profile.Id).ToArray();
            foreach (var id in ids)
            {
                profiles.Remove(id);
                audit.RemoveAll(entry => entry.ProfileId == id);
            }
            return Task.FromResult(new PlayerDataErasureResult(directive.RequestId, true, false, ids.Length));
        }
    }

    // Exposes append-only evidence for the authorized profile management screen.
    public Task<IReadOnlyList<ProfileAuditEntry>> ListAuditAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (sync) return Task.FromResult<IReadOnlyList<ProfileAuditEntry>>(audit.Where(entry => entry.ProfileId == id).OrderByDescending(entry => entry.OccurredAt).ToArray());
    }

    // Centralizes missing-profile behavior so mutations never silently create a record from a guessed identifier.
    private PlayerProfile RequireProfile(Guid id) => profiles.GetValueOrDefault(id) ?? throw new KeyNotFoundException("Profile not found.");

    // Uses a compact reason-only audit in Development while the PostgreSQL store retains full request evidence.
    private void RecordAudit(Guid profileId, string action, string actor, string reason) => audit.Add(new ProfileAuditEntry(Guid.NewGuid(), profileId, action, actor, reason, DateTimeOffset.UtcNow));

    // Demonstrates mixed provenance and many-to-one linking without using any real player or venue data.
    private static IReadOnlyList<PlayerProfile> SeedProfiles()
    {
        var now = DateTimeOffset.UtcNow;
        var averyId = Guid.Parse("41000000-0000-0000-0000-000000000001");
        return
        [
            new PlayerProfile(
                averyId,
                Guid.Parse("42000000-0000-0000-0000-000000000001"),
                Guid.Parse("43000000-0000-0000-0000-000000000001"),
                "avery-brooks",
                "Avery Brooks",
                "Community competitor · 8-ball and 9-ball",
                "Avery plays weekly league and open community tournaments, and volunteers at junior pool clinics.",
                "Corner Pocket Billiards",
                "Riverton",
                "Ohio",
                "US",
                "9-ball",
                ProfileStatus.Published,
                now.AddYears(-2),
                now.AddDays(-1),
                [
                    new ProfileSourceLink(Guid.NewGuid(), SourceProduct.League, "riverton-weekly", "player-1042", "Riverton Tuesday League", SourceLinkStatus.Verified, now.AddYears(-2), now.AddYears(-2), null),
                    new ProfileSourceLink(Guid.NewGuid(), SourceProduct.League, "county-travel", "member-882", "County Travel League", SourceLinkStatus.Verified, now.AddYears(-1), now.AddYears(-1), null),
                    new ProfileSourceLink(Guid.NewGuid(), SourceProduct.Tournament, "corner-pocket", "entrant-2049", "Corner Pocket events", SourceLinkStatus.Verified, now.AddMonths(-8), now.AddMonths(-8), null)
                ],
                [
                    new PlayerAchievement(Guid.NewGuid(), "Summer room championship", "Won the local 8-ball room final.", "8-ball", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)), AchievementSource.CommunityEndorsed, "Corner Pocket Billiards", null, now.AddMonths(-2)),
                    new PlayerAchievement(Guid.NewGuid(), "Junior clinic volunteer", "Helped run four beginner tables.", "Community", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-4)), AchievementSource.PlayerReported, "Player reported", null, now.AddMonths(-4)),
                    new PlayerAchievement(Guid.NewGuid(), "Tuesday league sportsmanship award", "Selected by participating captains.", "8-ball", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)), AchievementSource.LeagueVerified, "Riverton Tuesday League", null, now.AddMonths(-6))
                ],
                [
                    new PlayerResult(Guid.NewGuid(), SourceProduct.Tournament, "corner-pocket", "event-2026-14", "Saturday Night Community Open", "9-ball", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 1, 16, 5, 0, now.AddDays(-1), null),
                    new PlayerResult(Guid.NewGuid(), SourceProduct.Tournament, "corner-pocket", "event-2026-11", "Spring 8-Ball Classic", "8-ball", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)), 3, 24, 4, 2, now.AddMonths(-2), null),
                    new PlayerResult(Guid.NewGuid(), SourceProduct.League, "riverton-weekly", "season-2026-spring", "Spring league season", "8-ball", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)), 2, 10, 14, 6, now.AddMonths(-3), null)
                ],
                []),
            new PlayerProfile(Guid.Parse("41000000-0000-0000-0000-000000000002"), Guid.NewGuid(), null, "jordan-lee", "Jordan Lee", "League regular · local tournament traveler", "Jordan enjoys team play, weekend tournaments, and introducing new players to the sport.", "Break Point Social Club", "Fairview", "Kentucky", "US", "8-ball", ProfileStatus.Published, now.AddMonths(-10), now.AddDays(-4), [], [new PlayerAchievement(Guid.NewGuid(), "Charity scotch doubles finalist", "Played in a community fundraiser final.", "Scotch doubles", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)), AchievementSource.PlayerReported, "Player reported", null, now.AddMonths(-1))], [], []),
            new PlayerProfile(Guid.Parse("41000000-0000-0000-0000-000000000003"), Guid.NewGuid(), null, "morgan-diaz", "Morgan Diaz", "One-pocket student · tournament volunteer", "Morgan tracks local results and helps directors keep events moving.", "The Green Room", "Lakeside", "Indiana", "US", "One-pocket", ProfileStatus.Draft, now.AddDays(-20), now.AddDays(-2), [], [], [], [])
        ];
    }
}
