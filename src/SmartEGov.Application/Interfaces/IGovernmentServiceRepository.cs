using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IGovernmentServiceRepository : IRepository<GovernmentService>
{
    Task<IEnumerable<GovernmentService>> GetActiveServicesAsync();
    Task<GovernmentService?> GetWithWorkflowAsync(int id);
    Task<GovernmentService?> GetWithDetailsAsync(int id);
}
