namespace SmartEGov.Application.Services;

public class TwilioSettings
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromPhoneNumber { get; set; } = string.Empty;
    // Optional Twilio Messaging Service SID - preferred for sending when available
    public string? MessagingServiceSid { get; set; }
    // Optional default country code to normalize local phone numbers (e.g. "+961")
    public string? DefaultCountryCode { get; set; }
}
