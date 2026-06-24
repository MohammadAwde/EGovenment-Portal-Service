namespace SmartEGov.Application.Services;

public interface INotificationService
{
    Task SendAsync(string userId, string title, string message);
    Task<IEnumerable<NotificationDto>> GetByUserIdAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(int notificationId);
}

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
