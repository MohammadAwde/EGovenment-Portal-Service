using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SmartEGov.Infrastructure.Repositories;

public class RequiredDocumentRepository : Repository<RequiredDocument>, IRequiredDocumentRepository
{
    private readonly ApplicationDbContext _context;

    public RequiredDocumentRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<IEnumerable<RequiredDocument>> GetByServiceIdAsync(int governmentServiceId)
    {
        return await _context.RequiredDocuments
            .Where(r => r.GovernmentServiceId == governmentServiceId)
            .ToListAsync();
    }

    public async Task<RequiredDocument?> GetByIdAsync(int id)
    {
        return await _context.RequiredDocuments.FindAsync(id);
    }
}
