using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Web.Controllers;

[Authorize(Roles = "Officer")]
public class OfficerController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuditLogService _auditLogService;

    public OfficerController(
        IDashboardService dashboardService,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment environment,
        IAuditLogService auditLogService)
    {
        _dashboardService = dashboardService;
        _userManager = userManager;
        _environment = environment;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Dashboard()
    {
        var officerId = _userManager.GetUserId(User)!;
        var dashboard = await _dashboardService.GetOfficerDashboardAsync(officerId);
        return View(dashboard);
    }

    public async Task<IActionResult> Signature()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        ViewBag.CurrentSignaturePath = user.DigitalSignaturePath;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadSignature(IFormFile? signatureFile, string? signatureData)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var signaturesFolder = Path.Combine(_environment.WebRootPath, "uploads", "signatures");
        Directory.CreateDirectory(signaturesFolder);

        // Remove old signature file if exists
        if (!string.IsNullOrWhiteSpace(user.DigitalSignaturePath))
        {
            var oldPath = Path.Combine(_environment.WebRootPath, user.DigitalSignaturePath.TrimStart('/'));
            if (System.IO.File.Exists(oldPath))
            {
                System.IO.File.Delete(oldPath);
            }
        }

        string relativePath;

        if (signatureFile != null && signatureFile.Length > 0)
        {
            // File upload mode
            var extension = Path.GetExtension(signatureFile.FileName).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
            {
                TempData["Error"] = "Only PNG and JPEG images are allowed for signatures.";
                ViewBag.CurrentSignaturePath = user.DigitalSignaturePath;
                return View("Signature");
            }

            if (signatureFile.Length > 2 * 1024 * 1024)
            {
                TempData["Error"] = "Signature image must be less than 2 MB.";
                ViewBag.CurrentSignaturePath = user.DigitalSignaturePath;
                return View("Signature");
            }

            var fileName = $"{user.Id}_signature_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(signaturesFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await signatureFile.CopyToAsync(stream);
            }

            relativePath = $"/uploads/signatures/{fileName}";
        }
        else if (!string.IsNullOrWhiteSpace(signatureData))
        {
            // Canvas drawing mode — signatureData is a base64 data URL
            var base64Data = signatureData;
            if (base64Data.Contains(','))
            {
                base64Data = base64Data[(base64Data.IndexOf(',') + 1)..];
            }

            var imageBytes = Convert.FromBase64String(base64Data);
            var fileName = $"{user.Id}_signature_{Guid.NewGuid():N}.png";
            var filePath = Path.Combine(signaturesFolder, fileName);

            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

            relativePath = $"/uploads/signatures/{fileName}";
        }
        else
        {
            TempData["Error"] = "Please draw or upload a signature.";
            ViewBag.CurrentSignaturePath = user.DigitalSignaturePath;
            return View("Signature");
        }

        user.DigitalSignaturePath = relativePath;
        await _userManager.UpdateAsync(user);

        await _auditLogService.LogAsync(
            user.Id,
            "DigitalSignatureUpdated",
            "ApplicationUser",
            user.Id,
            null,
            "Officer updated digital signature"
        );

        TempData["Success"] = "Digital signature saved successfully.";
        return RedirectToAction("Signature");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSignature()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(user.DigitalSignaturePath))
        {
            var fullPath = Path.Combine(_environment.WebRootPath, user.DigitalSignaturePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }

            user.DigitalSignaturePath = null;
            await _userManager.UpdateAsync(user);

            await _auditLogService.LogAsync(
                user.Id,
                "DigitalSignatureRemoved",
                "ApplicationUser",
                user.Id,
                null,
                "Officer removed digital signature"
            );
        }

        TempData["Success"] = "Digital signature removed.";
        return RedirectToAction("Signature");
    }
}
