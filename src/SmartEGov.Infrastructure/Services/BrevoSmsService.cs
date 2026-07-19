using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SmartEGov.Application.Services;

namespace SmartEGov.Infrastructure.Services;

public class BrevoSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty; // SMS sender id or name
    public string SmsEndpoint { get; set; } = "https://api.brevo.com/v3/transactional/sms";
}

public class BrevoSmsService : IBrevoSmsService
{
    private readonly HttpClient _http;
    private readonly BrevoSettings _settings;
    private readonly ILogger<BrevoSmsService> _logger;

    public BrevoSmsService(HttpClient http, IConfiguration configuration, ILogger<BrevoSmsService> logger)
    {
        _http = http;
        _logger = logger;
        _settings = new BrevoSettings();
        configuration.GetSection("Brevo").Bind(_settings);
    }

    public async Task<bool> SendSmsAsync(string to, string text)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("Brevo API key not configured; SMS not sent to {To}", to);
            return false;
        }

        var payload = new Dictionary<string, object?>
        {
            ["sender"] = string.IsNullOrWhiteSpace(_settings.Sender) ? null : _settings.Sender,
            ["recipient"] = to,
            ["content"] = text
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var req = new HttpRequestMessage(HttpMethod.Post, _settings.SmsEndpoint ?? "https://api.brevo.com/v3/transactional/sms")
        {
            Content = content
        };

        // Brevo uses 'api-key' header for API key authentication
        req.Headers.Add("api-key", _settings.ApiKey);

        try
        {
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _logger.LogError("Brevo SMS send failed ({Status}) to {To}: {Body}", resp.StatusCode, to, body);
                return false;
            }

            _logger.LogInformation("Brevo SMS sent to {To}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Brevo SMS exception while sending to {To}", to);
            return false;
        }
    }
}
