using SmartEGov.Application.DTOs;

namespace SmartEGov.Application.Services;

public interface IAppointmentService
{
    Task<IEnumerable<ServiceCenterDto>> GetNearestCentersAsync(double latitude, double longitude, int radiusKm = 20);
    Task<IEnumerable<ServiceCenterDto>> GetAllCentersAsync();
    Task<IEnumerable<string>> GetAvailableSlotsAsync(int serviceCenterId, int governmentServiceId, DateTime date);
    Task<AppointmentDto> BookAsync(BookAppointmentRequest request, string userId);
    Task<IEnumerable<AppointmentDto>> GetByUserAsync(string userId);
    Task<AppointmentDto?> GetByIdAsync(int appointmentId, string userId);
    Task CancelAsync(int appointmentId, string userId);
    Task<AppointmentDto> RescheduleAsync(int appointmentId, DateTime newDate, string newTimeSlot, string userId, int? newCenterId = null);
}