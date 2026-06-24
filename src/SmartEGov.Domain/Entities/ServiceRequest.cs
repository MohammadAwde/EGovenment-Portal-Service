using SmartEGov.Domain.Enums;

namespace SmartEGov.Domain.Entities;

public class ServiceRequest
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Submitted;
    public string Notes { get; set; } = string.Empty;
    // For draft requests, SubmittedAt will be null until the request is submitted.
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? CompletionFileName { get; set; }
    public string? CompletionFilePath { get; set; }

    public int CitizenId { get; set; }
    public Citizen Citizen { get; set; } = null!;

    public int GovernmentServiceId { get; set; }
    public GovernmentService GovernmentService { get; set; } = null!;

    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<ApprovalStep> ApprovalSteps { get; set; } = new List<ApprovalStep>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
