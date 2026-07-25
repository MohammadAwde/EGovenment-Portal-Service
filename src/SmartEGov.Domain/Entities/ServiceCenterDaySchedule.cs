namespace SmartEGov.Domain.Entities;

public class ServiceCenterDaySchedule
{
    public int Id { get; set; }
    public int ServiceCenterId { get; set; }
    public ServiceCenter ServiceCenter { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan OpenTime { get; set; } = new TimeSpan(9, 0, 0);
    public TimeSpan CloseTime { get; set; } = new TimeSpan(17, 0, 0);
    public TimeSpan BreakStart { get; set; } = new TimeSpan(12, 0, 0);
    public TimeSpan BreakEnd { get; set; } = new TimeSpan(13, 0, 0);
}