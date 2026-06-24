namespace SmartEGov.Domain.Entities;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GovernmentService> GovernmentServices { get; set; } = new List<GovernmentService>();
    public ICollection<DepartmentLocation> Locations { get; set; } = new List<DepartmentLocation>();
}
