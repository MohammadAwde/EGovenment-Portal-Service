namespace SmartEGov.Domain.Entities;

public class PublicHoliday
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int Day { get; set; }
    public int Month { get; set; }
    public int? Year { get; set; } // null = recurring every year
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
