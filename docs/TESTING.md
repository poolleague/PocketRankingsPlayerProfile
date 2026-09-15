# Testing

Run:

```powershell
dotnet build PocketRankingsPlayerProfile.slnx -c Release
dotnet test PocketRankingsPlayerProfile.slnx -c Release
dotnet list src/PocketRankingsPlayerProfile/PocketRankingsPlayerProfile.csproj package --vulnerable --include-transitive
docker compose --env-file .env.example config
```

Current automated coverage checks verified-stat filtering, zero totals, unsafe Facebook/X URLs, tracking removal, slug safety, public draft exclusion, forced player-reported provenance, pending-link isolation, event idempotency, complete profile erasure, repeated-request idempotency, future-event privacy suppression, and required schema safeguards. The 0.2.0 suite passes 19 tests.

Before launch, add PostgreSQL integration tests, Account authentication tests, event signature/replay tests, authorization negatives for every mutation, CSRF and security-header browser checks, keyboard/mobile/responsive checks, accessibility scan, Lighthouse, backup/restore validation, load profiles, and the approved exact-candidate release gate.
