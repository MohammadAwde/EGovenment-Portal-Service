using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Services;

public class AppointmentReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentReminderService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public AppointmentReminderService(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Appointment Reminder Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending appointment reminders.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task SendRemindersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var tomorrow = DateTime.UtcNow.Date.AddDays(1);

        // Get booked appointments for tomorrow that haven't been reminded yet
        var appointments = await ctx.Appointments
            .Include(a => a.User)
            .Include(a => a.ServiceCenter)
            .Include(a => a.GovernmentService)
            .Where(a =>
                a.Status == "Booked" &&
                a.AppointmentDate.Date == tomorrow &&
                !ctx.AppointmentReminderLogs.Any(r => r.AppointmentId == a.Id))
            .ToListAsync();

        _logger.LogInformation("Found {Count} appointments to remind for {Date}",
            appointments.Count, tomorrow.ToShortDateString());

        foreach (var apt in appointments)
        {
            try
            {
                await SendReminderEmailAsync(emailSender, apt);

                // Log the reminder
                ctx.AppointmentReminderLogs.Add(new AppointmentReminderLog
                {
                    AppointmentId = apt.Id,
                    SentAt = DateTime.UtcNow,
                    Channel = "Email"
                });

                _logger.LogInformation("Reminder sent for appointment {Ref}", apt.ReferenceNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder for appointment {Ref}", apt.ReferenceNumber);
            }
        }

        await ctx.SaveChangesAsync();
    }

    private async Task SendReminderEmailAsync(IEmailSender emailSender, Appointment apt)
    {
        var subject = $"Appointment Reminder — {apt.ReferenceNumber}";
        var body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
  <div style='background:#1a5c2e;padding:20px;text-align:center;'>
    <h2 style='color:white;margin:0;'>SmartEGov — Appointment Reminder</h2>
    <p style='color:rgba(255,255,255,0.8);margin:8px 0 0;'>Lebanese E-Government Portal</p>
  </div>
  <div style='padding:30px;background:#f9f9f9;'>
    <p style='font-size:16px;'>Dear <strong>{apt.User.FullName}</strong>,</p>
    <p>This is a reminder that you have an appointment <strong>tomorrow</strong>.</p>
    <div style='background:white;border-radius:8px;padding:20px;border-left:4px solid #1a5c2e;margin:20px 0;'>
      <table style='width:100%;font-size:14px;'>
        <tr><td style='color:#666;padding:6px 0;'>Reference</td><td style='font-weight:bold;'>{apt.ReferenceNumber}</td></tr>
        <tr><td style='color:#666;padding:6px 0;'>Service</td><td>{apt.GovernmentService?.Name}</td></tr>
        <tr><td style='color:#666;padding:6px 0;'>Service Center</td><td>{apt.ServiceCenter?.CenterName ?? "N/A"}</td></tr>
        <tr><td style='color:#666;padding:6px 0;'>Address</td><td>{apt.ServiceCenter?.Address}</td></tr>
        <tr><td style='color:#666;padding:6px 0;'>Date</td><td>{apt.AppointmentDate:dddd, MMMM dd, yyyy}</td></tr>
        <tr><td style='color:#666;padding:6px 0;'>Time</td><td><strong>{apt.TimeSlot}</strong></td></tr>
      </table>
    </div>
    <p style='color:#666;font-size:13px;'>Please arrive 10 minutes early and bring your Lebanese ID card.</p>
    <p style='color:#666;font-size:13px;'>To cancel or reschedule, visit the portal at least 2 hours before your appointment.</p>
  </div>
  <div style='background:#1a5c2e;padding:15px;text-align:center;'>
    <p style='color:rgba(255,255,255,0.7);font-size:12px;margin:0;'>© {DateTime.Now.Year} Lebanese Republic — E-Government Portal</p>
  </div>
</div>";

        if (!string.IsNullOrWhiteSpace(apt.User.Email))
            await emailSender.SendEmailAsync(apt.User.Email!, subject, body);
    }
}
