using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Web.Controllers;

[Authorize]
public class NotificationController : Controller
{
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationController(INotificationService notificationService, UserManager<ApplicationUser> userManager)
    {
        _notificationService = notificationService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var notifications = await _notificationService.GetByUserIdAsync(userId);
        return View(notifications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _notificationService.MarkAsReadAsync(id);
        // If the request was made by htmx, return 204 No Content so the client can remove the element
        if (Request.Headers.TryGetValue("HX-Request", out var vals) && vals.Count > 0 && vals[0] == "true")
        {
            return NoContent();
        }

        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Json(0);
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Json(count);
    }
}
