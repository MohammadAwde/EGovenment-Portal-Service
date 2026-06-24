using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartEGov.Application.Services;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartEGov.Infrastructure.Services;

public class MailtrapService : IMailtrapService
{
    private readonly ILogger<MailtrapService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string? _apiToken;

    public MailtrapService(IConfiguration configuration, ILogger<MailtrapService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _apiToken = configuration["Mailtrap:ApiToken"];
    }

    public async Task<bool> SendAsync(string to, string subject, string html)
    {
        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            _logger.LogDebug("Mailtrap API token not configured; skipping Mailtrap send.");
            return false;
        }

        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

            var text = Regex.Replace(html ?? string.Empty, "<.*?>", string.Empty);
            var smtpFrom = _configuration["SmtpSettings:FromEmail"] ?? "no-reply@smartegov.local";
            var smtpFromName = _configuration["SmtpSettings:FromName"] ?? "SmartEgov";

            // Build payload matching Mailtrap Send API example
            var payload = new Dictionary<string, object?>
            {
                ["from"] = new { email = smtpFrom, name = smtpFromName },
                ["to"] = new[] { new { email = to } },
                ["subject"] = subject,
                ["text"] = text,
                ["category"] = subject?.Contains("password", StringComparison.OrdinalIgnoreCase) == true ? "Password Reset" : "Notification"
            };

            if (!string.IsNullOrWhiteSpace(html))
            {
                // include html when available
                payload["html"] = html;
            }

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var endpoint = _configuration["Mailtrap:SendEndpoint"] ?? "https://send.api.mailtrap.io/api/send";
            var resp = await http.PostAsync(endpoint, content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _logger.LogError("Mailtrap Send API responded with {Status}: {Body}", resp.StatusCode, body);
                return false;
            }

            _logger.LogInformation("Mailtrap: email sent to {To}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mailtrap Send API failed for {To}", to);
            return false;
        }
    }
}
