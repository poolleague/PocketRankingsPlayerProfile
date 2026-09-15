# Player Data Privacy

An irreversible Account opt-out deletes the player's complete Profile-owned graph: public profile and slug, biography, location, social links, achievements, projected results and statistics, source links, status history, and profile-bound audit entries. The public route then returns not found.

Only two non-identifying controls remain: the Account request ID receipt and an HMAC-SHA-256 suppression of `PersonId` using a Player-Profile-only secret. No raw `PersonId`, profile identifier, source identity, name, or reverse mapping is retained in those controls. Replayed requests are idempotent. Future projections for the suppressed identity return `suppressed_privacy` and cannot recreate a profile. A later opt-in begins only with future activity.

The PostgreSQL action is one transaction. Rollback of application code must retain suppression/receipt tables. Any restored backup must replay completed Account directives before public or projection traffic is enabled. The secret must be backed up through the approved secrets process; losing it prevents deterministic suppression checks, while exposing it weakens the one-way property.

The store operation is implemented, but signed delivery from Account, authentication of the directive, acknowledgements, retries, and alerting are not active yet. Until those are added and validated, Account must show the request as processing.
