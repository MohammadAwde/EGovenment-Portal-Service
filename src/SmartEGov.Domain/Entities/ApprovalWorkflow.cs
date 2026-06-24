namespace SmartEGov.Domain.Entities;

public class ApprovalWorkflow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalSteps { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GovernmentService> GovernmentServices { get; set; } = new List<GovernmentService>();
}
