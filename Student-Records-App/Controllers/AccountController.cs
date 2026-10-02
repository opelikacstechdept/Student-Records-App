using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace StudentArchive.Web.Controllers;

/// <summary>
/// Handles authentication pages.
/// In production, sign-in/out are delegated to Entra ID via the
/// MicrosoftIdentity area controller that ships with Microsoft.Identity.Web.UI.
/// In demo mode, this controller provides a simple local login that
/// issues a cookie with a pre-configured demo identity.
/// </summary>
public class AccountController : Controller
{
    private readonly IWebHostEnvironment _env;

    public AccountController(IWebHostEnvironment env)
    {
        _env = env;
    }

    /// <summary>
    /// Mirrors the isDemoMode check in Program.cs. The demo actions return 404
    /// in production so the role picker can never be reached there.
    /// </summary>
    private bool IsDemoMode =>
        _env.IsEnvironment("Demo") || _env.IsDevelopment();

    // -------------------------------------------------------------------------
    // Demo login actions (demo / development mode only)
    // -------------------------------------------------------------------------

    /// <summary>
    /// GET /Account/DemoLogin
    /// Renders the demo role-picker login page.
    /// Only reachable when the app is running in Demo or Development mode.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult DemoLogin(string? returnUrl = null)
    {
        if (!IsDemoMode) return NotFound();

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    /// <summary>
    /// POST /Account/DemoLogin
    /// Issues a cookie for one of the three demo roles.
    /// Roles map directly to the same role names used in production Entra claims.
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DemoLogin(string role, string? returnUrl = null)
    {
        if (!IsDemoMode) return NotFound();

        // Only accept the three known demo roles
        var validRoles = new[] { "Admin", "Uploader", "Viewer" };
        if (!validRoles.Contains(role))
        {
            ModelState.AddModelError(string.Empty, "Please select a role.");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // Build a claims identity that mirrors what Entra would provide
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"demo-{role.ToLower()}"),
            new("oid",                     $"demo-{role.ToLower()}"),
            new("name",                    $"Demo {role}"),
            new("preferred_username",      $"demo.{role.ToLower()}@studentarchive.demo"),
            new(ClaimTypes.Role,           role),
            new("amr",                     "mfa"),   // Simulate completed MFA
        };

        var identity  = new ClaimsIdentity(claims, "DemoAuth");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("DemoAuth", principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(8)
            });

        return LocalRedirect(returnUrl ?? "/");
    }

    /// <summary>
    /// GET /Account/DemoLogout
    /// Signs the demo user out and returns to the login page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DemoLogout()
    {
        if (!IsDemoMode) return NotFound();

        await HttpContext.SignOutAsync("DemoAuth");
        return RedirectToAction(nameof(DemoLogin));
    }

    // -------------------------------------------------------------------------
    // Shared pages (both modes)
    // -------------------------------------------------------------------------

    /// <summary>
    /// GET /Account/MfaRequired
    /// Shown when a token is presented without MFA proof.
    /// Provides a friendly explanation and links to IT support.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult MfaRequired() => View();

    /// <summary>
    /// GET /Account/AccessDenied
    /// Shown when an authenticated user attempts to access a resource
    /// their role does not permit.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();
}
