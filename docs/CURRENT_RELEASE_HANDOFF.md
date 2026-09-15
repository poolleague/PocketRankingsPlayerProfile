# Current release handoff

Verified date: 2026-09-15

## Repository state

- Product: Pocket Rankings Player Profile only.
- Working branch: `codex/player-data-privacy`.
- Version: `0.2.0-development`.
- No Production deployment, DNS record, tag, or GitHub Release exists.
- League and Tournament repositories were not changed by this phase.

## Implemented

- Public directory and amateur-oriented player profile layout.
- Stable public UUID plus friendly slug; nullable future Account `PersonId`.
- Many League and Tournament source links per profile, with pending/verified/revoked states.
- Verified-result statistics calculated from active source projections only.
- Player-reported and community-endorsed provenance kept distinct from verified sources.
- Owner, ProfileAdmin, and assigned Player management boundaries.
- Draft/published/hidden lifecycle with required reasons and append-only audit records.
- Strict public Facebook/X URL storage and visitor-initiated embed loading.
- Isolated PostgreSQL schema, Docker network/database/volume, and Development fixtures.
- Complete Account-directed profile erasure, idempotent non-identifying receipts, and keyed rejection of future data for opted-out people.

## Intentionally deferred

- Account authentication and the signed verification handshake that assigns `PersonId`.
- Signed Account erasure delivery, acknowledgement, retries, and operational alerting; the store contract exists but no live receiver is activated.
- League/Tournament producer changes and live event delivery.
- A supported handler that converts accepted inbox payloads into result/achievement rows. The inbox currently deduplicates and stages safely.
- Community endorser workflow, moderation tooling, player photo upload, and owner-provided launch symbol.
- Production infrastructure, secrets, backups, DNS, monitoring, and deployment certification.

## Next safe phase

Define and approve signed Account/source verification and event authentication, then implement producers in their respective repositories with those owners. Do not infer identity from names or copy source databases.

## Validation evidence

- Release build: passed with zero warnings.
- Automated tests: 19 passed, zero failed.
- Known-vulnerability scan: no vulnerable packages reported by configured NuGet sources.
- Browser smoke: directory, public profile, source provenance, and Development access page rendered successfully.
- Security response: CSP and `Referrer-Policy: no-referrer` verified locally.
- Docker Compose rendering: passed with isolated Player Profile resources and loopback binding.
- Docker image/runtime verification: unavailable because this desktop session could not access the Docker engine API; do not represent the container as runtime-validated.
