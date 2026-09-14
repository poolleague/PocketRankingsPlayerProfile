BEGIN;

CREATE SCHEMA IF NOT EXISTS profile;

CREATE TABLE IF NOT EXISTS profile.schema_migrations (
    version integer PRIMARY KEY,
    description text NOT NULL,
    applied_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS profile.player_profiles (
    profile_id uuid PRIMARY KEY,
    public_id uuid NOT NULL UNIQUE,
    person_id uuid UNIQUE,
    slug varchar(80) NOT NULL,
    display_name varchar(80) NOT NULL,
    headline varchar(120) NOT NULL DEFAULT '',
    biography varchar(2000) NOT NULL DEFAULT '',
    home_room varchar(100) NOT NULL DEFAULT '',
    locality varchar(100) NOT NULL DEFAULT '',
    region varchar(100) NOT NULL DEFAULT '',
    country_code char(2) NOT NULL DEFAULT 'US',
    primary_discipline varchar(40) NOT NULL DEFAULT '8-ball',
    profile_status varchar(16) NOT NULL DEFAULT 'draft' CHECK (profile_status IN ('draft', 'published', 'hidden')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_player_profiles_slug CHECK (slug ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'),
    CONSTRAINT ck_player_profiles_country CHECK (country_code ~ '^[A-Z]{2}$')
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_player_profiles_slug_lower ON profile.player_profiles (lower(slug));

CREATE TABLE IF NOT EXISTS profile.profile_source_links (
    source_link_id uuid PRIMARY KEY,
    profile_id uuid NOT NULL REFERENCES profile.player_profiles(profile_id),
    source_product varchar(16) NOT NULL CHECK (source_product IN ('league', 'tournament')),
    tenant_key varchar(100) NOT NULL,
    source_entity_id varchar(160) NOT NULL,
    display_label varchar(160) NOT NULL,
    link_status varchar(16) NOT NULL DEFAULT 'pending' CHECK (link_status IN ('pending', 'verified', 'revoked')),
    linked_at timestamptz NOT NULL DEFAULT now(),
    verified_at timestamptz,
    revoked_at timestamptz,
    CONSTRAINT ck_source_link_state CHECK (
        (link_status = 'pending' AND verified_at IS NULL AND revoked_at IS NULL) OR
        (link_status = 'verified' AND verified_at IS NOT NULL AND revoked_at IS NULL) OR
        (link_status = 'revoked' AND revoked_at IS NOT NULL)
    )
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_active_source_identity
    ON profile.profile_source_links(source_product, tenant_key, source_entity_id)
    WHERE link_status <> 'revoked';
CREATE INDEX IF NOT EXISTS ix_source_links_profile ON profile.profile_source_links(profile_id);

CREATE TABLE IF NOT EXISTS profile.player_achievements (
    achievement_id uuid PRIMARY KEY,
    profile_id uuid NOT NULL REFERENCES profile.player_profiles(profile_id),
    title varchar(120) NOT NULL,
    description varchar(600) NOT NULL DEFAULT '',
    discipline varchar(40) NOT NULL,
    achieved_on date NOT NULL,
    source_kind varchar(24) NOT NULL CHECK (source_kind IN ('player_reported', 'community_endorsed', 'league_verified', 'tournament_verified')),
    source_label varchar(100) NOT NULL,
    evidence_url varchar(500),
    source_product varchar(16) CHECK (source_product IN ('league', 'tournament')),
    source_record_id varchar(160),
    recorded_at timestamptz NOT NULL DEFAULT now(),
    retracted_at timestamptz,
    CONSTRAINT ck_verified_achievement_origin CHECK (
        source_kind NOT IN ('league_verified', 'tournament_verified') OR
        (source_product IS NOT NULL AND source_record_id IS NOT NULL)
    )
);
CREATE INDEX IF NOT EXISTS ix_achievements_profile_date ON profile.player_achievements(profile_id, achieved_on DESC);

CREATE TABLE IF NOT EXISTS profile.player_results (
    result_id uuid PRIMARY KEY,
    profile_id uuid NOT NULL REFERENCES profile.player_profiles(profile_id),
    source_product varchar(16) NOT NULL CHECK (source_product IN ('league', 'tournament')),
    tenant_key varchar(100) NOT NULL,
    source_event_id varchar(160) NOT NULL,
    event_name varchar(160) NOT NULL,
    discipline varchar(40) NOT NULL,
    played_on date NOT NULL,
    placement integer NOT NULL CHECK (placement > 0),
    field_size integer NOT NULL CHECK (field_size >= placement),
    matches_won integer NOT NULL CHECK (matches_won >= 0),
    matches_lost integer NOT NULL CHECK (matches_lost >= 0),
    recorded_at timestamptz NOT NULL DEFAULT now(),
    retracted_at timestamptz,
    UNIQUE(source_product, tenant_key, source_event_id, profile_id)
);
CREATE INDEX IF NOT EXISTS ix_results_profile_date ON profile.player_results(profile_id, played_on DESC);

CREATE TABLE IF NOT EXISTS profile.player_social_links (
    social_link_id uuid PRIMARY KEY,
    profile_id uuid NOT NULL REFERENCES profile.player_profiles(profile_id),
    provider varchar(16) NOT NULL CHECK (provider IN ('facebook', 'x')),
    public_url varchar(500) NOT NULL,
    embed_enabled boolean NOT NULL DEFAULT false,
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(profile_id, provider)
);

CREATE TABLE IF NOT EXISTS profile.profile_status_history (
    status_history_id uuid PRIMARY KEY,
    profile_id uuid NOT NULL REFERENCES profile.player_profiles(profile_id),
    prior_status varchar(16) NOT NULL,
    new_status varchar(16) NOT NULL,
    actor varchar(160) NOT NULL,
    reason varchar(500) NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS profile.integration_inbox (
    event_id uuid PRIMARY KEY,
    event_type varchar(80) NOT NULL,
    schema_version integer NOT NULL CHECK (schema_version > 0),
    source_product varchar(16) NOT NULL CHECK (source_product IN ('league', 'tournament')),
    tenant_key varchar(100) NOT NULL,
    source_entity_id varchar(160) NOT NULL,
    person_id uuid,
    occurred_at timestamptz NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    payload jsonb NOT NULL,
    processing_status varchar(24) NOT NULL CHECK (processing_status IN ('received', 'applied', 'staged_unmatched', 'rejected')),
    processing_reason varchar(500) NOT NULL DEFAULT '',
    profile_id uuid REFERENCES profile.player_profiles(profile_id)
);
CREATE INDEX IF NOT EXISTS ix_integration_inbox_unmatched ON profile.integration_inbox(received_at) WHERE processing_status = 'staged_unmatched';

CREATE TABLE IF NOT EXISTS profile.profile_audit_log (
    audit_id uuid PRIMARY KEY,
    profile_id uuid NOT NULL REFERENCES profile.player_profiles(profile_id),
    action varchar(80) NOT NULL,
    actor varchar(160) NOT NULL,
    reason varchar(500) NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_audit_profile_date ON profile.profile_audit_log(profile_id, occurred_at DESC);

-- Audit entries are historical evidence; corrections are represented by a later entry.
CREATE OR REPLACE FUNCTION profile.reject_audit_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'profile audit entries are append-only';
END;
$$;
DROP TRIGGER IF EXISTS profile_audit_append_only ON profile.profile_audit_log;
CREATE TRIGGER profile_audit_append_only BEFORE UPDATE OR DELETE ON profile.profile_audit_log
FOR EACH ROW EXECUTE FUNCTION profile.reject_audit_mutation();

INSERT INTO profile.schema_migrations(version, description)
VALUES (1, 'Initial player identity, source links, history, social links, projections, and audit schema')
ON CONFLICT (version) DO NOTHING;

COMMIT;
