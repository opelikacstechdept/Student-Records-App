using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.UI;
using Serilog;
using StudentArchive.Web.Data;
using StudentArchive.Web.Extensions;

// =============================================================================
// Bootstrap Serilog early so startup errors are captured
// =============================================================================

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Student Archive application");

    var builder = WebApplication.CreateBuilder(args);

    // -------------------------------------------------------------------------
    // Determine run mode
    // -------------------------------------------------------------------------

    // Set ASPNETCORE_ENVIRONMENT=Demo in Visual Studio launch profile
    // to activate demo mode. Production uses Release configuration.
    var isDemoMode = builder.Environment.IsEnvironment("Demo")
                  || builder.Environment.IsDevelopment();

    Log.Information("Run mode: {Mode}", isDemoMode ? "DEMO" : "PRODUCTION");

    // -------------------------------------------------------------------------
    // Logging
    // -------------------------------------------------------------------------

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .WriteTo.Console();

        // In production, also write to Azure Blob Storage audit log container
        if (!isDemoMode)
        {
            configuration.WriteTo.AzureBlobStorage(
                connectionString: context.Configuration["BlobStorage:LogConnectionString"],
                storageContainerName: "application-logs",
                storageFileName: $"logs/{DateTime.UtcNow:yyyy/MM/dd}/app.log");
        }
    });

    // -------------------------------------------------------------------------
    // MVC
    // -------------------------------------------------------------------------

    var mvc = builder.Services.AddControllersWithViews();
    builder.Services.AddRazorPages();

    // Production: adds the MicrosoftIdentity area (Account/SignIn, Account/SignOut)
    // that _Layout and MfaRequired link to
    if (!isDemoMode)
        mvc.AddMicrosoftIdentityUI();

    // -------------------------------------------------------------------------
    // Authentication — skipped in demo mode, uses a fake identity instead
    // -------------------------------------------------------------------------

    if (isDemoMode)
    {
        // Demo mode: cookie-based auth with a pre-seeded demo user
        // No Entra registration required — works fully offline in Visual Studio
        builder.Services.AddAuthentication("DemoAuth")
            .AddCookie("DemoAuth", options =>
            {
                options.LoginPath  = "/Account/DemoLogin";
                options.LogoutPath = "/Account/DemoLogout";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization
                .AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
    }
    else
    {
        builder.Services.AddEntraAuthentication(builder.Configuration);
    }

    // -------------------------------------------------------------------------
    // Database
    // -------------------------------------------------------------------------

    if (isDemoMode)
        builder.Services.AddDemoDatabase();
    else
        builder.Services.AddProductionDatabase(builder.Configuration);

    // -------------------------------------------------------------------------
    // Blob storage
    // -------------------------------------------------------------------------

    if (isDemoMode)
        builder.Services.AddDemoBlobStorage();
    else
        builder.Services.AddProductionBlobStorage(builder.Configuration);

    // -------------------------------------------------------------------------
    // Application services — same in all modes
    // -------------------------------------------------------------------------

    builder.Services
        .AddApplicationServices()
        .AddApplicationRateLimiting();

    // Allow large file uploads (50 MB)
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 52_428_800;
    });

    // -------------------------------------------------------------------------
    // Build the app
    // -------------------------------------------------------------------------

    var app = builder.Build();

    // -------------------------------------------------------------------------
    // Middleware pipeline — ORDER MATTERS
    // -------------------------------------------------------------------------

    if (!app.Environment.IsDevelopment() && !isDemoMode)
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    else
    {
        // Show detailed errors in demo / development
        app.UseDeveloperExceptionPage();
    }

    app.UseHttpsRedirection();

    // Security headers on every response
    app.UseSecurityHeaders();

    app.UseStaticFiles();
    app.UseRouting();
    app.UseRateLimiter();

    // Forward headers from IIS — required for correct redirect URIs
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapRazorPages();

    // -------------------------------------------------------------------------
    // Database initialisation
    // -------------------------------------------------------------------------

    await InitialiseDatabaseAsync(app, isDemoMode);

    // -------------------------------------------------------------------------
    // Run
    // -------------------------------------------------------------------------

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// =============================================================================
// Helpers
// =============================================================================

/// <summary>
/// Runs EF Core migrations (production) or seeds demo data (demo mode).
/// Called once at startup.
/// </summary>
static async Task InitialiseDatabaseAsync(WebApplication app, bool isDemoMode)
{
    using var scope = app.Services.CreateScope();
    var db     = scope.ServiceProvider.GetRequiredService<StudentArchiveDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (isDemoMode)
    {
        // In-memory database — ensure schema is created and seed demo data
        await db.Database.EnsureCreatedAsync();
        await DemoDataSeeder.SeedAsync(db, logger);
        logger.LogInformation("Demo database initialised with seed data");
    }
    else
    {
        // Production — apply any pending migrations at startup
        await db.Database.MigrateAsync();
        logger.LogInformation("Production database migrations applied");
    }
}
