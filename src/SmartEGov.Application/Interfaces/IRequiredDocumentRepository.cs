using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IRequiredDocumentRepository : IRepository<RequiredDocument>
{
    Task<IEnumerable<RequiredDocument>> GetByServiceIdAsync(int governmentServiceId);
    Task<RequiredDocument?> GetByIdAsync(int id);
}
