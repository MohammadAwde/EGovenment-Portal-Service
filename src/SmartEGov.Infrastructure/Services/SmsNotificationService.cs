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

            // Prefer using MessagingServiceSid if configured (allows Twilio to use a pool of senders)
            MessageResource messageResource;
            if (!string.IsNullOrWhiteSpace(_settings.MessagingServiceSid))
            {
                messageResource = await MessageResource.CreateAsync(
                    to: new PhoneNumber(toPhoneNumber),
                    messagingServiceSid: _settings.MessagingServiceSid,
                    body: message);
            }
            else
            {
                messageResource = await MessageResource.CreateAsync(
                    to: new PhoneNumber(toPhoneNumber),
                    from: new PhoneNumber(_settings.FromPhoneNumber),
                    body: message);
            }

            _logger.LogInformation("SMS sent to {PhoneNumber}. SID: {MessageSid}", toPhoneNumber, messageResource.Sid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}.", toPhoneNumber);
        }
    }
}
