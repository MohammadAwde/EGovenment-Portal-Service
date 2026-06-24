namespace SmartEGov.Domain.Entities;

public class DepartmentLocation
{
    public int Id { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? PhoneNumber { get; set; }

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
}
