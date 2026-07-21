using System.ComponentModel.DataAnnotations;

namespace SmartEGov.Application.DTOs;

public class GovernmentServiceDto
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Fee { get; set; }

    [Range(1, 365)]
    public int EstimatedDays { get; set; }

    [Range(15, 240)]
    public int SlotDurationMinutes { get; set; } = 30;

    public bool IsActive { get; set; } = true;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int? ApprovalWorkflowId { get; set; }
    public IEnumerable<RequiredDocumentDto> RequiredDocuments { get; set; } = new List<RequiredDocumentDto>();
}

public class RequiredDocumentDto
{
    public int Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
    public int GovernmentServiceId { get; set; }
}

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
