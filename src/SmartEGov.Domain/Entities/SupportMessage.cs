namespace SmartEGov.Domain.Entities;

public class SupportMessage
{
    public int Id { get; set; }
    public string CitizenUserId { get; set; } = string.Empty;
    public string CitizenName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Reply { get; set; }
    public string? RepliedByUserId { get; set; }
    public ICollection<SupportReply> Replies { get; set; } = new List<SupportReply>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RepliedAt { get; set; }
    public string Status { get; set; } = "Open";
}