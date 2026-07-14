using SmartEGov.Application.DTOs;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Infrastructure.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _appointments;
    private readonly IServiceCenterRepository _centers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notifications;

    private static readonly string[] AllSlots =
    [
        "09:00-09:30","09:30-10:00","10:00-10:30","10:30-11:00",
        "11:00-11:30","11:30-12:00","13:00-13:30","13:30-14:00",
        "14:00-14:30","14:30-15:00"
    ];

    public AppointmentService(
        IAppointmentRepository appointments,
        IServiceCenterRepository centers,
        IUnitOfWork unitOfWork,
        INotificationService notifications)
    {
        _appointments = appointments;
        _centers = centers;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<IEnumerable<ServiceCenterDto>> GetNearestCentersAsync(
        double lat, double lng, int radiusKm = 20)
    {
        var centers = await _centers.GetActiveAsync();
        return centers
            .Select(c => new ServiceCenterDto
            {
                Id = c.Id,
                CenterName = c.CenterName,
                Department = c.Department,
                Address = c.Address,
                Latitude = c.Latitude,
                Longitude = c.Longitude,
                WorkingHours = c.WorkingHours,
                PhoneNumber = c.PhoneNumber,
                IsActive = c.IsActive,
                DistanceKm = HaversineKm(lat, lng, c.Latitude, c.Longitude)
            })
            .Where(c => c.DistanceKm <= radiusKm)
            .OrderBy(c => c.DistanceKm)
            .ToList();
    }

    public async Task<IEnumerable<ServiceCenterDto>> GetAllCentersAsync()
    {
        var centers = await _centers.GetActiveAsync();
        return centers.Select(c => new ServiceCenterDto
        {
            Id = c.Id,
            CenterName = c.CenterName,
            Department = c.Department,
            Address = c.Address,
            Latitude = c.Latitude,
            Longitude = c.Longitude,
            WorkingHours = c.WorkingHours,
            PhoneNumber = c.PhoneNumber,
            IsActive = c.IsActive
        }).ToList();
    }

    public async Task<IEnumerable<string>> GetAvailableSlotsAsync(int serviceCenterId, DateTime date)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return [];

        var isHoliday = await _unitOfWork.PublicHolidays.IsHolidayAsync(date);
        if (isHoliday) return [];

        // Friday — morning only (before 12:00)
        var slots = date.DayOfWeek == DayOfWeek.Friday
            ? AllSlots.Where(s => s.StartsWith("09") || s.StartsWith("10") || s.StartsWith("11"))
            : AllSlots;

        var booked = (await _appointments.GetByCenterAndDateAsync(serviceCenterId, date))
            .Select(a => a.TimeSlot).ToHashSet();

        return slots.Where(s => !booked.Contains(s)).ToList();
    }

    public async Task<AppointmentDto> BookAsync(BookAppointmentRequest request, string userId)
    {
        if (request.AppointmentDate.Date <= DateTime.Today)
            throw new InvalidOperationException("Appointment date must be in the future.");

        if (request.AppointmentDate.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
            throw new InvalidOperationException("Appointments are not available on weekends.");

        var existing = await _appointments.GetByUserIdAsync(userId);
        if (existing.Any(a =>
            a.ServiceCenterId == request.ServiceCenterId &&
            a.AppointmentDate.Date == request.AppointmentDate.Date &&
            a.TimeSlot == request.TimeSlot &&
            a.Status == "Booked"))
        {
            throw new InvalidOperationException("You already have a booking at this slot.");
        }

        if (await _appointments.SlotTakenAsync(
            request.ServiceCenterId, request.AppointmentDate, request.TimeSlot))
        {
            throw new InvalidOperationException(
                "This slot was just taken. Please choose another.");
        }

        var appointment = new Appointment
        {
            UserId = userId,
            ServiceCenterId = request.ServiceCenterId,
            GovernmentServiceId = request.GovernmentServiceId,
            AppointmentDate = request.AppointmentDate.Date,
            TimeSlot = request.TimeSlot,
            Status = "Booked",
            ReferenceNumber = GenerateRef(),
            CreatedAt = DateTime.UtcNow
        };

        await _appointments.AddAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.SendAsync(
            userId,
            "Appointment Confirmed",
            $"Your appointment #{appointment.ReferenceNumber} is confirmed for " +
            $"{appointment.AppointmentDate:dd MMM yyyy} at {appointment.TimeSlot}.");

        return await BuildDto(appointment);
    }

    public async Task<IEnumerable<AppointmentDto>> GetByUserAsync(string userId)
    {
        var list = await _appointments.GetByUserIdAsync(userId);
        var dtos = new List<AppointmentDto>();
        foreach (var a in list) dtos.Add(await BuildDto(a));
        return dtos;
    }

    public async Task<AppointmentDto?> GetByIdAsync(int id, string userId)
    {
        var a = await _appointments.GetWithDetailsAsync(id);
        if (a == null || a.UserId != userId) return null;
        return await BuildDto(a);
    }

    public async Task CancelAsync(int id, string userId)
    {
        var a = await _appointments.GetWithDetailsAsync(id)
            ?? throw new KeyNotFoundException("Appointment not found.");

        if (a.UserId != userId)
            throw new KeyNotFoundException("Appointment not found.");

        if (a.Status != "Booked")
            throw new InvalidOperationException("Only booked appointments can be cancelled.");

        EnforceWindow(a);

        a.Status = "Cancelled";
        _appointments.Update(a);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.SendAsync(
            userId,
            "Appointment Cancelled",
            $"Your appointment #{a.ReferenceNumber} has been cancelled.");
    }

    public async Task<AppointmentDto> RescheduleAsync(
        int id, DateTime newDate, string newSlot, string userId)
    {
        var a = await _appointments.GetWithDetailsAsync(id)
            ?? throw new KeyNotFoundException("Appointment not found.");

        if (a.UserId != userId)
            throw new KeyNotFoundException("Appointment not found.");

        if (a.Status != "Booked")
            throw new InvalidOperationException("Only booked appointments can be rescheduled.");

        EnforceWindow(a);

        if (newDate.Date <= DateTime.Today)
            throw new InvalidOperationException("New date must be in the future.");

        if (await _appointments.SlotTakenAsync(a.ServiceCenterId, newDate, newSlot))
            throw new InvalidOperationException("The new slot is already taken.");

        a.AppointmentDate = newDate.Date;
        a.TimeSlot = newSlot;
        _appointments.Update(a);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.SendAsync(
            userId,
            "Appointment Rescheduled",
            $"Your appointment #{a.ReferenceNumber} has been moved to " +
            $"{newDate:dd MMM yyyy} at {newSlot}.");

        return await BuildDto(a);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void EnforceWindow(Appointment a)
    {
        var slotStart = TimeSpan.Parse(a.TimeSlot.Split('-')[0]);
        var appointmentTime = a.AppointmentDate.Date + slotStart;
        if (DateTime.Now >= appointmentTime.AddHours(-2))
            throw new InvalidOperationException(
                "Cannot change appointment within 2 hours of scheduled time.");
    }

    private static string GenerateRef()
        => $"APT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                        + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                        * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double d) => d * Math.PI / 180;

    private async Task<AppointmentDto> BuildDto(Appointment a)
    {
        var f = a.ServiceCenter != null
            ? a
            : await _appointments.GetWithDetailsAsync(a.Id) ?? a;

        return new AppointmentDto
        {
            Id = f.Id,
            UserId = f.UserId,
            ServiceCenterId = f.ServiceCenterId,
            GovernmentServiceId = f.GovernmentServiceId,
            AppointmentDate = f.AppointmentDate,
            TimeSlot = f.TimeSlot,
            Status = f.Status,
            ReferenceNumber = f.ReferenceNumber,
            CreatedAt = f.CreatedAt,
            ServiceCenterName = f.ServiceCenter?.CenterName,
            ServiceCenterAddress = f.ServiceCenter?.Address,
            GovernmentServiceName = f.GovernmentService?.Name
        };
    }
}
