using System.Security.Cryptography;
using System.Text;

namespace PocketRankingsPlayerProfile.Services;

// Produces a stable, one-way, product-local suppression token without retaining Account PersonId.
public sealed class PrivacySuppressionHasher
{
    private readonly byte[] key;

    public PrivacySuppressionHasher(string keyValue)
    {
        if (string.IsNullOrWhiteSpace(keyValue) || keyValue.Length < 32)
            throw new InvalidOperationException("PlayerProfile privacy suppression key must contain at least 32 characters.");
        key = Encoding.UTF8.GetBytes(keyValue);
    }

    // Uses the canonical GUID byte representation so all callers hash the same identity consistently.
    public string Hash(Guid personId) => Convert.ToHexString(HMACSHA256.HashData(key, personId.ToByteArray())).ToLowerInvariant();
}
