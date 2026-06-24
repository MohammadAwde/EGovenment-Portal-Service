using SmartEGov.Domain.Enums;

namespace SmartEGov.Domain.Entities;

public class ApprovalStep
{
    public int Id { get; set; }
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? Comments { get; set; }
    public string? OfficerSignaturePath { get; set; }
    public DateTime? ActionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // RowVersion for optimistic concurrency control. Requires EF migration to add to DB.
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[]? RowVersion { get; set; }

    public int ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public string? OfficerId { get; set; }
    public ApplicationUser? Officer { get; set; }
}
