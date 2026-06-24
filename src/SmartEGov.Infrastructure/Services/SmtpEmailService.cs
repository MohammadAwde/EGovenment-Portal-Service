using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartEGov.Application.Services;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartEGov.Infrastructure.Services;

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; } = true;
    public string FromName { get; set; } = "SmartEgov";
    public string FromEmail { get; set; } = "no-reply@smartegov.local";
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
}

public class SmtpEmailService : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly string? _mailtrapApiToken;
    private readonly IConfiguration _configuration;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _settings = new SmtpSettings();
        configuration.GetSection("SmtpSettings").Bind(_settings);
        _mailtrapApiToken = configuration["Mailtrap:ApiToken"];
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlMessage)
    {
        try
        {
            // If Mailtrap API token is configured, prefer using Mailtrap Send API over raw SMTP
            if (!string.IsNullOrWhiteSpace(_mailtrapApiToken))
            {
                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _mailtrapApiToken);

                var text = Regex.Replace(htmlMessage ?? string.Empty, "<.*?>", string.Empty);
                var payload = new
                {
                    from = new { email = _settings.FromEmail, name = _settings.FromName },
                    to = new[] { new { email = to } },
                    subject = subject,
                    html = htmlMessage,
                    text = text
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var endpoint = _configuration["Mailtrap:SendEndpoint"] ?? "https://send.api.mailtrap.io/api/send";
                var resp = await http.PostAsync(endpoint, content);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    _logger.LogError("Mailtrap Send API responded with {Status}: {Body}", resp.StatusCode, body);
                }
                else
                {
                    _logger.LogInformation("Email sent via Mailtrap to {To}.", to);
                }

                return;
            }

            // Fallback to SMTP
            using var msg = new System.Net.Mail.MailMessage();
            msg.From = new System.Net.Mail.MailAddress(_settings.FromEmail, _settings.FromName);
            msg.To.Add(new System.Net.Mail.MailAddress(to));
            msg.Subject = subject;
            msg.Body = htmlMessage;
            msg.IsBodyHtml = true;

            using var client = new System.Net.Mail.SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_settings.UserName))
            {
                client.Credentials = new System.Net.NetworkCredential(_settings.UserName, _settings.Password);
            }

            // Send asynchronously
            await client.SendMailAsync(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
        }
    }
}
