namespace SmartEGov.Domain.Entities;

public class RequiredDocument
{
    public int Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;

    public int GovernmentServiceId { get; set; }
    public GovernmentService GovernmentService { get; set; } = null!;
}
