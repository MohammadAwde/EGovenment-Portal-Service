namespace SmartEGov.Application.Services;

public class SmsSettings
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromPhoneNumber { get; set; } = string.Empty;
    // Optional Twilio Messaging Service SID - preferred for sending when available
    public string? MessagingServiceSid { get; set; }
}

public interface ISmsNotificationService
{
    Task SendSmsAsync(string toPhoneNumber, string message);
}
