using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IOfficerServiceAssignmentRepository : IRepository<OfficerServiceAssignment>
{
    Task<IEnumerable<OfficerServiceAssignment>> GetAllWithDetailsAsync();
    Task<IEnumerable<OfficerServiceAssignment>> GetByOfficerIdAsync(string officerId);
    Task<IEnumerable<OfficerServiceAssignment>> GetByServiceIdAsync(int governmentServiceId);
    Task<OfficerServiceAssignment?> GetByOfficerAndServiceAsync(string officerId, int governmentServiceId);
}
