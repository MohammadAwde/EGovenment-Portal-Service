using System.ComponentModel.DataAnnotations;
using SmartEGov.Domain.Enums;

namespace SmartEGov.Application.DTOs;

public class ServiceRequestDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;

    [Required]
    public int GovernmentServiceId { get; set; }

    public string? GovernmentServiceName { get; set; }

    public string? Notes { get; set; }
    // Default to Submitted unless explicitly set to Draft when saving as draft
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Submitted;
    // Nullable because drafts are not yet submitted
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? CompletionFileName { get; set; }
    public string? CompletionFilePath { get; set; }

    public int CitizenId { get; set; }
    public string? CitizenName { get; set; }
}
