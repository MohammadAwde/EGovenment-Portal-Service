using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Repositories;

public class DocumentRepository : Repository<Document>, IDocumentRepository
{
    public DocumentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Document>> GetByServiceRequestIdAsync(int serviceRequestId)
    {
        return await _dbSet
            .Where(d => d.ServiceRequestId == serviceRequestId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }
}
