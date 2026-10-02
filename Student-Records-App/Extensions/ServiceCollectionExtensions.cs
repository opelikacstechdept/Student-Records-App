using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using StudentArchive.Web.Data;
using StudentArchive.Web.Services;
using System.Threading.RateLimiting;

namespace StudentArchive.Web.Extensions;

/// <summary>
/// Extension methods that register logical groups of services on
/// <see cref="IServiceCollection"/>. Keeps <c>Program.cs</c> short and
/// readable — each method is named after the concern it configures.
/// </summary>
public static class ServiceCollectionExtensions
{
    // -------------------------------------------------------------------------
    // Authentication
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers Microsoft Entra ID (Azure AD) OpenID Connect authentication
    /// with MFA enforcement via the amr claim check.
    /// </summary>
    public static IServiceCollection AddEntraAuthentication(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(options =>
            {
                config.GetSection("AzureAd").Bind(options);

                // Secondary MFA guard — primary enforcement is Conditional Access
                options.Events.OnTokenValidated = context =>
                {
                    var principal = context.Principal;
                    if (principal != null && !principal.HasCompletedMfa())
                    {
                        context.Fail("Multi-factor authentication is required.");
                    }
                    return Task.CompletedTask;
                };

                // Redirect to friendly page on auth failure
                options.Events.OnAuthenticationFailed = context =>
                {
                    context.Response.Redirect("/Account/MfaRequired");
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
            });

        // Every route requires authentication — opt out, not opt in
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization
                .AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    // -------------------------------------------------------------------------
    // Database
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers the EF Core DbContext against Azure SQL (production).
    /// </summary>
    public static IServiceCollection AddProductionDatabase(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<StudentArchiveDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));

        return services;
    }

    /// <summary>
    /// Registers an in-memory EF Core DbContext for demo / local development.
    /// Data does not persist between application restarts.
    /// </summary>
    public static IServiceCollection AddDemoDatabase(
        this IServiceCollection services)
    {
        services.AddDbContext<StudentArchiveDbContext>(options =>
            options.UseInMemoryDatabase("StudentArchiveDemo"));

        return services;
    }

    // -------------------------------------------------------------------------
    // Azure Storage
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers the Azure Blob Storage client using Managed Identity —
    /// no connection string or account key is used.
    /// </summary>
    public static IServiceCollection AddProductionBlobStorage(
        this IServiceCollection services, IConfiguration config)
    {
        var accountName = config["BlobStorage:AccountName"]
            ?? throw new InvalidOperationException("BlobStorage:AccountName not configured.");

        services.AddSingleton(_ =>
            new BlobServiceClient(
                new Uri($"https://{accountName}.blob.core.windows.net"),
                new DefaultAzureCredential()));

        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        return services;
    }

    /// <summary>
    /// Registers a local filesystem blob storage stub for demo mode.
    /// Files are written to the system temp folder instead of Azure.
    /// </summary>
    public static IServiceCollection AddDemoBlobStorage(
        this IServiceCollection services)
    {
        services.AddScoped<IBlobStorageService, LocalBlobStorageService>();
        return services;
    }

    // -------------------------------------------------------------------------
    // Application services
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers all application-layer services — the same set is used
    /// in both production and demo modes.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IFileValidationService, FileValidationService>();
        services.AddScoped<IBlobPathService, BlobPathService>();
        return services;
    }

    // -------------------------------------------------------------------------
    // Rate limiting
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers per-endpoint rate limiters.
    /// Limits are deliberately lenient for legitimate users but prevent
    /// bulk scraping and brute-force upload attempts.
    /// </summary>
    public static IServiceCollection AddApplicationRateLimiting(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Search: 30 requests per minute — supports normal browsing behaviour
            options.AddFixedWindowLimiter("search", opt =>
            {
                opt.PermitLimit = 30;
                opt.Window      = TimeSpan.FromMinutes(1);
                opt.QueueLimit  = 0;
            });

            // Upload: 10 files per minute — prevents bulk upload flooding
            options.AddFixedWindowLimiter("upload", opt =>
            {
                opt.PermitLimit = 10;
                opt.Window      = TimeSpan.FromMinutes(1);
                opt.QueueLimit  = 0;
            });

            // Download: 60 per minute — generous for normal use, limits scraping
            options.AddFixedWindowLimiter("download", opt =>
            {
                opt.PermitLimit = 60;
                opt.Window      = TimeSpan.FromMinutes(1);
                opt.QueueLimit  = 0;
            });

            // Return 429 Too Many Requests on limit exceeded
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }

    // -------------------------------------------------------------------------
    // Security headers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers a middleware delegate that appends security headers to every response.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.Append("X-Content-Type-Options",  "nosniff");
            headers.Append("X-Frame-Options",          "DENY");
            headers.Append("X-XSS-Protection",         "1; mode=block");
            headers.Append("Referrer-Policy",           "strict-origin-when-cross-origin");
            headers.Append("Permissions-Policy",        "camera=(), microphone=(), geolocation=()");
            headers.Append("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "frame-ancestors 'none';");
            await next();
        });
    }
}
