using Microsoft.AspNetCore.Mvc;

namespace PocketRankingsPlayerProfile.Controllers;

// Hosts durable informational and error routes outside the player directory.
public sealed class HomeController : Controller
{
    public IActionResult About() => View();
    public IActionResult Privacy() => View();
    public IActionResult Help() => View();
    public IActionResult Error() => View();
}
