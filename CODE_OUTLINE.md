# Code outline

- `Controllers/PlayersController.cs`: published directory and stable public profile routes.
- `Controllers/ManageProfilesController.cs`: authorized narrative, accomplishment, social, source-request, and lifecycle actions.
- `Controllers/DevelopmentAccessController.cs`: Development-only fictional role sessions.
- `Models/ProfileModels.cs`: public identity, source, result, achievement, social, and form contracts.
- `Services/PlayerProfileStore.cs`: storage boundary and deterministic Development implementation.
- `Services/PostgresPlayerProfileStore.cs`: PostgreSQL persistence and migration initialization.
- `Services/ProfileStatisticsCalculator.cs`: verified-result-only derived totals.
- `Services/SocialLinkPolicy.cs`: strict public-provider URL allowlist and normalization.
- `Views/Players`: public amateur player experience.
- `Views/ManageProfiles`: role-limited profile administration and history.
- `Database/001_initial_schema.sql`: versioned Player Profile-owned PostgreSQL objects.
