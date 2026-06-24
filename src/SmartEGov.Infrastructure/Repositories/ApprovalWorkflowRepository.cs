using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Repositories;

public class ApprovalWorkflowRepository : Repository<ApprovalWorkflow>, IApprovalWorkflowRepository
{
    public ApprovalWorkflowRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ApprovalWorkflow>> GetActiveWorkflowsAsync()
    {
        return await _dbSet.Where(w => w.IsActive).ToListAsync();
    }
}
