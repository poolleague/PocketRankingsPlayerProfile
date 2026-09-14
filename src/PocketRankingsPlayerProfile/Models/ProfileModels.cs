using System.ComponentModel.DataAnnotations;

namespace PocketRankingsPlayerProfile.Models;

public enum ProfileStatus { Draft, Published, Hidden }
public enum SourceProduct { League, Tournament }
public enum SourceLinkStatus { Pending, Verified, Revoked }
public enum AchievementSource { PlayerReported, CommunityEndorsed, LeagueVerified, TournamentVerified }
public enum SocialProvider { Facebook, X }

// Separates the public profile identity from the future Account-owned PersonId and local product records.
public sealed record PlayerProfile(
    Guid Id,
    Guid PublicId,
    Guid? PersonId,
    string Slug,
    string DisplayName,
    string Headline,
    string Biography,
    string HomeRoom,
    string Locality,
    string Region,
    string CountryCode,
    string PrimaryDiscipline,
    ProfileStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ProfileSourceLink> SourceLinks,
    IReadOnlyList<PlayerAchievement> Achievements,
    IReadOnlyList<PlayerResult> Results,
    IReadOnlyList<PlayerSocialLink> SocialLinks);

// Records one local identity without treating a name as proof that two records belong to the same person.
public sealed record ProfileSourceLink(
    Guid Id,
    SourceProduct Product,
    string TenantKey,
    string SourceEntityId,
    string DisplayLabel,
    SourceLinkStatus Status,
    DateTimeOffset LinkedAt,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? RevokedAt);

// Preserves informal community accomplishments while keeping their provenance visible to readers.
public sealed record PlayerAchievement(
    Guid Id,
    string Title,
    string Description,
    string Discipline,
    DateOnly AchievedOn,
    AchievementSource Source,
    string SourceLabel,
    string? EvidenceUrl,
    DateTimeOffset RecordedAt);

// Keeps source results immutable enough to rebuild statistics after corrections or replay.
public sealed record PlayerResult(
    Guid Id,
    SourceProduct Product,
    string TenantKey,
    string SourceEventId,
    string EventName,
    string Discipline,
    DateOnly PlayedOn,
    int Placement,
    int FieldSize,
    int MatchesWon,
    int MatchesLost,
    DateTimeOffset RecordedAt,
    DateTimeOffset? RetractedAt);

// Stores only a public social address and an explicit choice to offer the third-party embed.
public sealed record PlayerSocialLink(
    Guid Id,
    SocialProvider Provider,
    string PublicUrl,
    bool EmbedEnabled,
    DateTimeOffset UpdatedAt);

public sealed record ProfileStatistics(
    int EventsPlayed,
    int EventWins,
    int PodiumFinishes,
    int MatchesWon,
    int MatchesLost,
    decimal MatchWinPercentage);

public sealed record PlayerDirectoryViewModel(IReadOnlyList<PlayerProfile> Profiles);
public sealed record PlayerProfileViewModel(PlayerProfile Profile, ProfileStatistics Statistics);

// Creates a new durable profile in draft state without asserting an Account or source identity.
public sealed class CreateProfileInput
{
    [Required, StringLength(80)] public string DisplayName { get; set; } = "";
    [Required, StringLength(80), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] public string Slug { get; set; } = "";
    [Required, StringLength(40)] public string PrimaryDiscipline { get; set; } = "8-ball";
}

// Provides one bounded form for player-managed biography fields without accepting identity claims.
public sealed class EditProfileInput
{
    [Required, StringLength(80)] public string DisplayName { get; set; } = "";
    [StringLength(120)] public string Headline { get; set; } = "";
    [StringLength(2000)] public string Biography { get; set; } = "";
    [StringLength(100)] public string HomeRoom { get; set; } = "";
    [StringLength(100)] public string Locality { get; set; } = "";
    [StringLength(100)] public string Region { get; set; } = "";
    [RegularExpression("^[A-Z]{2}$")] public string CountryCode { get; set; } = "US";
    [Required, StringLength(40)] public string PrimaryDiscipline { get; set; } = "8-ball";
}

// Makes player-reported status explicit at input time instead of allowing an official-looking free-form record.
public sealed class AddAchievementInput
{
    [Required, StringLength(120)] public string Title { get; set; } = "";
    [StringLength(600)] public string Description { get; set; } = "";
    [Required, StringLength(40)] public string Discipline { get; set; } = "8-ball";
    [Required] public DateOnly AchievedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [StringLength(100)] public string SourceLabel { get; set; } = "Player reported";
    [Url, StringLength(500)] public string? EvidenceUrl { get; set; }
}

// Restricts social configuration to the two approved public providers and never accepts credentials.
public sealed class UpdateSocialLinksInput
{
    [Url, StringLength(500)] public string? FacebookUrl { get; set; }
    public bool FacebookEmbedEnabled { get; set; }
    [Url, StringLength(500)] public string? XUrl { get; set; }
    public bool XEmbedEnabled { get; set; }
}

// Carries a versioned, replayable projection event without creating a runtime dependency on a producer.
public sealed record ProfileProjectionEvent(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    SourceProduct Product,
    string TenantKey,
    string SourceEntityId,
    Guid? PersonId,
    DateTimeOffset OccurredAt,
    string PayloadJson);
