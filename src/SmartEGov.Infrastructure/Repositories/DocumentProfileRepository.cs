using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Repositories;

public class DocumentProfileRepository : Repository<DocumentProfile>, IDocumentProfileRepository
{
    public DocumentProfileRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<DocumentProfile>> GetByUserIdAsync(string userId)
        => await _context.DocumentProfiles
            .Where(p => p.UserId == userId).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<DocumentProfile?> GetByUserAndTypeAsync(string userId, string documentType)
        => await _context.DocumentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.DocumentType == documentType);
}
