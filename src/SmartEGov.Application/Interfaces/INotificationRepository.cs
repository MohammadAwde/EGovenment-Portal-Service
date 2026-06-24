using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    Task<IEnumerable<Notification>> GetByUserIdAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
}
