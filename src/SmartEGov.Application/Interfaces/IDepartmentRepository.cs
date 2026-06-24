using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IDepartmentRepository : IRepository<Department>
{
    Task<IEnumerable<Department>> GetActiveDepartmentsAsync();
}
