# Deployment

## Local container validation

Copy `.env.example` to `.env`, set a local-only password, then run `docker compose config` and `docker compose up --build`. The stack binds the web service to loopback by default and uses uniquely named Player Profile services, network, volume, database, and credentials.

## Production prerequisites

Production must supply `ConnectionStrings__PlayerProfile`; the application fails startup without it. Before any Production deployment, separately approve and document the host, TLS/reverse proxy, random secrets, restricted configuration permissions, PostgreSQL backup/restore drill, persistent storage, migrations, monitoring, alerting, log retention, Account authentication, rate limits, abuse response, public hostname, and rollback image.

No DNS or Production action is authorized by this document. Do not share League or Tournament databases, networks, queues, credentials, or backups.
