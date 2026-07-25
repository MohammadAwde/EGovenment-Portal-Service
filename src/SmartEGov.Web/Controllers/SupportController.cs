using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using Twilio.TwiML.Messaging;

namespace SmartEGov.Web.Controllers;

[Authorize]
public class SupportController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<SmartEGov.Web.Hubs.SupportHub> _hub;

    public SupportController(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager, IHubContext<SmartEGov.Web.Hubs.SupportHub> hub)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var messages = await _unitOfWork.SupportMessages.GetByUserIdAsync(userId);
        return View(messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string subject, string message)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] = "Please fill in both the subject and the message.";
            return RedirectToAction("Index");
        }

        var user = await _userManager.GetUserAsync(User);

        var msg = new SupportMessage
        {
            CitizenUserId = user!.Id,
            CitizenName = user.FullName ?? user.Email ?? "Unknown",
            Subject = subject,
            Message = message,
            Status = "Open"
        };

        await _unitOfWork.SupportMessages.AddAsync(msg);
        await _unitOfWork.SaveChangesAsync();

        TempData["Success"] = "Your question has been sent.";
        return RedirectToAction("Index");
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    
    
    public async Task<IActionResult> AddReply(int messageId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return RedirectToAction("Index");
        var user = await _userManager.GetUserAsync(User);
        var msg = await _unitOfWork.SupportMessages.GetByIdWithRepliesAsync(messageId);
        if (msg == null || msg.CitizenUserId != user!.Id) return RedirectToAction("Index");
        var reply = new SupportReply
        {
            SupportMessageId = messageId,
            SenderUserId = user.Id,
            SenderName = user.FullName ?? user.Email ?? "Citizen",
            IsFromAdmin = false,
            Text = text
        };
        await _unitOfWork.SupportReplies.AddAsync(reply);
        msg.Status = "Open";
        _unitOfWork.SupportMessages.Update(msg);
        await _unitOfWork.SaveChangesAsync();
        return RedirectToAction("Details", new { id = messageId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var msg = await _unitOfWork.SupportMessages.GetByIdWithRepliesAsync(id);
        if (msg == null || msg.CitizenUserId != userId) return NotFound();
        return View(msg);
    }
}