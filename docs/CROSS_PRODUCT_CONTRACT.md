# Cross-product contract

Player Profile is a consumer, never a reader of League or Tournament databases.

## Envelope version 1

Each delivery carries an immutable `eventId`, `eventType`, `schemaVersion`, `sourceProduct`, opaque product-local `tenantKey`, opaque `sourceEntityId`, optional Account-owned `personId`, `occurredAt`, and JSON payload.

The inbox uses `eventId` for idempotency. It associates an event only through an exact `personId` or an already verified `(sourceProduct, tenantKey, sourceEntityId)` link. Unmatched events remain `staged_unmatched`; names are ignored. Unknown or unsupported payloads must fail closed and remain inspectable.

## Planned event families

- `player.result.upsert` version 1
- `player.result.retracted` version 1
- `player.achievement.upsert` version 1
- `player.achievement.retracted` version 1

The current foundation persists the envelope and determines exact identity association, but intentionally does not project payload fields. Before live integration, define canonical JSON schemas, signatures, key rotation, replay window, producer authorization, retry/dead-letter policy, and privacy-safe logging. That requires separate approval and changes owned by each producer repository.
