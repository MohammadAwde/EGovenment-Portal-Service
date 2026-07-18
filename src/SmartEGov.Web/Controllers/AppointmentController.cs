using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Services;


namespace SmartEGov.Web.Controllers;

[Authorize(Roles = "Citizen")]
public class AppointmentController : Controller
{
    private readonly IAppointmentService _appointmentService;
    private readonly ICitizenService _citizenService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public AppointmentController(IAppointmentService appointmentService, ICitizenService citizenService, UserManager<ApplicationUser> userManager, IUnitOfWork unitOfWork)
    {
        _appointmentService = appointmentService;
        _citizenService = citizenService;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index()
    {
        var citizen = await _citizenService.GetByUserIdAsync(_userManager.GetUserId(User)!);
        if (citizen == null) return RedirectToAction("Create", "Citizen");
        return View(await _appointmentService.GetByUserAsync(_userManager.GetUserId(User)!));
    }

    [HttpGet]
    public async Task<IActionResult> Book()
    {
        var citizen = await _citizenService.GetByUserIdAsync(_userManager.GetUserId(User)!);
        if (citizen == null) return RedirectToAction("Create", "Citizen");
        ViewBag.Centers = await _appointmentService.GetAllCentersAsync();
        ViewBag.Services = await _unitOfWork.GovernmentServices.GetActiveServicesAsync();
        return View(new BookAppointmentRequest());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookAppointmentRequest model)
    {
        if (!ModelState.IsValid) { ViewBag.Centers = await _appointmentService.GetAllCentersAsync(); return View(model); }
        try
        {
            var appt = await _appointmentService.BookAsync(model, _userManager.GetUserId(User)!);
            TempData["Success"] = $"Appointment confirmed! Reference: {appt.ReferenceNumber}";
            return RedirectToAction("Index");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Centers = await _appointmentService.GetAllCentersAsync();
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        try { await _appointmentService.CancelAsync(id, _userManager.GetUserId(User)!); TempData["Success"] = "Appointment cancelled."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Reschedule(int id)
    {
        var appt = await _appointmentService.GetByIdAsync(id, _userManager.GetUserId(User)!);
        if (appt == null) return NotFound();
        return View(appt);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, DateTime newDate, string newSlot)
    {
        try
        {
            await _appointmentService.RescheduleAsync(id, newDate, newSlot, _userManager.GetUserId(User)!);
            TempData["Success"] = "Appointment rescheduled.";
            return RedirectToAction("Index");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return View(await _appointmentService.GetByIdAsync(id, _userManager.GetUserId(User)!));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetNearestCenters(double lat, double lng, int radiusKm = 20)
        => Json(await _appointmentService.GetNearestCentersAsync(lat, lng, radiusKm));

    [HttpGet]
    public async Task<IActionResult> GetSlots(int centerId, DateTime date)
        => Json(await _appointmentService.GetAvailableSlotsAsync(centerId, date));
}
