using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Web.Controllers;

[Authorize(Roles = "Citizen")]
public class AutoFillController : Controller
{
    private readonly IAutoFillService _autoFillService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AutoFillController(IAutoFillService autoFillService, UserManager<ApplicationUser> userManager)
    {
        _autoFillService = autoFillService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var profiles = await _autoFillService.GetAllProfilesAsync(_userManager.GetUserId(User)!);
        var list = profiles.ToList();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile(string type = "NationalID")
    {
        var result = await _autoFillService.GetAutoFillDataAsync(_userManager.GetUserId(User)!, type);
        if (result == null) return NotFound();
        return Json(result);
    }

    [HttpGet] public IActionResult SaveProfile() => View(new DocumentProfileDto());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(DocumentProfileDto model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await _autoFillService.SaveProfileAsync(model, _userManager.GetUserId(User)!);
            TempData["Success"] = "Document profile saved.";
            return RedirectToAction("Index");
        }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); return View(model); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> UploadId(OcrUploadRequest model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var userId = User.Identity?.IsAuthenticated == true
                ? _userManager.GetUserId(User)!
                : "temp";
            var result = await _autoFillService.ExtractFromOcrAsync(
                model.IdImage, model.DocumentType, userId);
            return Json(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _autoFillService.DeleteProfileAsync(id, _userManager.GetUserId(User)!); TempData["Success"] = "Profile deleted."; }
        catch (KeyNotFoundException) { TempData["Error"] = "Profile not found."; }
        return RedirectToAction("Index");
    }
}
