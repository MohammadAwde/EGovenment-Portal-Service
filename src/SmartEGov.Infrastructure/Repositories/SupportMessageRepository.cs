using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Repositories;

public class SupportMessageRepository : Repository<SupportMessage>, ISupportMessageRepository
{
    public SupportMessageRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SupportMessage>> GetByUserIdAsync(string userId)
    => await _dbSet
        .Include(m => m.Replies)
        .Where(m => m.CitizenUserId == userId)
        .OrderByDescending(m => m.CreatedAt)
        .ToListAsync();

    public async Task<IEnumerable<SupportMessage>> GetAllWithRepliesAsync()
        => await _dbSet
            .Include(m => m.Replies)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

    public async Task<SupportMessage?> GetByIdWithRepliesAsync(int id)
    => await _dbSet.Include(m => m.Replies).FirstOrDefaultAsync(m => m.Id == id);
}