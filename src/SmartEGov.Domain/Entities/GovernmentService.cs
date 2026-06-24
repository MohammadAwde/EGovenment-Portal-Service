namespace SmartEGov.Domain.Entities;

public class GovernmentService
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public int EstimatedDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int? ApprovalWorkflowId { get; set; }
    public ApprovalWorkflow? ApprovalWorkflow { get; set; }

    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
    public ICollection<RequiredDocument> RequiredDocuments { get; set; } = new List<RequiredDocument>();
    public ICollection<OfficerServiceAssignment> OfficerServiceAssignments { get; set; } = new List<OfficerServiceAssignment>();
}
