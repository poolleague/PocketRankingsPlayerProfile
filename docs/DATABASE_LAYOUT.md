# PostgreSQL database layout

Schema: `profile`; introduced in `0.1.0-development` by `Database/001_initial_schema.sql` and extended in 0.2.0 by `002_player_data_erasure.sql`.

| Object | Purpose |
|---|---|
| `schema_migrations` | Applied migration ledger |
| `player_profiles` | Durable public identity, narrative fields, and lifecycle |
| `profile_source_links` | Many exact local identities linked to one profile |
| `player_achievements` | Provenance-labeled accomplishments and retractions |
| `player_results` | Source-owned results used to derive verified totals |
| `player_social_links` | Public Facebook/X URLs and embed preference only |
| `profile_status_history` | Lifecycle transitions with actor and reason |
| `integration_inbox` | Deduplicated source events and unmatched staging |
| `profile_audit_log` | Append-only management evidence |
| `reject_audit_mutation()` | Trigger function preventing audit update/delete |
| `privacy_suppressions` | Keyed one-way PersonId suppressions; contains no raw PersonId |
| `privacy_erasure_receipts` | Non-identifying, append-only completion evidence |

The migration is transactional and idempotent. Active local source identities are globally unique within this product. It creates no foreign keys, connections, credentials, or dependencies to another product database.

During an approved erasure transaction, the audit trigger permits deletion of profile-bound audit entries through a transaction-local flag. All other audit changes remain rejected. Code rollback must preserve suppressions and receipts; backup restore must replay erasure directives before normal traffic resumes.
