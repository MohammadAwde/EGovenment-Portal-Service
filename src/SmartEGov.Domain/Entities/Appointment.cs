namespace SmartEGov.Domain.Entities;

public class Appointment
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ServiceCenterId { get; set; }
    public int GovernmentServiceId { get; set; }
    public int QueueNumber { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
    public string Status { get; set; } = "Booked";
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ApplicationUser User { get; set; } = null!;
    public ServiceCenter ServiceCenter { get; set; } = null!;
    public GovernmentService GovernmentService { get; set; } = null!;
}
