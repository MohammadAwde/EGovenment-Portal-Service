using AutoMapper;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailSender _emailSender;
    private readonly IBrevoSmsService _brevoSms;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMapper _mapper;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IUnitOfWork unitOfWork, IEmailSender emailSender, IBrevoSmsService brevoSms, UserManager<ApplicationUser> userManager, IMapper mapper, ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailSender = emailSender;
        _brevoSms = brevoSms;
        _userManager = userManager;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task SendAsync(string userId, string title, string message)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            IsRead = false
        };

        await _unitOfWork.Notifications.AddAsync(notification);
        await _unitOfWork.SaveChangesAsync();

        var citizen = await _unitOfWork.Citizens.GetByUserIdAsync(userId);
        if (citizen != null)
        {
            if (!string.IsNullOrWhiteSpace(citizen.PhoneNumber))
            {
                try
                {
                    var smsText = title + " - " + (message.Length > 200 ? message.Substring(0, 197) + "..." : message);
                    var sent = await _brevoSms.SendSmsAsync(citizen.PhoneNumber, smsText);
                    if (sent)
                        _logger.LogInformation("Sent SMS to {Phone} for user {UserId}", citizen.PhoneNumber, userId);
                    else
                        _logger.LogWarning("Failed to send SMS to {Phone} for user {UserId}", citizen.PhoneNumber, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while sending SMS to {Phone} for user {UserId}", citizen.PhoneNumber, userId);
                }
            }
            else
            {
                _logger?.LogInformation("No phone number for user {UserId}; skipping SMS/WhatsApp.", userId);
            }
        }

        // Send email using Identity user record when available
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var html = $"<p>{message}</p>";
            try
            {
                _logger?.LogDebug("Attempting to send email to user {UserId} at {Email}", userId, user.Email);
                await _emailSender.SendEmailAsync(user.Email, title, html);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send email to user {UserId} at {Email}", userId, user.Email);
            }
        }
    }

    public async Task<IEnumerable<NotificationDto>> GetByUserIdAsync(string userId)
    {
        var notifications = await _unitOfWork.Notifications.GetByUserIdAsync(userId);
        return _mapper.Map<IEnumerable<NotificationDto>>(notifications);
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _unitOfWork.Notifications.GetUnreadCountAsync(userId);
    }

    public async Task MarkAsReadAsync(int notificationId)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId);
        if (notification == null) throw new KeyNotFoundException("Notification not found.");

        notification.IsRead = true;
        _unitOfWork.Notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync();
    }
}
