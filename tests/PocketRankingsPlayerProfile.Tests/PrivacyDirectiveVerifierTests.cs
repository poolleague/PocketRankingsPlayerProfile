using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PocketRankingsPlayerProfile.Services;

namespace PocketRankingsPlayerProfile.Tests;

public sealed class PrivacyDirectiveVerifierTests
{
    [Fact]
    public void AcceptsOnlyCorrectlyBoundCurrentAccountSignature()
    {
        using var rsa=RSA.Create(2048); var now=DateTimeOffset.UtcNow;
        using var verifier=CreateVerifier(rsa,"profile-main");
        Assert.True(verifier.TryVerify(Sign(rsa,"pocketrankings-player_profile","profile-main",now),now,out var directive));
        Assert.NotNull(directive);
        Assert.False(verifier.TryVerify(Sign(rsa,"pocketrankings-tournament","profile-main",now),now,out _));
        Assert.False(verifier.TryVerify(Sign(rsa,"pocketrankings-player_profile","another-installation",now),now,out _));
        Assert.False(verifier.TryVerify(Sign(rsa,"pocketrankings-player_profile","profile-main",now.AddMinutes(-5)),now,out _));
    }

    [Fact]
    public void DisabledReceiverFailsClosed()
    {
        using var rsa = RSA.Create(2048);
        using var verifier = new PrivacyDirectiveVerifier(new ConfigurationBuilder().Build());
        Assert.False(verifier.TryVerify(Sign(rsa, "pocketrankings-player_profile", "profile-main", DateTimeOffset.UtcNow), DateTimeOffset.UtcNow, out _));
    }

    private static PrivacyDirectiveVerifier CreateVerifier(RSA rsa,string installation)=>new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"PrivacyReceiver:Enabled","true"},{"AccountIdentity:PublicKeyPem",rsa.ExportSubjectPublicKeyInfoPem()},{"Installation:Key",installation}}).Build());
    private static string Sign(RSA rsa,string audience,string installation,DateTimeOffset now)
    {
        var header=JsonSerializer.Serialize(new{alg="RS256",typ="JWT",kid="account-privacy-1"});
        var payload=JsonSerializer.Serialize(new{iss="pocketrankings-account",aud=audience,action="person.player_data_erasure_requested.v1",request_id=Guid.NewGuid(),person_id=Guid.NewGuid(),installation_key=installation,iat=now.ToUnixTimeSeconds(),exp=now.AddMinutes(2).ToUnixTimeSeconds(),jti=Guid.NewGuid()});
        var unsigned=$"{Encode(Encoding.UTF8.GetBytes(header))}.{Encode(Encoding.UTF8.GetBytes(payload))}";
        return $"{unsigned}.{Encode(rsa.SignData(Encoding.UTF8.GetBytes(unsigned),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))}";
    }
    private static string Encode(byte[] value)=>Convert.ToBase64String(value).TrimEnd('=').Replace('+','-').Replace('/','_');
}
