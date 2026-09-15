# AGENTS.md — PocketRankingsPlayerProfile

This repo inherits every rule in `PLATFORM_AGENTS.md` (`PocketRankingsPlatform` repo). This file covers only what is specific to Player Profile.

## Scope of this product

- Player-facing profile data: stats, history, and cross-product identity in its own database, isolated from League and Tournament.
- References local League and Tournament player identities through explicit source links and versioned events, never by direct database access or name matching.

## Explicitly out of scope here

- Owning identity or entitlement state — that is Account's job.
- Tournament bracket/match logic — that is Tournament's job.
- League scheduling, scoring, standings, or administration — that is League's job.

## Current implementation

The first runtime foundation is implemented. Read `docs/CURRENT_RELEASE_HANDOFF.md` before changing code or proposing deployment.

## Player data privacy

An authenticated Account opt-out must delete the complete profile aggregate, including biography, social links, achievements, statistics/results, source links, lifecycle history, and profile audit tied to that person. Retain only a keyed one-way suppression and a non-identifying request receipt. Future events for the opted-out `PersonId` must be rejected as `suppressed_privacy`; a later opt-in starts from future data only. Live signed Account delivery is not yet activated, so do not claim an Account request completed from this store contract alone.
