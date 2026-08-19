# AGENTS.md — PocketRankingsPlayerProfile

This repo inherits every rule in `PLATFORM_AGENTS.md`
(`PocketRankingsPlatform` repo). This file covers only what's specific to
Player Profile.

## Scope of this product

- Player-facing profile data: stats, history, cross-league identity — in
  its own database, isolated from League and Tournament.
- References League player records via `IntegrationEntityIdentifier` /
  `IntegrationEntityLink` (already defined in League's
  `Models/LeagueModels.cs`), never by direct League database access.

## Explicitly out of scope here

- Owning identity or entitlement state — that's Account's job.
- Tournament bracket/match logic — that's Tournament's job.

## Status

Scaffolding only. No controllers/services/models implemented yet — real
implementation starts next session, per owner approval, per
PLATFORM_AGENTS.md Section 8 (Approval Boundaries).
