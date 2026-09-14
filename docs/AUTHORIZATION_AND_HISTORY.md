# Authorization, identity, and history

## Roles

| Role | Profile details | Reported achievements | Social links | Link request | Publication | All profiles/history |
|---|---:|---:|---:|---:|---:|---:|
| Owner | Yes | Yes | Yes | Yes | Yes | Yes |
| ProfileAdmin | Yes | Yes | Yes | Yes | Yes | Yes |
| Player | Assigned only | Assigned only | Assigned only | Assigned only | No | No |
| Public visitor | Read published | Read | Follow/load | No | No | No |

Development role selection returns 404 outside Development. Production requires PostgreSQL; the future Account service must replace local role selection as the identity authority.

## Identity

- `profile_id` is the internal primary key.
- `public_id` is the durable public Player Profile identity.
- `person_id` is nullable until a future Account-owned verification process binds the person.
- `profile_source_links` permits many local League and Tournament identities to point to one profile.
- Names, email addresses, and fuzzy matching never prove identity.
- A requested source link remains pending and cannot contribute verified data.

## Retention and correction

Profiles and audit history are retained until an approved policy defines deletion periods. Audit records are append-only at the database layer. Official corrections arrive as later source events or retractions; profile managers cannot edit official results. Backups and legal retention periods remain deployment decisions requiring owner approval.
