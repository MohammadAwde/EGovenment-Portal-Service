namespace SmartEGov.Domain.Entities;

public class SupportReply
{
    public int Id { get; set; }
    public int SupportMessageId { get; set; }
    public SupportMessage SupportMessage { get; set; } = null!;
    public string SenderUserId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public bool IsFromAdmin { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}