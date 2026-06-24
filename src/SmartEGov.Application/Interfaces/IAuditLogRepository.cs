using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityName, string entityId);
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(string userId);
}
