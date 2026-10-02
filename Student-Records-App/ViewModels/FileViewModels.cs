using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace StudentArchive.Web.ViewModels;

public class FileUploadViewModel
{
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a file")]
    [Display(Name = "File")]
    public IFormFile? File { get; set; }

    [Required(ErrorMessage = "Document type is required")]
    [Display(Name = "Document Type")]
    public string DocumentType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Academic year is required")]
    [Display(Name = "Academic Year")]
    public string AcademicYear { get; set; } = string.Empty;

    [Display(Name = "Semester")]
    public string? Semester { get; set; }

    [Display(Name = "Department")]
    public string? Department { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public List<SelectListItem> DocumentTypeOptions { get; set; } = new();
    public List<SelectListItem> AcademicYearOptions { get; set; } = new();
    public List<SelectListItem> SemesterOptions { get; set; } = new();
}

public class SearchViewModel
{
    [Display(Name = "Student Number")]
    [RegularExpression(@"^\d{0,10}$", ErrorMessage = "Student number must be numeric")]
    public string? StudentNumber { get; set; }

    [Display(Name = "First Name")]
    [StringLength(100)]
    public string? FirstName { get; set; }

    [Display(Name = "Last Name")]
    [StringLength(100)]
    public string? LastName { get; set; }

    [Display(Name = "Document Type")]
    public string? DocumentType { get; set; }

    [Display(Name = "Academic Year")]
    public string? AcademicYear { get; set; }

    [Display(Name = "Status")]
    public string? Status { get; set; }

    public List<SelectListItem> DocumentTypeOptions { get; set; } = new();
    public List<SelectListItem> AcademicYearOptions { get; set; } = new();
    public List<SelectListItem> StatusOptions { get; set; } = new();

    public PagedResult<SearchResultViewModel>? Results { get; set; }
    public bool HasSearched { get; set; }
}

public class SearchResultViewModel
{
    public int FileId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string FileSizeDisplay { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
