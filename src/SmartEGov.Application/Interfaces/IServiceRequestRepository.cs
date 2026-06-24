
using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IServiceRequestRepository : IRepository<ServiceRequest>
{
    Task<ServiceRequest?> GetWithDetailsAsync(int id);
    Task<IEnumerable<ServiceRequest>> GetByCitizenIdAsync(int citizenId);
    Task<IEnumerable<ServiceRequest>> GetPendingRequestsAsync();
    Task<IEnumerable<ServiceRequest>> GetPendingByServiceIdsAsync(IEnumerable<int> serviceIds);
    Task<IEnumerable<ServiceRequest>> GetPendingByOfficerIdAsync(string officerId);
    Task<ServiceRequest?> GetByReferenceNumberAsync(string referenceNumber);
    Task<IEnumerable<ServiceRequest>> GetRecentByCitizenAndServiceAsync(int citizenId, int governmentServiceId, DateTime since);
}
