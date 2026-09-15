# Pocket Rankings — Player Profile

An amateur-first home for pool-player biographies, informal accomplishments, and verified results projected from separately operated Pocket Rankings League and Tournament products.

## Current shape

- Public player directory and stable `/players/{slug}` profile pages.
- Durable public `ProfileId`, future Account-owned `PersonId`, and many local source links per player.
- Clear provenance: League verified, Tournament verified, Community endorsed, or Player reported.
- Role-limited profile management, publication lifecycle, and append-only history.
- Optional public Facebook/X links with visitor-initiated embeds; no social credentials or copied feeds.
- PostgreSQL schema and isolated Docker stack; deterministic in-memory fixtures for local development.
- Irreversible Account-directed profile erasure with keyed future-data suppression and non-identifying receipts.

## Run locally

```powershell
dotnet run --project src/PocketRankingsPlayerProfile
```

Development uses fictional in-memory data when no database connection is configured. To exercise PostgreSQL, copy `.env.example` to `.env`, choose a non-production password, and run `docker compose up --build`.

## Important paths

- `src/PocketRankingsPlayerProfile/Database/*.sql` — ordered idempotent PostgreSQL migrations
- `src/PocketRankingsPlayerProfile/Services/PlayerProfileStore.cs` — store contract and fictional development implementation
- `src/PocketRankingsPlayerProfile/Services/PostgresPlayerProfileStore.cs` — isolated persistent implementation
- `docs/CURRENT_RELEASE_HANDOFF.md` — verified current state and remaining launch work
- `docs/CROSS_PRODUCT_CONTRACT.md` — future League/Tournament event boundary
- `docs/AUTHORIZATION_AND_HISTORY.md` — permissions, identity, and retention
- `docs/THIRD_PARTY_AND_ORIGINALITY.md` — licensing and design-risk record
- `docs/PLAYER_DATA_PRIVACY.md` — irreversible opt-out behavior, suppression, and restore rules

No Production deployment, DNS, Account integration, or League/Tournament producer change is included in this foundation.
