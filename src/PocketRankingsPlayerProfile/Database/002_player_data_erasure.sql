BEGIN;

CREATE TABLE IF NOT EXISTS profile.privacy_suppressions (
    suppression_hash char(64) PRIMARY KEY,
    first_request_id uuid NOT NULL UNIQUE,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_privacy_suppression_hash CHECK (suppression_hash ~ '^[0-9a-f]{64}$')
);

CREATE TABLE IF NOT EXISTS profile.privacy_erasure_receipts (
    request_id uuid PRIMARY KEY,
    token_id uuid NOT NULL UNIQUE,
    profiles_deleted integer NOT NULL CHECK (profiles_deleted >= 0),
    completed_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE profile.privacy_erasure_receipts ADD COLUMN IF NOT EXISTS token_id uuid;
CREATE UNIQUE INDEX IF NOT EXISTS ux_privacy_erasure_receipt_token ON profile.privacy_erasure_receipts(token_id) WHERE token_id IS NOT NULL;

-- The normal audit trigger still rejects changes; only the erasure transaction's local flag permits removal.
CREATE OR REPLACE FUNCTION profile.reject_audit_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF current_setting('pocketrankings.privacy_erasure', true) = 'on' THEN
        RETURN OLD;
    END IF;
    RAISE EXCEPTION 'profile audit entries are append-only';
END;
$$;

-- Erasure receipts are non-identifying control evidence and must remain append-only.
CREATE OR REPLACE FUNCTION profile.reject_privacy_receipt_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'profile privacy receipts are append-only';
END;
$$;
DROP TRIGGER IF EXISTS privacy_receipt_append_only ON profile.privacy_erasure_receipts;
CREATE TRIGGER privacy_receipt_append_only BEFORE UPDATE OR DELETE ON profile.privacy_erasure_receipts
FOR EACH ROW EXECUTE FUNCTION profile.reject_privacy_receipt_mutation();

INSERT INTO profile.schema_migrations(version, description)
VALUES (2, 'Irreversible player-profile erasure receipts and keyed future-data suppression')
ON CONFLICT (version) DO NOTHING;

COMMIT;
