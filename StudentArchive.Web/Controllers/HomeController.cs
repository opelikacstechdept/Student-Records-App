using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentArchive.Web.ViewModels;

namespace StudentArchive.Web.Controllers;

/// <summary>
/// Default route target and the production error handler.
/// The archive has no dashboard, so the home page forwards to search,
/// which every role (Admin, Uploader, Viewer) can use.
/// </summary>
public class HomeController : Controller
{
    // -------------------------------------------------------------------------
    // Actions
    // -------------------------------------------------------------------------

    /// <summary>
    /// GET /
    /// Sends signed-in users to the search page. Anonymous users never reach
    /// this action — the fallback policy challenges them first.
    /// </summary>
    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Search");

    /// <summary>
    /// GET /Home/Error
    /// Target of <c>UseExceptionHandler("/Home/Error")</c> in production.
    /// Shows a request ID only — never exception details.
    /// </summary>
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}
