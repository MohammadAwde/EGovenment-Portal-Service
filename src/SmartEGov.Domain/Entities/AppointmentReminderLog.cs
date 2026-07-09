namespace SmartEGov.Domain.Entities;

public class AppointmentReminderLog
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string Channel { get; set; } = "Email"; // Email or SMS
    public Appointment Appointment { get; set; } = null!;
}
