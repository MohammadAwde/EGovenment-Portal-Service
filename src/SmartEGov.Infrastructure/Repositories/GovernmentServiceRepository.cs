using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Repositories;

public class GovernmentServiceRepository : Repository<GovernmentService>, IGovernmentServiceRepository
{
    public GovernmentServiceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<GovernmentService>> GetActiveServicesAsync()
    {
        return await _dbSet
            .Include(g => g.Department)
            .Where(g => g.IsActive)
            .ToListAsync();
    }

    public async Task<GovernmentService?> GetWithWorkflowAsync(int id)
    {
        return await _dbSet
            .Include(g => g.ApprovalWorkflow)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<GovernmentService?> GetWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(g => g.Department)
            .Include(g => g.ApprovalWorkflow)
            .Include(g => g.RequiredDocuments)
            .FirstOrDefaultAsync(g => g.Id == id);
    }
}
