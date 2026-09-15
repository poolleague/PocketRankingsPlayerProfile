namespace PocketRankingsPlayerProfile.Tests;

public sealed class SchemaContractTests
{
    [Fact]
    public void PrivacyMigrationStoresNoRawPersonIdentifier()
    {
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PocketRankingsPlayerProfile", "Database", "002_player_data_erasure.sql"));
        Assert.Contains("profile.privacy_suppressions", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("person_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("append-only", sql, StringComparison.OrdinalIgnoreCase);
    }
    // Keeps the checked-in migration aligned with identity, replay, and append-only history guarantees.
    [Fact]
    public void InitialSchema_ContainsRequiredProtection()
    {
        var root = FindRepositoryRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "PocketRankingsPlayerProfile", "Database", "001_initial_schema.sql"));
        Assert.Contains("person_id uuid UNIQUE", sql);
        Assert.Contains("ux_active_source_identity", sql);
        Assert.Contains("integration_inbox", sql);
        Assert.Contains("profile_audit_append_only", sql);
        Assert.Contains("staged_unmatched", sql);
    }

    // Locates the repository without relying on a developer-specific absolute path.
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PocketRankingsPlayerProfile.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
