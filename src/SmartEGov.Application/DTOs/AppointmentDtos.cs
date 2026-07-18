using System.ComponentModel.DataAnnotations;
namespace SmartEGov.Application.DTOs;

public class ServiceCenterDto
{
    public int Id { get; set; }
    public string CenterName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string WorkingHours { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public double? DistanceKm { get; set; }
}

public class AppointmentDto
{
    public int Id { get; set; }
    public int QueueNumber { get; set; }
    public string UserId { get; set; } = string.Empty;
    [Required] public int ServiceCenterId { get; set; }
    [Required] public int GovernmentServiceId { get; set; }
    [Required][DataType(DataType.Date)] public DateTime AppointmentDate { get; set; }
    [Required] public string TimeSlot { get; set; } = string.Empty;
    public string Status { get; set; } = "Booked";
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ServiceCenterName { get; set; }
    public string? ServiceCenterAddress { get; set; }
    public string? GovernmentServiceName { get; set; }
}

public class BookAppointmentRequest
{
    [Required] public int ServiceCenterId { get; set; }
    [Required] public int GovernmentServiceId { get; set; }
    [Required][DataType(DataType.Date)] public DateTime AppointmentDate { get; set; }
    [Required] public string TimeSlot { get; set; } = string.Empty;
}
