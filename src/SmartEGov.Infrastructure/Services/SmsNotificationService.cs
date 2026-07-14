using Microsoft.Extensions.Logging;
using SmartEGov.Application.Services;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SmartEGov.Infrastructure.Services;

public class SmsNotificationService : ISmsNotificationService
{
    private readonly SmsSettings _settings;
    private readonly ILogger<SmsNotificationService> _logger;

    public SmsNotificationService(SmsSettings settings, ILogger<SmsNotificationService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendSmsAsync(string toPhoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            _logger.LogWarning("SMS not sent: phone number is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.AccountSid) ||
            string.IsNullOrWhiteSpace(_settings.AuthToken) ||
            string.IsNullOrWhiteSpace(_settings.FromPhoneNumber))
        {
            _logger.LogWarning("SMS not sent: Twilio settings are not configured.");
            return;
        }

        try
        {
            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);

            // Normalize phone number to E.164 when possible
            string NormalizePhone(string input)
            {
                if (string.IsNullOrWhiteSpace(input)) return input ?? string.Empty;
                var trimmed = input.Trim();

                // Keep leading + if present, remove other non-digit characters
                var chars = trimmed.Where(c => char.IsDigit(c) || c == '+').ToArray();
                var cleaned = new string(chars);

                if (cleaned.StartsWith("+")) return cleaned;
                if (cleaned.StartsWith("00")) return "+" + cleaned.Substring(2);

                // If a DefaultCountryCode is configured, use it to build E.164 number for local numbers
                var dc = _settings.DefaultCountryCode?.Trim();
                if (!string.IsNullOrWhiteSpace(dc))
                {
                    if (!dc.StartsWith("+")) dc = "+" + dc;
                    if (cleaned.StartsWith("0"))
                        return dc + cleaned.Substring(1);
                    return dc + cleaned;
                }

                // Fallback: return cleaned as-is (may be accepted by Twilio in some cases)
                return cleaned;
            }

            var toNormalized = NormalizePhone(toPhoneNumber);

            // Prefer using MessagingServiceSid if configured (allows Twilio to use a pool of senders)
            MessageResource messageResource;
            if (!string.IsNullOrWhiteSpace(_settings.MessagingServiceSid))
            {
                messageResource = await MessageResource.CreateAsync(
                    to: new PhoneNumber(toNormalized),
                    messagingServiceSid: _settings.MessagingServiceSid,
                    body: message);
            }
            else
            {
                var fromNumber = _settings.FromPhoneNumber;
                // Ensure from number is normalized as well
                var fromNormalized = NormalizePhone(fromNumber);
                messageResource = await MessageResource.CreateAsync(
                    to: new PhoneNumber(toNormalized),
                    from: new PhoneNumber(fromNormalized),
                    body: message);
            }

            _logger.LogInformation("SMS sent to {PhoneNumber}. SID: {MessageSid}", toNormalized, messageResource.Sid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}.", toPhoneNumber);
        }
    }
}
