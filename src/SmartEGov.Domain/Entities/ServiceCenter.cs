namespace SmartEGov.Domain.Entities;

public class ServiceCenter
{
    public int Id { get; set; }
    public string CenterName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string WorkingHours { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool WorksOnSaturday { get; set; } = false;
    public bool WorksOnSunday { get; set; } = false;
    public TimeSpan? FridayClosingOverride { get; set; } = new TimeSpan(12, 0, 0);
    public TimeSpan WorkingHoursStart { get; set; } = new TimeSpan(9, 0, 0);
    public TimeSpan WorkingHoursEnd { get; set; } = new TimeSpan(17, 0, 0);
    public TimeSpan LunchBreakStart { get; set; } = new TimeSpan(12, 0, 0);
    public TimeSpan LunchBreakEnd { get; set; } = new TimeSpan(13, 0, 0);
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
