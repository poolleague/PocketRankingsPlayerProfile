using Microsoft.AspNetCore.Mvc;
using PocketRankingsPlayerProfile.Services;

namespace PocketRankingsPlayerProfile.Controllers;

// Provides a JWT-authenticated machine boundary; it never relies on browser cookies or exposes identity details.
[ApiController]
[Route("internal/privacy")]
public sealed class InternalPrivacyController(PrivacyDirectiveVerifier verifier, IPlayerProfileStore store) : ControllerBase
{
    [HttpPost("player-data-erasure")]
    [IgnoreAntiforgeryToken]
    // Bypasses browser CSRF only because this machine route requires a purpose-bound Account signature.
    public async Task<IActionResult> Erase([FromHeader(Name="Authorization")] string authorization, CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal) || !verifier.TryVerify(authorization[7..], DateTimeOffset.UtcNow, out var directive)) return Unauthorized();
        var result = await store.ErasePlayerDataAsync(directive!, token);
        return Ok(new { requestId=result.RequestId, completed=result.Completed, duplicate=result.Duplicate });
    }
}
