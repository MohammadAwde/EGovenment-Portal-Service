using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Domain.Enums;
using SmartEGov.Infrastructure.Data;
using System.Linq;

namespace SmartEGov.Infrastructure.Repositories;

public class ServiceRequestRepository : Repository<ServiceRequest>, IServiceRequestRepository
{
    public ServiceRequestRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<ServiceRequest?> GetWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(sr => sr.Citizen).ThenInclude(c => c.User)
            .Include(sr => sr.GovernmentService)
            .Include(sr => sr.Documents)
            .Include(sr => sr.ApprovalSteps)
            .FirstOrDefaultAsync(sr => sr.Id == id);
    }

    public async Task<IEnumerable<ServiceRequest>> GetByCitizenIdAsync(int citizenId)
    {
        return await _dbSet
            .Include(sr => sr.GovernmentService)
            .Where(sr => sr.CitizenId == citizenId)
            .OrderByDescending(sr => sr.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetPendingRequestsAsync()
    {
        return await _dbSet
            .Include(sr => sr.Citizen).ThenInclude(c => c.User)
            .Include(sr => sr.GovernmentService)
            // Consider a request "pending" for officers while it has not reached a terminal state.
            // Terminal states: Completed, Cancelled, Rejected. Keep requests visible until then.
            .Where(sr => sr.Status != ServiceRequestStatus.Completed
                         && sr.Status != ServiceRequestStatus.Cancelled
                         && sr.Status != ServiceRequestStatus.Rejected)
            .OrderBy(sr => sr.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetPendingByServiceIdsAsync(IEnumerable<int> serviceIds)
    {
        var ids = serviceIds.ToList();
        return await _dbSet
            .Include(sr => sr.Citizen).ThenInclude(c => c.User)
            .Include(sr => sr.GovernmentService)
            // Include any non-terminal statuses for officer visibility until service is completed/rejected/cancelled
            .Where(sr => sr.Status != ServiceRequestStatus.Completed
                         && sr.Status != ServiceRequestStatus.Cancelled
                         && sr.Status != ServiceRequestStatus.Rejected
                         && ids.Contains(sr.GovernmentServiceId))
            .OrderBy(sr => sr.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetPendingByOfficerIdAsync(string officerId)
    {
        return await _dbSet
            .Include(sr => sr.Citizen).ThenInclude(c => c.User)
            .Include(sr => sr.GovernmentService)
            .Include(sr => sr.ApprovalSteps)
            // Return requests that are not in a terminal state and where this officer has a pending approval step
            .Where(sr => sr.Status != ServiceRequestStatus.Completed
                         && sr.Status != ServiceRequestStatus.Cancelled
                         && sr.Status != ServiceRequestStatus.Rejected
                         && sr.ApprovalSteps.Any(step => step.OfficerId == officerId && step.Status == ApprovalStatus.Pending))
            .OrderBy(sr => sr.SubmittedAt)
            .ToListAsync();
    }

    public async Task<ServiceRequest?> GetByReferenceNumberAsync(string referenceNumber)
    {
        return await _dbSet
            .Include(sr => sr.GovernmentService)
            .FirstOrDefaultAsync(sr => sr.ReferenceNumber == referenceNumber);
    }

    public async Task<IEnumerable<ServiceRequest>> GetRecentByCitizenAndServiceAsync(int citizenId, int governmentServiceId, DateTime since)
    {
        return await _dbSet
            .Where(sr => sr.CitizenId == citizenId && sr.GovernmentServiceId == governmentServiceId && sr.SubmittedAt != null && sr.SubmittedAt >= since)
            .OrderByDescending(sr => sr.SubmittedAt)
            .ToListAsync();
    }
}
