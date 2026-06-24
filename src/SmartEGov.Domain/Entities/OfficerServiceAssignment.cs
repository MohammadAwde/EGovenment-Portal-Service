namespace SmartEGov.Domain.Entities;

public class OfficerServiceAssignment
{
    public int Id { get; set; }

    public string OfficerId { get; set; } = string.Empty;
    public ApplicationUser Officer { get; set; } = null!;

    public int GovernmentServiceId { get; set; }
    public GovernmentService GovernmentService { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
