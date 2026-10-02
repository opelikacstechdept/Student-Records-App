using System.ComponentModel.DataAnnotations;

namespace StudentArchive.Web.Models;

public class StudentFile
{
    public int FileId { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string BlobPath { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string BlobUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [MaxLength(20)]
    public string AcademicYear { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public string UploadedByUserId { get; set; } = string.Empty;

    public string UploadedByUserName { get; set; } = string.Empty;

    public ICollection<FileMetadataItem> Metadata { get; set; } = new List<FileMetadataItem>();

    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1048576 => $"{FileSizeBytes / 1024.0:F1} KB",
        _ => $"{FileSizeBytes / 1048576.0:F1} MB"
    };
}
