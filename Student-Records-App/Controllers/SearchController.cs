using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudentArchive.Web.Extensions;
using StudentArchive.Web.Models;
using StudentArchive.Web.Services;
using StudentArchive.Web.ViewModels;

namespace StudentArchive.Web.Controllers;

/// <summary>
/// Provides the multi-criteria file search page.
/// Available to all authenticated roles — results are the same regardless
/// of role; download access is enforced separately in <see cref="FileController"/>.
/// </summary>
[Authorize(Roles = "Admin,Uploader,Viewer")]
public class SearchController : Controller
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    /// <summary>Number of results shown per search page.</summary>
    private const int PageSize = 25;

    // -------------------------------------------------------------------------
    // Dependencies
    // -------------------------------------------------------------------------

    private readonly ISearchService _searchService;
    private readonly IAuditService _auditService;
    private readonly ILogger<SearchController> _logger;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public SearchController(
        ISearchService searchService,
        IAuditService auditService,
        ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _auditService  = auditService;
        _logger        = logger;
    }

    // -------------------------------------------------------------------------
    // Actions
    // -------------------------------------------------------------------------

    /// <summary>
    /// GET /Search
    /// Renders the search form with all dropdown options populated.
    /// No results are shown until the user submits.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vm = await _searchService.BuildSearchViewModelAsync();
        return View(vm);
    }

    /// <summary>
    /// POST /Search
    /// Executes the search with the provided filters and returns paginated results.
    /// Rate-limited to 30 requests per minute to prevent bulk scraping.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> Index(SearchViewModel vm, int page = 1)
    {
        // Rebuild dropdown options — they are not round-tripped through the form
        var baseVm = await _searchService.BuildSearchViewModelAsync();
        vm.DocumentTypeOptions = baseVm.DocumentTypeOptions;
        vm.AcademicYearOptions = baseVm.AcademicYearOptions;
        vm.StatusOptions       = baseVm.StatusOptions;

        if (!ModelState.IsValid)
            return View(vm);

        // Clamp page to a valid range
        page = Math.Max(1, page);

        vm.Results    = await _searchService.SearchAsync(vm, page, PageSize);
        vm.HasSearched = true;

        await _auditService.LogAsync(
            userId:     User.GetUserId(),
            userEmail:  User.GetEmail(),
            action:     AuditActions.SearchPerformed,
            entityType: "Search",
            entityId:   string.Empty,
            ipAddress:  HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
            details: new
            {
                vm.StudentNumber,
                vm.FirstName,
                vm.LastName,
                vm.DocumentType,
                vm.AcademicYear,
                vm.Status,
                ResultCount = vm.Results.TotalCount,
                page
            });

        _logger.LogDebug(
            "Search by {UserId}: {Count} results for filters {@Filters}",
            User.GetUserId(), vm.Results.TotalCount,
            new { vm.StudentNumber, vm.LastName, vm.DocumentType });

        return View(vm);
    }

    /// <summary>
    /// GET /Search/Page
    /// Handles pagination requests from the search results view.
    /// Accepts the same filter parameters as the POST action so the
    /// user can page through results without re-submitting the form.
    /// </summary>
    [HttpGet]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> Page(
        string? studentNumber, string? firstName, string? lastName,
        string? documentType, string? academicYear, string? status,
        int page = 1)
    {
        var vm = new SearchViewModel
        {
            StudentNumber = studentNumber,
            FirstName     = firstName,
            LastName      = lastName,
            DocumentType  = documentType,
            AcademicYear  = academicYear,
            Status        = status
        };

        var baseVm = await _searchService.BuildSearchViewModelAsync();
        vm.DocumentTypeOptions = baseVm.DocumentTypeOptions;
        vm.AcademicYearOptions = baseVm.AcademicYearOptions;
        vm.StatusOptions       = baseVm.StatusOptions;

        vm.Results    = await _searchService.SearchAsync(vm, Math.Max(1, page), PageSize);
        vm.HasSearched = true;

        return View(nameof(Index), vm);
    }
}
