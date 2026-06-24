using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Repositories;

public class OfficerServiceAssignmentRepository : Repository<OfficerServiceAssignment>, IOfficerServiceAssignmentRepository
{
    public OfficerServiceAssignmentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<OfficerServiceAssignment>> GetAllWithDetailsAsync()
    {
        return await _dbSet
            .Include(o => o.Officer)
            .Include(o => o.GovernmentService)
            .OrderBy(o => o.Officer.FullName)
            .ThenBy(o => o.GovernmentService.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<OfficerServiceAssignment>> GetByOfficerIdAsync(string officerId)
    {
        return await _dbSet
            .Include(o => o.GovernmentService)
            .Where(o => o.OfficerId == officerId)
            .ToListAsync();
    }

    public async Task<IEnumerable<OfficerServiceAssignment>> GetByServiceIdAsync(int governmentServiceId)
    {
        return await _dbSet
            .Include(o => o.Officer)
            .Where(o => o.GovernmentServiceId == governmentServiceId)
            .ToListAsync();
    }

    public async Task<OfficerServiceAssignment?> GetByOfficerAndServiceAsync(string officerId, int governmentServiceId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(o => o.OfficerId == officerId && o.GovernmentServiceId == governmentServiceId);
    }
}
