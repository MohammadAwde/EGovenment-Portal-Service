namespace SmartEGov.Application.Services;

public interface IAuditLogService
{
    Task LogAsync(string? userId, string action, string entityName, string? entityId, string? oldValues = null, string? newValues = null);
    Task<IEnumerable<AuditLogDto>> GetAllAsync();
    Task<IEnumerable<AuditLogDto>> GetByUserIdAsync(string userId);
}

public class AuditLogDto
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime Timestamp { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}
