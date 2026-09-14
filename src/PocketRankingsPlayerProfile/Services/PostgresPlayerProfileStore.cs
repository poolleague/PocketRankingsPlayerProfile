using System.Text.Json;
using Npgsql;
using PocketRankingsPlayerProfile.Models;

namespace PocketRankingsPlayerProfile.Services;

// Persists the profile-owned read model without reaching into League, Tournament, or Account databases.
public sealed class PostgresPlayerProfileStore(NpgsqlDataSource dataSource) : IPlayerProfileStore
{
    // Filters publication in SQL so an accidental view change cannot expose non-public profiles.
    public async Task<IReadOnlyList<PlayerProfile>> ListPublishedAsync(CancellationToken cancellationToken) =>
        await LoadProfilesAsync("WHERE p.profile_status = 'published' ORDER BY p.display_name", null, cancellationToken);

    // Returns lifecycle-visible records for authorized management callers.
    public async Task<IReadOnlyList<PlayerProfile>> ListAllAsync(CancellationToken cancellationToken) =>
        await LoadProfilesAsync("ORDER BY p.display_name", null, cancellationToken);

    // Combines a publication guard with a case-insensitive slug lookup.
    public async Task<PlayerProfile?> FindPublishedBySlugAsync(string slug, CancellationToken cancellationToken) =>
        (await LoadProfilesAsync("WHERE p.profile_status = 'published' AND lower(p.slug) = lower(@lookup)", slug, cancellationToken)).SingleOrDefault();

    // Retrieves drafts for management without changing their public visibility.
    public async Task<PlayerProfile?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await LoadProfilesAsync("WHERE p.profile_id = @profile_id", id, cancellationToken)).SingleOrDefault();

    // Creates an empty draft and its first audit entry in one transaction.
    public async Task<Guid> CreateProfileAsync(CreateProfileInput input, string actor, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("INSERT INTO profile.player_profiles(profile_id, public_id, slug, display_name, primary_discipline) VALUES (@id, @public, @slug, @name, @discipline)", connection, transaction);
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("public", Guid.NewGuid()); command.Parameters.AddWithValue("slug", input.Slug); command.Parameters.AddWithValue("name", input.DisplayName.Trim()); command.Parameters.AddWithValue("discipline", input.PrimaryDiscipline.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
        await InsertAuditAsync(connection, transaction, id, "profile_created", actor, "Draft profile created", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    // Updates narrative data only and leaves every identity and source-owned field untouched.
    public async Task UpdateProfileAsync(Guid id, EditProfileInput input, string actor, CancellationToken cancellationToken)
    {
        await InTransactionAsync(async (connection, transaction) =>
        {
            await using var command = new NpgsqlCommand("UPDATE profile.player_profiles SET display_name=@name, headline=@headline, biography=@bio, home_room=@room, locality=@locality, region=@region, country_code=@country, primary_discipline=@discipline, updated_at=now() WHERE profile_id=@id", connection, transaction);
            command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("name", input.DisplayName.Trim()); command.Parameters.AddWithValue("headline", input.Headline.Trim()); command.Parameters.AddWithValue("bio", input.Biography.Trim()); command.Parameters.AddWithValue("room", input.HomeRoom.Trim()); command.Parameters.AddWithValue("locality", input.Locality.Trim()); command.Parameters.AddWithValue("region", input.Region.Trim()); command.Parameters.AddWithValue("country", input.CountryCode.Trim().ToUpperInvariant()); command.Parameters.AddWithValue("discipline", input.PrimaryDiscipline.Trim());
            await RequireChangedAsync(command, cancellationToken);
            await InsertAuditAsync(connection, transaction, id, "profile_updated", actor, "Player-facing profile details updated", cancellationToken);
        }, cancellationToken);
    }

    // Hard-codes player_reported so request binding cannot manufacture a verified accomplishment.
    public async Task AddPlayerReportedAchievementAsync(Guid id, AddAchievementInput input, string actor, CancellationToken cancellationToken)
    {
        await InTransactionAsync(async (connection, transaction) =>
        {
            await using var command = new NpgsqlCommand("INSERT INTO profile.player_achievements(achievement_id, profile_id, title, description, discipline, achieved_on, source_kind, source_label, evidence_url) VALUES (@aid,@id,@title,@description,@discipline,@date,'player_reported',@label,@url)", connection, transaction);
            command.Parameters.AddWithValue("aid", Guid.NewGuid()); command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("title", input.Title.Trim()); command.Parameters.AddWithValue("description", input.Description.Trim()); command.Parameters.AddWithValue("discipline", input.Discipline.Trim()); command.Parameters.AddWithValue("date", input.AchievedOn); command.Parameters.AddWithValue("label", string.IsNullOrWhiteSpace(input.SourceLabel) ? "Player reported" : input.SourceLabel.Trim()); command.Parameters.AddWithValue("url", (object?)input.EvidenceUrl?.Trim() ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await InsertAuditAsync(connection, transaction, id, "reported_achievement_added", actor, "Player-reported accomplishment added", cancellationToken);
        }, cancellationToken);
    }

    // Replaces the two allowed provider rows after validating both URLs before opening the transaction.
    public async Task UpdateSocialLinksAsync(Guid id, UpdateSocialLinksInput input, string actor, CancellationToken cancellationToken)
    {
        var facebook = SocialLinkPolicy.Normalize(SocialProvider.Facebook, input.FacebookUrl);
        var x = SocialLinkPolicy.Normalize(SocialProvider.X, input.XUrl);
        await InTransactionAsync(async (connection, transaction) =>
        {
            await using (var delete = new NpgsqlCommand("DELETE FROM profile.player_social_links WHERE profile_id=@id", connection, transaction)) { delete.Parameters.AddWithValue("id", id); await delete.ExecuteNonQueryAsync(cancellationToken); }
            if (facebook is not null) await InsertSocialAsync(connection, transaction, id, "facebook", facebook, input.FacebookEmbedEnabled, cancellationToken);
            if (x is not null) await InsertSocialAsync(connection, transaction, id, "x", x, input.XEmbedEnabled, cancellationToken);
            await InsertAuditAsync(connection, transaction, id, "social_links_updated", actor, "Public social addresses updated", cancellationToken);
        }, cancellationToken);
    }

    // Captures both prior and new states before updating the public visibility decision.
    public async Task SetStatusAsync(Guid id, ProfileStatus status, string actor, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        await InTransactionAsync(async (connection, transaction) =>
        {
            string prior;
            await using (var read = new NpgsqlCommand("SELECT profile_status FROM profile.player_profiles WHERE profile_id=@id FOR UPDATE", connection, transaction)) { read.Parameters.AddWithValue("id", id); prior = (string?)await read.ExecuteScalarAsync(cancellationToken) ?? throw new KeyNotFoundException("Profile not found."); }
            var next = ToDb(status);
            await using (var update = new NpgsqlCommand("UPDATE profile.player_profiles SET profile_status=@status, updated_at=now() WHERE profile_id=@id", connection, transaction)) { update.Parameters.AddWithValue("status", next); update.Parameters.AddWithValue("id", id); await update.ExecuteNonQueryAsync(cancellationToken); }
            await using (var history = new NpgsqlCommand("INSERT INTO profile.profile_status_history(status_history_id,profile_id,prior_status,new_status,actor,reason) VALUES (@hid,@id,@prior,@next,@actor,@reason)", connection, transaction)) { history.Parameters.AddWithValue("hid", Guid.NewGuid()); history.Parameters.AddWithValue("id", id); history.Parameters.AddWithValue("prior", prior); history.Parameters.AddWithValue("next", next); history.Parameters.AddWithValue("actor", actor); history.Parameters.AddWithValue("reason", reason.Trim()); await history.ExecuteNonQueryAsync(cancellationToken); }
            await InsertAuditAsync(connection, transaction, id, $"profile_{next}", actor, reason.Trim(), cancellationToken);
        }, cancellationToken);
    }

    // Inserts pending state only; database uniqueness prevents competing active claims across profiles.
    public async Task AddPendingSourceLinkAsync(Guid id, SourceProduct product, string tenantKey, string sourceEntityId, string displayLabel, string actor, CancellationToken cancellationToken)
    {
        await InTransactionAsync(async (connection, transaction) =>
        {
            await using var command = new NpgsqlCommand("INSERT INTO profile.profile_source_links(source_link_id,profile_id,source_product,tenant_key,source_entity_id,display_label) VALUES (@lid,@id,@product,@tenant,@source,@label)", connection, transaction);
            command.Parameters.AddWithValue("lid", Guid.NewGuid()); command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("product", ToDb(product)); command.Parameters.AddWithValue("tenant", tenantKey.Trim()); command.Parameters.AddWithValue("source", sourceEntityId.Trim()); command.Parameters.AddWithValue("label", displayLabel.Trim());
            await command.ExecuteNonQueryAsync(cancellationToken);
            await InsertAuditAsync(connection, transaction, id, "source_link_requested", actor, "Pending local player link added", cancellationToken);
        }, cancellationToken);
    }

    // Deduplicates first, then stages unmatched events instead of guessing identity from payload content.
    public async Task<ProjectionApplyResult> ApplyProjectionAsync(ProfileProjectionEvent projectionEvent, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        Guid? profileId = null;
        await using (var match = new NpgsqlCommand("SELECT p.profile_id FROM profile.player_profiles p WHERE (@person IS NOT NULL AND p.person_id=@person) OR EXISTS (SELECT 1 FROM profile.profile_source_links l WHERE l.profile_id=p.profile_id AND l.link_status='verified' AND l.source_product=@product AND l.tenant_key=@tenant AND l.source_entity_id=@source) LIMIT 1", connection, transaction))
        { match.Parameters.AddWithValue("person", (object?)projectionEvent.PersonId ?? DBNull.Value); match.Parameters.AddWithValue("product", ToDb(projectionEvent.Product)); match.Parameters.AddWithValue("tenant", projectionEvent.TenantKey); match.Parameters.AddWithValue("source", projectionEvent.SourceEntityId); profileId = (Guid?)await match.ExecuteScalarAsync(cancellationToken); }
        var outcome = profileId is null ? "staged_unmatched" : "received";
        await using var inbox = new NpgsqlCommand("INSERT INTO profile.integration_inbox(event_id,event_type,schema_version,source_product,tenant_key,source_entity_id,person_id,occurred_at,payload,processing_status,processing_reason,profile_id) VALUES (@event,@type,@version,@product,@tenant,@source,@person,@occurred,CAST(@payload AS jsonb),@status,@reason,@profile) ON CONFLICT (event_id) DO NOTHING", connection, transaction);
        inbox.Parameters.AddWithValue("event", projectionEvent.EventId); inbox.Parameters.AddWithValue("type", projectionEvent.EventType); inbox.Parameters.AddWithValue("version", projectionEvent.SchemaVersion); inbox.Parameters.AddWithValue("product", ToDb(projectionEvent.Product)); inbox.Parameters.AddWithValue("tenant", projectionEvent.TenantKey); inbox.Parameters.AddWithValue("source", projectionEvent.SourceEntityId); inbox.Parameters.AddWithValue("person", (object?)projectionEvent.PersonId ?? DBNull.Value); inbox.Parameters.AddWithValue("occurred", projectionEvent.OccurredAt); inbox.Parameters.AddWithValue("payload", projectionEvent.PayloadJson); inbox.Parameters.AddWithValue("status", outcome); inbox.Parameters.AddWithValue("reason", profileId is null ? "No verified identity link" : "Awaiting supported projection handler"); inbox.Parameters.AddWithValue("profile", (object?)profileId ?? DBNull.Value);
        try { JsonDocument.Parse(projectionEvent.PayloadJson); } catch (JsonException) { throw new ArgumentException("Projection payload must be valid JSON."); }
        var inserted = await inbox.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return inserted == 0 ? new(false, true, "duplicate", null) : new(false, false, profileId is null ? outcome : "accepted_for_projection", profileId);
    }

    // Reads append-only audit history newest first for management review.
    public async Task<IReadOnlyList<ProfileAuditEntry>> ListAuditAsync(Guid id, CancellationToken cancellationToken)
    {
        var entries = new List<ProfileAuditEntry>();
        await using var command = dataSource.CreateCommand("SELECT audit_id,profile_id,action,actor,reason,occurred_at FROM profile.profile_audit_log WHERE profile_id=@id ORDER BY occurred_at DESC"); command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) entries.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5)));
        return entries;
    }

    // Builds complete immutable aggregate records from product-owned tables, avoiding mutable navigation state.
    private async Task<IReadOnlyList<PlayerProfile>> LoadProfilesAsync(string filter, object? lookup, CancellationToken cancellationToken)
    {
        var profiles = new List<PlayerProfile>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"SELECT profile_id,public_id,person_id,slug,display_name,headline,biography,home_room,locality,region,country_code,primary_discipline,profile_status,created_at,updated_at FROM profile.player_profiles p {filter}", connection);
        if (lookup is Guid id) command.Parameters.AddWithValue("profile_id", id); else if (lookup is string slug) command.Parameters.AddWithValue("lookup", slug);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(Guid Id, Guid PublicId, Guid? PersonId, string Slug, string Name, string Headline, string Bio, string Room, string Locality, string Region, string Country, string Discipline, ProfileStatus Status, DateTimeOffset Created, DateTimeOffset Updated)>();
        while (await reader.ReadAsync(cancellationToken)) rows.Add((reader.GetGuid(0),reader.GetGuid(1),reader.IsDBNull(2)?null:reader.GetGuid(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetString(8),reader.GetString(9),reader.GetString(10),reader.GetString(11),ParseStatus(reader.GetString(12)),reader.GetFieldValue<DateTimeOffset>(13),reader.GetFieldValue<DateTimeOffset>(14)));
        await reader.CloseAsync();
        foreach (var row in rows) profiles.Add(new PlayerProfile(row.Id,row.PublicId,row.PersonId,row.Slug,row.Name,row.Headline,row.Bio,row.Room,row.Locality,row.Region,row.Country,row.Discipline,row.Status,row.Created,row.Updated,await LoadLinksAsync(connection,row.Id,cancellationToken),await LoadAchievementsAsync(connection,row.Id,cancellationToken),await LoadResultsAsync(connection,row.Id,cancellationToken),await LoadSocialAsync(connection,row.Id,cancellationToken)));
        return profiles;
    }

    // Loads every local identity claim so public and management views can show its exact verification state.
    private static async Task<IReadOnlyList<ProfileSourceLink>> LoadLinksAsync(NpgsqlConnection connection, Guid id, CancellationToken token) { var list=new List<ProfileSourceLink>(); await using var c=new NpgsqlCommand("SELECT source_link_id,source_product,tenant_key,source_entity_id,display_label,link_status,linked_at,verified_at,revoked_at FROM profile.profile_source_links WHERE profile_id=@id",connection);c.Parameters.AddWithValue("id",id);await using var r=await c.ExecuteReaderAsync(token);while(await r.ReadAsync(token))list.Add(new(r.GetGuid(0),ParseProduct(r.GetString(1)),r.GetString(2),r.GetString(3),r.GetString(4),Enum.Parse<SourceLinkStatus>(r.GetString(5),true),r.GetFieldValue<DateTimeOffset>(6),r.IsDBNull(7)?null:r.GetFieldValue<DateTimeOffset>(7),r.IsDBNull(8)?null:r.GetFieldValue<DateTimeOffset>(8)));return list; }
    // Omits retracted accomplishments from the current profile while retaining their database history.
    private static async Task<IReadOnlyList<PlayerAchievement>> LoadAchievementsAsync(NpgsqlConnection connection, Guid id, CancellationToken token) { var list=new List<PlayerAchievement>();await using var c=new NpgsqlCommand("SELECT achievement_id,title,description,discipline,achieved_on,source_kind,source_label,evidence_url,recorded_at FROM profile.player_achievements WHERE profile_id=@id AND retracted_at IS NULL ORDER BY achieved_on DESC",connection);c.Parameters.AddWithValue("id",id);await using var r=await c.ExecuteReaderAsync(token);while(await r.ReadAsync(token))list.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetFieldValue<DateOnly>(4),ParseAchievementSource(r.GetString(5)),r.GetString(6),r.IsDBNull(7)?null:r.GetString(7),r.GetFieldValue<DateTimeOffset>(8)));return list; }
    // Includes retraction timestamps because the statistics calculator is the single active-result filter.
    private static async Task<IReadOnlyList<PlayerResult>> LoadResultsAsync(NpgsqlConnection connection, Guid id, CancellationToken token) { var list=new List<PlayerResult>();await using var c=new NpgsqlCommand("SELECT result_id,source_product,tenant_key,source_event_id,event_name,discipline,played_on,placement,field_size,matches_won,matches_lost,recorded_at,retracted_at FROM profile.player_results WHERE profile_id=@id ORDER BY played_on DESC",connection);c.Parameters.AddWithValue("id",id);await using var r=await c.ExecuteReaderAsync(token);while(await r.ReadAsync(token))list.Add(new(r.GetGuid(0),ParseProduct(r.GetString(1)),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetFieldValue<DateOnly>(6),r.GetInt32(7),r.GetInt32(8),r.GetInt32(9),r.GetInt32(10),r.GetFieldValue<DateTimeOffset>(11),r.IsDBNull(12)?null:r.GetFieldValue<DateTimeOffset>(12)));return list; }
    // Loads only the two allowlisted providers represented by the database constraint.
    private static async Task<IReadOnlyList<PlayerSocialLink>> LoadSocialAsync(NpgsqlConnection connection, Guid id, CancellationToken token) { var list=new List<PlayerSocialLink>();await using var c=new NpgsqlCommand("SELECT social_link_id,provider,public_url,embed_enabled,updated_at FROM profile.player_social_links WHERE profile_id=@id ORDER BY provider",connection);c.Parameters.AddWithValue("id",id);await using var r=await c.ExecuteReaderAsync(token);while(await r.ReadAsync(token))list.Add(new(r.GetGuid(0),r.GetString(1)=="facebook"?SocialProvider.Facebook:SocialProvider.X,r.GetString(2),r.GetBoolean(3),r.GetFieldValue<DateTimeOffset>(4)));return list; }

    // Ensures every multi-row mutation either records its audit evidence or makes no change.
    private async Task InTransactionAsync(Func<NpgsqlConnection,NpgsqlTransaction,Task> action, CancellationToken token) { await using var connection=await dataSource.OpenConnectionAsync(token);await using var transaction=await connection.BeginTransactionAsync(token);await action(connection,transaction);await transaction.CommitAsync(token); }
    // Uses one audit insertion path so every mutation records the same minimum evidence.
    private static async Task InsertAuditAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid id,string action,string actor,string reason,CancellationToken token) { await using var command=new NpgsqlCommand("INSERT INTO profile.profile_audit_log(audit_id,profile_id,action,actor,reason) VALUES (@audit,@id,@action,@actor,@reason)",connection,transaction);command.Parameters.AddWithValue("audit",Guid.NewGuid());command.Parameters.AddWithValue("id",id);command.Parameters.AddWithValue("action",action);command.Parameters.AddWithValue("actor",actor);command.Parameters.AddWithValue("reason",reason);await command.ExecuteNonQueryAsync(token); }
    // Persists normalized URLs only after the shared provider policy succeeds.
    private static async Task InsertSocialAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid id,string provider,string url,bool enabled,CancellationToken token) { await using var command=new NpgsqlCommand("INSERT INTO profile.player_social_links(social_link_id,profile_id,provider,public_url,embed_enabled) VALUES (@social,@id,@provider,@url,@enabled)",connection,transaction);command.Parameters.AddWithValue("social",Guid.NewGuid());command.Parameters.AddWithValue("id",id);command.Parameters.AddWithValue("provider",provider);command.Parameters.AddWithValue("url",url);command.Parameters.AddWithValue("enabled",enabled);await command.ExecuteNonQueryAsync(token); }
    // Converts a zero-row update into an explicit missing-profile failure rather than a false success.
    private static async Task RequireChangedAsync(NpgsqlCommand command,CancellationToken token) { if(await command.ExecuteNonQueryAsync(token)==0)throw new KeyNotFoundException("Profile not found."); }
    // Keeps enum persistence stable and independent of CLR casing.
    private static string ToDb(ProfileStatus value)=>value.ToString().ToLowerInvariant();
    // Uses the same stable lowercase representation for producer names.
    private static string ToDb(SourceProduct value)=>value.ToString().ToLowerInvariant();
    // Restores database lifecycle values to the public model.
    private static ProfileStatus ParseStatus(string value)=>Enum.Parse<ProfileStatus>(value,true);
    // Restores the constrained source-product token to the public model.
    private static SourceProduct ParseProduct(string value)=>Enum.Parse<SourceProduct>(value,true);
    // Maps underscore-delimited provenance values without presenting them as raw UI terms.
    private static AchievementSource ParseAchievementSource(string value)=>value switch{"league_verified"=>AchievementSource.LeagueVerified,"tournament_verified"=>AchievementSource.TournamentVerified,"community_endorsed"=>AchievementSource.CommunityEndorsed,_=>AchievementSource.PlayerReported};
}

// Applies the checked-in idempotent schema before the app accepts Production traffic.
public sealed class PostgresSchemaInitializer(NpgsqlDataSource dataSource, IWebHostEnvironment environment)
{
    // Reads the deployed migration artifact so the running image and repository share one source of truth.
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(environment.ContentRootPath, "Database", "001_initial_schema.sql");
        await using var command = dataSource.CreateCommand(await File.ReadAllTextAsync(path, cancellationToken));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
