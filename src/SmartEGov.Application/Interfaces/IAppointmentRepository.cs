using SmartEGov.Domain.Entities;
namespace SmartEGov.Application.Interfaces;

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<IEnumerable<Appointment>> GetByUserIdAsync(string userId);
    Task<IEnumerable<Appointment>> GetByCenterAndDateAsync(int serviceCenterId, DateTime date);
    Task<bool> SlotTakenAsync(int serviceCenterId, DateTime date, string timeSlot);
    Task<Appointment?> GetWithDetailsAsync(int appointmentId);
}

public interface IServiceCenterRepository : IRepository<ServiceCenter>
{
    Task<IEnumerable<ServiceCenter>> GetActiveAsync();
    Task<ServiceCenter?> GetByIdWithSchedulesAsync(int id);

}

public interface IDocumentProfileRepository : IRepository<DocumentProfile>
{
    Task<IEnumerable<DocumentProfile>> GetByUserIdAsync(string userId);
    Task<DocumentProfile?> GetByUserAndTypeAsync(string userId, string documentType);
}
