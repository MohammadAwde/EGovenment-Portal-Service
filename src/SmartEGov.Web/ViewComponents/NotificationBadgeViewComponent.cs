using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;
using System.Threading.Tasks;

namespace SmartEGov.Web.ViewComponents;

public class NotificationBadgeViewComponent : ViewComponent
{
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationBadgeViewComponent(INotificationService notificationService, UserManager<ApplicationUser> userManager)
    {
        _notificationService = notificationService;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return View(0);
        }

        var userId = _userManager.GetUserId((System.Security.Claims.ClaimsPrincipal)User);
        if (string.IsNullOrEmpty(userId))
        {
            return View(0);
        }

        var count = await _notificationService.GetUnreadCountAsync(userId);
        return View(count);
    }
}
