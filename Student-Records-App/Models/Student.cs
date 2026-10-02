using System.ComponentModel.DataAnnotations;

namespace StudentArchive.Web.Models;

public class Student
{
    public int StudentId { get; set; }

    [Required]
    [MaxLength(20)]
    public string StudentNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string CreatedByUserId { get; set; } = string.Empty;

    public ICollection<StudentFile> Files { get; set; } = new List<StudentFile>();

    public string FullName => $"{FirstName} {LastName}";
}
