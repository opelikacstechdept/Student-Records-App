using System.ComponentModel.DataAnnotations;

namespace StudentArchive.Web.Models;

public class FileMetadataItem
{
    public int MetadataId { get; set; }

    public int FileId { get; set; }
    public StudentFile StudentFile { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Value { get; set; } = string.Empty;
}

public class MetadataKey
{
    public int MetadataKeyId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
