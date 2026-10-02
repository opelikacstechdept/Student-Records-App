using System.ComponentModel.DataAnnotations;

namespace StudentArchive.Web.ViewModels;

public class AddStudentViewModel
{
    [Required(ErrorMessage = "Student number is required")]
    [RegularExpression(@"^\d{6,10}$", ErrorMessage = "Student number must be 6-10 digits")]
    [Display(Name = "Student Number")]
    public string StudentNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required")]
    [StringLength(100, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z\s\-']+$", ErrorMessage = "First name contains invalid characters")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(100, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z\s\-']+$", ErrorMessage = "Last name contains invalid characters")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Status is required")]
    [Display(Name = "Status")]
    public string Status { get; set; } = string.Empty;

    public static List<string> StatusOptions => new()
    {
        "Active", "Inactive", "Graduated", "Withdrawn", "Suspended"
    };
}

public class StudentDetailViewModel
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<FileListItemViewModel> Files { get; set; } = new();
}

public class FileListItemViewModel
{
    public int FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string FileSizeDisplay { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string UploadedByUserName { get; set; } = string.Empty;
}
