using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface ISupportMessageRepository : IRepository<SupportMessage>
{
    Task<IEnumerable<SupportMessage>> GetByUserIdAsync(string userId);
    Task<IEnumerable<SupportMessage>> GetAllWithRepliesAsync();
    Task<SupportMessage?> GetByIdWithRepliesAsync(int id);
}