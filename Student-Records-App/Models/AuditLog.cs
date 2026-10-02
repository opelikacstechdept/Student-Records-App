using System.ComponentModel.DataAnnotations;

namespace StudentArchive.Web.Models;

public class AuditLog
{
    public int AuditLogId { get; set; }

    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string UserEmail { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    [MaxLength(100)]
    public string EntityId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string IpAddress { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? Details { get; set; }
}

public static class AuditActions
{
    public const string StudentCreated = "StudentCreated";
    public const string FileUploaded = "FileUploaded";
    public const string FileDownloaded = "FileDownloaded";
    public const string FileViewed = "FileViewed";
    public const string SearchPerformed = "SearchPerformed";
    public const string LoginSuccess = "LoginSuccess";
    public const string LoginFailed = "LoginFailed";
    public const string MfaDenied = "MfaDenied";
    public const string UnauthorizedAccess = "UnauthorizedAccess";
}
