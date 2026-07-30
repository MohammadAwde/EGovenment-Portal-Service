using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Web.Controllers;

[Authorize(Roles = "Citizen")]
public class CitizenController : Controller
{
    private readonly ICitizenService _citizenService;
    private readonly IDashboardService _dashboardService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CitizenController(
        ICitizenService citizenService,
        IDashboardService dashboardService,
        UserManager<ApplicationUser> userManager)
    {
        _citizenService = citizenService;
        _dashboardService = dashboardService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Dashboard()
    {
        var userId = _userManager.GetUserId(User)!;
        var citizen = await _citizenService.GetByUserIdAsync(userId);
        if (citizen == null) return RedirectToAction("Create");

        var dashboard = await _dashboardService.GetCitizenDashboardAsync(citizen.Id, userId);
        return View(dashboard);
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        var citizen = await _citizenService.GetByUserIdAsync(userId!);
        if (citizen == null) return RedirectToAction("Create");
        return View(citizen);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var user = await _userManager.GetUserAsync(User);
        var dto = new CitizenDto();
       

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CitizenDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var userId = _userManager.GetUserId(User);
        model.UserId = userId;

        try
        {
            await _citizenService.CreateAsync(model);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("NationalId", ex.Message);
            return View(model);
        }
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var userId = _userManager.GetUserId(User);
        var citizen = await _citizenService.GetByUserIdAsync(userId!);
        if (citizen == null) return RedirectToAction("Create");
        return View(citizen);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CitizenDto model)
    {
        if (!ModelState.IsValid) return View(model);

        await _citizenService.UpdateAsync(model);
        return RedirectToAction("Index");
    }
}
