using System.Security.Claims;

namespace StudentArchive.Web.Extensions;

/// <summary>
/// Extension methods on <see cref="ClaimsPrincipal"/> that provide
/// strongly-typed, null-safe access to the most commonly needed user
/// identity fields from Entra ID JWT claims.
/// </summary>
/// <remarks>
/// Centralising claim reads here means controllers never contain raw
/// claim string literals, and the mapping only needs to change in one place.
/// </remarks>
public static class ClaimsPrincipalExtensions
{
    // -------------------------------------------------------------------------
    // Identity fields
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the Entra object ID (OID claim) — the stable, unique identifier
    /// for a user across the tenant. Use this as the authoritative user key
    /// in audit logs and foreign keys, not the email address.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst("oid")?.Value
        ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? "unknown";

    /// <summary>
    /// Returns the user's UPN / email address from the preferred_username claim.
    /// Suitable for display and audit logging. Not guaranteed to be unique across
    /// tenants — use <see cref="GetUserId"/> for identity keys.
    /// </summary>
    public static string GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst("preferred_username")?.Value
        ?? user.FindFirst(ClaimTypes.Email)?.Value
        ?? "unknown";

    /// <summary>
    /// Returns the user's display name from the name claim.
    /// Falls back to the email address if no name claim is present.
    /// </summary>
    public static string GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirst("name")?.Value
        ?? user.FindFirst(ClaimTypes.Name)?.Value
        ?? GetEmail(user);

    // -------------------------------------------------------------------------
    // MFA verification
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if the token contains proof that the user completed MFA.
    /// Checks for both standard MFA (<c>mfa</c>) and Windows Hello for Business
    /// (<c>ngcmfa</c>) in the Authentication Methods References (amr) claim.
    /// </summary>
    /// <remarks>
    /// This is a secondary check — the primary enforcement is the Conditional
    /// Access policy in Entra ID which prevents token issuance without MFA.
    /// This check ensures the app still rejects tokens if that policy is
    /// ever misconfigured.
    /// </remarks>
    public static bool HasCompletedMfa(this ClaimsPrincipal user)
    {
        // Entra issues one amr claim per method (e.g. "pwd" and "mfa"),
        // so check them all rather than only the first
        return user.FindAll("amr").Any(c =>
            c.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase)
            || c.Value.Equals("ngcmfa", StringComparison.OrdinalIgnoreCase));
    }

    // -------------------------------------------------------------------------
    // Role helpers
    // -------------------------------------------------------------------------

    /// <summary>Returns true if the user holds the Admin role.</summary>
    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole("Admin");

    /// <summary>Returns true if the user can upload files (Admin or Uploader).</summary>
    public static bool CanUpload(this ClaimsPrincipal user) =>
        user.IsInRole("Admin") || user.IsInRole("Uploader");
}
