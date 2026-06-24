using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Repositories;

public class AppointmentRepository : Repository<Appointment>, IAppointmentRepository
{
    public AppointmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Appointment>> GetByUserIdAsync(string userId)
        => await _context.Appointments
            .Include(a => a.ServiceCenter).Include(a => a.GovernmentService)
            .Where(a => a.UserId == userId).OrderByDescending(a => a.AppointmentDate).ToListAsync();

    public async Task<IEnumerable<Appointment>> GetByCenterAndDateAsync(int serviceCenterId, DateTime date)
        => await _context.Appointments
            .Where(a => a.ServiceCenterId == serviceCenterId
                     && a.AppointmentDate.Date == date.Date && a.Status == "Booked").ToListAsync();

    public async Task<bool> SlotTakenAsync(int serviceCenterId, DateTime date, string timeSlot)
        => await _context.Appointments.AnyAsync(a =>
            a.ServiceCenterId == serviceCenterId && a.AppointmentDate.Date == date.Date
            && a.TimeSlot == timeSlot && a.Status == "Booked");

    public async Task<Appointment?> GetWithDetailsAsync(int appointmentId)
        => await _context.Appointments
            .Include(a => a.ServiceCenter).Include(a => a.GovernmentService)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);
}

public class ServiceCenterRepository : Repository<ServiceCenter>, IServiceCenterRepository
{
    public ServiceCenterRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ServiceCenter>> GetActiveAsync()
        => await _context.ServiceCenters.Where(c => c.IsActive).OrderBy(c => c.CenterName).ToListAsync();
}
