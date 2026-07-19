using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartEGov.Application.Services;
using System.Text.RegularExpressions;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MailKit;
using System.IO;

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
    public string? ProtocolLogPath { get; set; }
}

public class SmtpEmailService : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _settings = new SmtpSettings();
        configuration.GetSection("SmtpSettings").Bind(_settings);
        _logger = logger;

        // default protocol log path if not set
        if (string.IsNullOrWhiteSpace(_settings.ProtocolLogPath))
            _settings.ProtocolLogPath = Path.Combine("logs", "mailkit-protocol.log");
    }

    public async Task SendEmailAsync(string to, string subject, string htmlMessage)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject ?? string.Empty;

            var builder = new BodyBuilder();
            if (!string.IsNullOrWhiteSpace(htmlMessage))
            {
                builder.HtmlBody = htmlMessage;
                // Also provide a plain-text fallback
                var text = Regex.Replace(htmlMessage, "<.*?>", string.Empty);
                builder.TextBody = text;
            }
            else
            {
                builder.TextBody = string.Empty;
            }

            message.Body = builder.ToMessageBody();

            // Ensure logs directory exists for protocol logs
            try
            {
                var logDir = Path.GetDirectoryName(_settings.ProtocolLogPath) ?? "logs";
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to ensure protocol log directory exists: {Path}", _settings.ProtocolLogPath);
            }

            using var client = new MailKit.Net.Smtp.SmtpClient(new ProtocolLogger(_settings.ProtocolLogPath!));

            // Determine secure socket options
            SecureSocketOptions socketOptions = SecureSocketOptions.Auto;
            if (_settings.EnableSsl)
            {
                if (_settings.Port == 465)
                    socketOptions = SecureSocketOptions.SslOnConnect;
                else
                    socketOptions = SecureSocketOptions.StartTls;
            }
            else
            {
                socketOptions = SecureSocketOptions.StartTlsWhenAvailable;
            }

            _logger.LogInformation("Connecting to SMTP {Host}:{Port} (SSL={EnableSsl}, SocketOptions={SocketOptions})", _settings.Host, _settings.Port, _settings.EnableSsl, socketOptions);

            // Connect
            await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions);
            _logger.LogInformation("SMTP connected: {Host}:{Port}", _settings.Host, _settings.Port);

            // Authenticate if credentials provided
            if (!string.IsNullOrWhiteSpace(_settings.UserName))
            {
                try
                {
                    _logger.LogInformation("Authenticating as {User}", _settings.UserName);
                    await client.AuthenticateAsync(_settings.UserName, _settings.Password);
                    _logger.LogInformation("SMTP authenticate succeeded for {User}", _settings.UserName);
                }
                catch (AuthenticationException aex)
                {
                    _logger.LogError(aex, "SMTP authentication failed for {User}", _settings.UserName);
                    throw;
                }
                catch (SmtpCommandException scex)
                {
                    _logger.LogError(scex, "SMTP command failed during authentication: {Response}", scex.Message);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during SMTP authentication");
                    throw;
                }
            }

            _logger.LogInformation("Sending message to {To}", to);
            await client.SendAsync(message);
            _logger.LogInformation("Message send completed to {To}", to);

            await client.DisconnectAsync(true);
            _logger.LogInformation("SMTP disconnected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            // Re-throw so callers/tests can detect errors if desired. Comment out if undesirable.
            throw;
        }
    }
}
