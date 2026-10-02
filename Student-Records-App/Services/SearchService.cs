using Microsoft.EntityFrameworkCore;
using StudentArchive.Web.Data;
using StudentArchive.Web.ViewModels;

namespace StudentArchive.Web.Services;

/// <summary>
/// Provides filtered, paginated search across student files and metadata.
/// All filters are optional and composed dynamically — only non-null
/// parameters contribute a WHERE clause to the final query.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Executes a search with the given filters and returns a paginated result set.
    /// </summary>
    /// <param name="vm">Search parameters. Any null/empty field is ignored.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of results per page. Defaults to 25.</param>
    Task<PagedResult<SearchResultViewModel>> SearchAsync(
        SearchViewModel vm, int page = 1, int pageSize = 25);

    /// <summary>
    /// Populates dropdown option lists on the search view model.
    /// </summary>
    Task<SearchViewModel> BuildSearchViewModelAsync();
}

/// <summary>
/// EF Core implementation of <see cref="ISearchService"/>.
/// Builds a composable LINQ query and executes a single COUNT + paginated
/// SELECT against the database rather than loading all rows into memory.
/// </summary>
public class SearchService : ISearchService
{
    // -------------------------------------------------------------------------
    // Dependencies
    // -------------------------------------------------------------------------

    private readonly StudentArchiveDbContext _context;
    private readonly ILogger<SearchService> _logger;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public SearchService(StudentArchiveDbContext context, ILogger<SearchService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // Public methods
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<PagedResult<SearchResultViewModel>> SearchAsync(
        SearchViewModel vm, int page = 1, int pageSize = 25)
    {
        // Start with the full join — Student → StudentFile
        var query = _context.StudentFiles
            .Include(f => f.Student)
            .AsQueryable();

        // Apply each filter only when a value was provided
        query = ApplyFilters(query, vm);

        // Count before pagination for accurate page calculation
        var totalCount = await query.CountAsync();

        _logger.LogDebug(
            "Search executed. Filters: StudentNumber={SN}, LastName={LN}, " +
            "DocType={DT}, AcYear={AY}. Total matches: {Count}",
            vm.StudentNumber, vm.LastName, vm.DocumentType, vm.AcademicYear, totalCount);

        // Fetch the requested page — ordered by upload date descending
        var items = await query
            .OrderByDescending(f => f.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new SearchResultViewModel
            {
                FileId          = f.FileId,
                StudentNumber   = f.Student.StudentNumber,
                StudentName     = f.Student.FirstName + " " + f.Student.LastName,
                FileName        = f.FileName,
                DocumentType    = f.DocumentType,
                AcademicYear    = f.AcademicYear,
                FileSizeDisplay = f.FileSizeBytes < 1048576
                    ? $"{f.FileSizeBytes / 1024.0:F1} KB"
                    : $"{f.FileSizeBytes / 1048576.0:F1} MB",
                UploadedAt = f.UploadedAt
            })
            .ToListAsync();

        return new PagedResult<SearchResultViewModel>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<SearchViewModel> BuildSearchViewModelAsync()
    {
        // Distinct document types already in the database — keeps dropdown in sync with real data
        var documentTypes = await _context.StudentFiles
            .Where(f => !string.IsNullOrEmpty(f.DocumentType))
            .Select(f => f.DocumentType)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        var academicYears = await _context.StudentFiles
            .Where(f => !string.IsNullOrEmpty(f.AcademicYear))
            .Select(f => f.AcademicYear)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();

        return new SearchViewModel
        {
            DocumentTypeOptions = documentTypes
                .Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(d, d))
                .ToList(),
            AcademicYearOptions = academicYears
                .Select(y => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(y, y))
                .ToList(),
            StatusOptions = new List<string>
                { "Active", "Inactive", "Graduated", "Withdrawn", "Suspended" }
                .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(s, s))
                .ToList()
        };
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Composes the WHERE clause by chaining optional filter predicates.
    /// Each condition is only appended when the corresponding field is non-empty,
    /// so an empty search returns all records (subject to pagination).
    /// </summary>
    private static IQueryable<Models.StudentFile> ApplyFilters(
        IQueryable<Models.StudentFile> query, SearchViewModel vm)
    {
        // Exact match on student number (unique identifier)
        if (!string.IsNullOrWhiteSpace(vm.StudentNumber))
            query = query.Where(f => f.Student.StudentNumber == vm.StudentNumber.Trim());

        // Prefix match on last name — supports partial entry (e.g. "Smi" matches "Smith")
        if (!string.IsNullOrWhiteSpace(vm.LastName))
            query = query.Where(f =>
                f.Student.LastName.StartsWith(vm.LastName.Trim()));

        // Prefix match on first name
        if (!string.IsNullOrWhiteSpace(vm.FirstName))
            query = query.Where(f =>
                f.Student.FirstName.StartsWith(vm.FirstName.Trim()));

        // Exact match on document type (from dropdown — always a known value)
        if (!string.IsNullOrWhiteSpace(vm.DocumentType))
            query = query.Where(f => f.DocumentType == vm.DocumentType);

        // Exact match on academic year
        if (!string.IsNullOrWhiteSpace(vm.AcademicYear))
            query = query.Where(f => f.AcademicYear == vm.AcademicYear);

        // Filter by student status
        if (!string.IsNullOrWhiteSpace(vm.Status))
            query = query.Where(f => f.Student.Status == vm.Status);

        return query;
    }
}
