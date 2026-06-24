using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IDocumentRepository : IRepository<Document>
{
    Task<IEnumerable<Document>> GetByServiceRequestIdAsync(int serviceRequestId);
}
