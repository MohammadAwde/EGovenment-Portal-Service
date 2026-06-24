using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Repositories;

public class CitizenRepository : Repository<Citizen>, ICitizenRepository
{
    public CitizenRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Citizen?> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    public async Task<Citizen?> GetByNationalIdAsync(string nationalId)
    {
        return await _dbSet.FirstOrDefaultAsync(c => c.NationalId == nationalId);
    }

    public async Task<Citizen?> GetWithServiceRequestsAsync(int citizenId)
    {
        return await _dbSet
            .Include(c => c.ServiceRequests)
                .ThenInclude(sr => sr.GovernmentService)
            .FirstOrDefaultAsync(c => c.Id == citizenId);
    }
}
