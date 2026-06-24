using AutoMapper;
using Microsoft.AspNetCore.Identity;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISmsNotificationService _smsNotificationService;
    private readonly IEmailSender _emailSender;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMapper _mapper;

    public NotificationService(IUnitOfWork unitOfWork, ISmsNotificationService smsNotificationService, IEmailSender emailSender, UserManager<ApplicationUser> userManager, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _smsNotificationService = smsNotificationService;
        _emailSender = emailSender;
        _userManager = userManager;
        _mapper = mapper;
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
        if (citizen != null && !string.IsNullOrWhiteSpace(citizen.PhoneNumber))
        {
            var smsBody = $"{title}: {message}";
            await _smsNotificationService.SendSmsAsync(citizen.PhoneNumber, smsBody);
        }

        // Send email using Identity user record when available
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var html = $"<p>{message}</p>";
            await _emailSender.SendEmailAsync(user.Email, title, html);
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
