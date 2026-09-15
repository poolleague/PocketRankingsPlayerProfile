# CLAUDE.md — PocketRankingsPlayerProfile

Read `AGENTS.md` and the inherited `PocketRankingsPlatform/PLATFORM_AGENTS.md` in full before working. Follow the same repository scope, approval boundaries, identity rules, and current handoff as every other agent. This repository owns Player Profile only; do not edit League or Tournament from this checkout.

## Player data privacy

An authenticated Account opt-out must delete the complete profile aggregate, including biography, social links, achievements, statistics/results, source links, lifecycle history, and profile audit tied to that person. Retain only a keyed one-way suppression and a non-identifying request receipt. Future events for the opted-out `PersonId` must be rejected as `suppressed_privacy`; a later opt-in starts from future data only. The signed receiver is disabled by default and must remain fail-closed until its exact private destination, installation binding, key distribution/rotation, dispatch, acknowledgements, retries, alerting, and activation are separately approved. Never claim an Account request completed from the receiver or store alone.
