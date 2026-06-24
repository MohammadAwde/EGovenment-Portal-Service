using SmartEGov.Domain.Enums;

namespace SmartEGov.Application.DTOs;

public class ApprovalStepDto
{
    public int Id { get; set; }
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public ApprovalStatus Status { get; set; }
    public string? Comments { get; set; }
    public string? OfficerSignaturePath { get; set; }
    public DateTime? ActionDate { get; set; }
    public int ServiceRequestId { get; set; }
    public string? OfficerId { get; set; }
    public string? OfficerName { get; set; }
}
