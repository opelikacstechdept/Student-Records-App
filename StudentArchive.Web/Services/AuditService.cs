using StudentArchive.Web.Data;
using StudentArchive.Web.Models;
using System.Text.Json;

namespace StudentArchive.Web.Services;

public interface IAuditService
{
    Task LogAsync(string userId, string userEmail, string action,
        string entityType = "", string entityId = "",
        string ipAddress = "", object? details = null);
}

public class AuditService : IAuditService
{
    private readonly StudentArchiveDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(StudentArchiveDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(string userId, string userEmail, string action,
        string entityType = "", string entityId = "",
        string ipAddress = "", object? details = null)
    {
        try
        {
            var log = new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow,
                Details = details != null ? JsonSerializer.Serialize(details) : null
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never let audit logging break the main flow
            _logger.LogError(ex, "Failed to write audit log for action {Action}", action);
        }
    }
}
