using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace PocketRankingsPlayerProfile.Controllers;

// Supplies fictional local identities so authorization can be evaluated without Production credentials.
public sealed class DevelopmentAccessController(IWebHostEnvironment environment) : Controller
{
    // Signs in one bounded demo role and rejects use outside Development.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SignInAs(string role, string? returnUrl)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var allowed = new[] { "Owner", "ProfileAdmin", "Player" };
        if (!allowed.Contains(role, StringComparer.Ordinal)) return BadRequest();
        var claims = new List<Claim> { new(ClaimTypes.Name, $"Demo {role}"), new(ClaimTypes.Role, role) };
        if (role == "Player") claims.Add(new("profile_id", "41000000-0000-0000-0000-000000000001"));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/manage/profiles");
    }

    // Shows the role chooser only in Development to avoid shipping a credential bypass.
    [HttpGet("/development/access")]
    public IActionResult Access(string? returnUrl) => environment.IsDevelopment() ? View(model: returnUrl) : NotFound();

    [HttpGet("/development/access-denied")]
    public IActionResult AccessDenied() => View();

    // Ends only the local application session and does not affect future Account identity state.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutCurrent()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction("Index", "Players");
    }
}
