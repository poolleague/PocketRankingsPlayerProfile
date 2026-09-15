# Changelog

## 0.2.0-development — 2026-09-15

- Added transactional deletion of the complete player-profile aggregate for irreversible Account opt-out.
- Added keyed, one-way future-data suppression without retaining raw Account identity.
- Added non-identifying append-only erasure receipts, ordered migration startup, tests, and synchronized deployment/restore documentation.
- Live signed Account delivery and acknowledgements remain separately gated and are not represented as active.

## 0.1.0-development — 2026-09-14

- Added an amateur-first public player directory and player profile.
- Added provenance-separated achievements and verified source statistics.
- Added durable profile identity with many League/Tournament local-record links.
- Added role-limited management, publication lifecycle, and append-only audit history.
- Added public Facebook/X links with visitor-initiated third-party loading.
- Added isolated PostgreSQL schema, container configuration, tests, and launch documentation.
