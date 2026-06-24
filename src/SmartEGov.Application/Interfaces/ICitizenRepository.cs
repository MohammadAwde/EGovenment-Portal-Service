using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface ICitizenRepository : IRepository<Citizen>
{
    Task<Citizen?> GetByUserIdAsync(string userId);
    Task<Citizen?> GetByNationalIdAsync(string nationalId);
    Task<Citizen?> GetWithServiceRequestsAsync(int citizenId);
}
