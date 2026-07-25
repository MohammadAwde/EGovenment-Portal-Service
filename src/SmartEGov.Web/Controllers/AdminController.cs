using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

using SmartEGov.Web.Hubs;

namespace SmartEGov.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly IDashboardService _dashboardService;
    private readonly IPublicHolidayRepository _holidays;
    private readonly IHubContext<SupportHub> _hub;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        IDashboardService dashboardService,
        IPublicHolidayRepository holidays,
        IHubContext<SupportHub> hub)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _dashboardService = dashboardService;
        _holidays = holidays;
        _hub = hub;
    }

    public async Task<IActionResult> Index()
    {
        var dashboard = await _dashboardService.GetAdminDashboardAsync();
        return View(dashboard);
    }

    public async Task<IActionResult> Users()
    {
        var users = _userManager.Users.ToList();
        var userRoles = new Dictionary<string, string>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userRoles[user.Id] = roles.FirstOrDefault() ?? "None";
        }
        ViewBag.UserRoles = userRoles;
        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);

        await _auditLogService.LogAsync(_userManager.GetUserId(User), "RoleChanged",
            "ApplicationUser", userId, string.Join(",", currentRoles), role);

        return RedirectToAction("Users");
    }

    public async Task<IActionResult> GovernmentServices()
    {
        var services = await _unitOfWork.GovernmentServices.GetAllAsync();
        return View(services);
    }

    [HttpGet]
    public async Task<IActionResult> CreateService()
    {
        var workflows = await _unitOfWork.ApprovalWorkflows.GetActiveWorkflowsAsync();
        ViewBag.Workflows = workflows;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateService(GovernmentServiceDto model)
    {
        if (!ModelState.IsValid)
        {
            var workflows = await _unitOfWork.ApprovalWorkflows.GetActiveWorkflowsAsync();
            ViewBag.Workflows = workflows;
            return View(model);
        }

        var service = new GovernmentService
        {
            Name = model.Name,
            Description = model.Description,
            Category = model.Category,
            Fee = model.Fee,
            EstimatedDays = model.EstimatedDays,
            SlotDurationMinutes = model.SlotDurationMinutes,
            IsActive = model.IsActive,
            ApprovalWorkflowId = model.ApprovalWorkflowId
        };

        await _unitOfWork.GovernmentServices.AddAsync(service);
        await _unitOfWork.SaveChangesAsync();

        return RedirectToAction("GovernmentServices");
    }

    [HttpGet]
    public async Task<IActionResult> EditService(int id)
    {
        var service = await _unitOfWork.GovernmentServices.GetByIdAsync(id);
        if (service == null) return NotFound();

        var dto = new GovernmentServiceDto
        {
            Id = service.Id,
            Name = service.Name,
            Description = service.Description,
            Category = service.Category,
            Fee = service.Fee,
            EstimatedDays = service.EstimatedDays,
            SlotDurationMinutes = service.SlotDurationMinutes,
            IsActive = service.IsActive,
            ApprovalWorkflowId = service.ApprovalWorkflowId
        };

        var workflows = await _unitOfWork.ApprovalWorkflows.GetActiveWorkflowsAsync();
        ViewBag.Workflows = workflows;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditService(GovernmentServiceDto model)
    {
        if (!ModelState.IsValid)
        {
            var workflows = await _unitOfWork.ApprovalWorkflows.GetActiveWorkflowsAsync();
            ViewBag.Workflows = workflows;
            return View(model);
        }

        var service = await _unitOfWork.GovernmentServices.GetByIdAsync(model.Id);
        if (service == null) return NotFound();

        service.Name = model.Name;
        service.Description = model.Description;
        service.Category = model.Category;
        service.Fee = model.Fee;
        service.EstimatedDays = model.EstimatedDays;
        service.SlotDurationMinutes = model.SlotDurationMinutes;
        service.IsActive = model.IsActive;
        service.ApprovalWorkflowId = model.ApprovalWorkflowId;

        _unitOfWork.GovernmentServices.Update(service);
        await _unitOfWork.SaveChangesAsync();

        return RedirectToAction("GovernmentServices");
    }

    public async Task<IActionResult> AuditLogs()
    {
        var logs = await _auditLogService.GetAllAsync();
        return View(logs);
    }

    public async Task<IActionResult> OfficerAssignments()
    {
        var officers = await _userManager.GetUsersInRoleAsync("Officer");
        var services = await _unitOfWork.GovernmentServices.GetAllAsync();
        var assignments = await _unitOfWork.OfficerServiceAssignments.GetAllWithDetailsAsync();

        ViewBag.Officers = officers;
        ViewBag.Services = services;
        return View(assignments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignOfficerToService(string officerId, int governmentServiceId)
    {
        var existingAssignments = await _unitOfWork.OfficerServiceAssignments
            .GetByOfficerIdAsync(officerId);

        var officer = await _userManager.FindByIdAsync(officerId);
        var service = await _unitOfWork.GovernmentServices.GetByIdAsync(governmentServiceId);
        string? previousServiceName = null;

        foreach (var old in existingAssignments)
        {
            if (old.GovernmentServiceId == governmentServiceId)
            {
                TempData["Error"] = "This officer is already assigned to this service.";
                return RedirectToAction("OfficerAssignments");
            }

            previousServiceName = old.GovernmentService?.Name;
            _unitOfWork.OfficerServiceAssignments.Remove(old);
        }

        var assignment = new OfficerServiceAssignment
        {
            OfficerId = officerId,
            GovernmentServiceId = governmentServiceId
        };

        await _unitOfWork.OfficerServiceAssignments.AddAsync(assignment);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(_userManager.GetUserId(User), "AssignedOfficer",
            "OfficerServiceAssignment", assignment.Id.ToString(),
            previousServiceName,
            $"Assigned {officer?.FullName} to {service?.Name}");

        var message = previousServiceName != null
            ? $"Officer reassigned from '{previousServiceName}' to '{service?.Name}' successfully."
            : "Officer assigned to service successfully.";
        TempData["Success"] = message;
        return RedirectToAction("OfficerAssignments");
    }
    [HttpGet]
    public async Task<IActionResult> ServiceCenters()
    {
        var centers = await _unitOfWork.ServiceCenters.GetAllAsync();
        return View(centers);
    }

    [HttpGet]
    public async Task<IActionResult> EditCenter(int id)
    {
        var center = await _unitOfWork.ServiceCenters.GetByIdWithSchedulesAsync(id);
        if (center == null) return NotFound();

        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var day in weekdays)
        {
            if (!center.DaySchedules.Any(s => s.DayOfWeek == day))
            {
                center.DaySchedules.Add(new ServiceCenterDaySchedule
                {
                    ServiceCenterId = center.Id,
                    DayOfWeek = day,
                    OpenTime = center.WorkingHoursStart,
                    CloseTime = day == DayOfWeek.Friday
                        ? (center.FridayClosingOverride ?? new TimeSpan(12, 0, 0))
                        : center.WorkingHoursEnd,
                    BreakStart = center.LunchBreakStart,
                    BreakEnd = center.LunchBreakEnd
                });
            }
        }
        await _unitOfWork.SaveChangesAsync();

        center.DaySchedules = center.DaySchedules.OrderBy(s => s.DayOfWeek == DayOfWeek.Monday ? 0
            : s.DayOfWeek == DayOfWeek.Tuesday ? 1
            : s.DayOfWeek == DayOfWeek.Wednesday ? 2
            : s.DayOfWeek == DayOfWeek.Thursday ? 3 : 4).ToList();

        return View(center);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCenter(ServiceCenter model, List<ServiceCenterDaySchedule> daySchedules)
    {
        var center = await _unitOfWork.ServiceCenters.GetByIdAsync(model.Id);
        if (center == null) return NotFound();

        center.WorkingHoursStart = model.WorkingHoursStart;
        center.WorkingHoursEnd = model.WorkingHoursEnd;
        center.LunchBreakStart = model.LunchBreakStart;
        center.LunchBreakEnd = model.LunchBreakEnd;

        foreach (var incoming in daySchedules)
        {
            var existing = await _unitOfWork.ServiceCenterDaySchedules.GetByIdAsync(incoming.Id);
            if (existing != null)
            {
                existing.OpenTime = incoming.OpenTime;
                existing.CloseTime = incoming.CloseTime;
                existing.BreakStart = incoming.BreakStart;
                existing.BreakEnd = incoming.BreakEnd;
                _unitOfWork.ServiceCenterDaySchedules.Update(existing);
            }
        }

        _unitOfWork.ServiceCenters.Update(center);
        await _unitOfWork.SaveChangesAsync();

        return RedirectToAction("ServiceCenters");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveOfficerAssignment(int id)
    {
        var assignment = await _unitOfWork.OfficerServiceAssignments.GetByIdAsync(id);
        if (assignment == null) return NotFound();

        _unitOfWork.OfficerServiceAssignments.Remove(assignment);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(_userManager.GetUserId(User), "RemovedOfficerAssignment",
            "OfficerServiceAssignment", id.ToString(), null, null);

        TempData["Success"] = "Officer assignment removed.";
        return RedirectToAction("OfficerAssignments");
    }
    public async Task<IActionResult> Appointments()
    {
        var all = await _unitOfWork.Appointments.GetAllAsync();

        var dtos = all.Select(a => new AppointmentDto
        {
            Id = a.Id,
            UserId = a.UserId,
            ServiceCenterId = a.ServiceCenterId,
            GovernmentServiceId = a.GovernmentServiceId,
            AppointmentDate = a.AppointmentDate,
            TimeSlot = a.TimeSlot,
            Status = a.Status,
            ReferenceNumber = a.ReferenceNumber,
            CreatedAt = a.CreatedAt,
            ServiceCenterName = a.ServiceCenter?.CenterName,
            ServiceCenterAddress = a.ServiceCenter?.Address,
            GovernmentServiceName = a.GovernmentService?.Name
        });
        return View(dtos);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAppointmentCompleted(int id)
    {
        var a = await _unitOfWork.Appointments.GetByIdAsync(id);
        if (a != null)
        {
            if (a.AppointmentDate.Date > DateTime.Today)
            {
                TempData["Error"] = "Cannot mark a future appointment as completed.";
                return RedirectToAction("Appointments");
            }
            a.Status = "Completed";
            _unitOfWork.Appointments.Update(a);
            await _unitOfWork.SaveChangesAsync();
        }
        TempData["Success"] = "Appointment marked as completed.";
        return RedirectToAction("Appointments");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAppointmentNoShow(int id)
    {
        var a = await _unitOfWork.Appointments.GetByIdAsync(id);
        if (a != null)
        {
            if (a.AppointmentDate.Date > DateTime.Today)
            {
                TempData["Error"] = "Cannot mark a future appointment as no-show.";
                return RedirectToAction("Appointments");
            }
            a.Status = "No-Show";
            _unitOfWork.Appointments.Update(a);
            await _unitOfWork.SaveChangesAsync();
        }
        TempData["Success"] = "Appointment marked as no-show.";
        return RedirectToAction("Appointments");
    }
    [HttpGet]
    public async Task<IActionResult> Holidays()
    {
        var holidays = await _holidays.GetAllAsync();
        return View(holidays);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddHoliday(string name, string nameAr,
        int day, int month, int? year)
    {
        await _holidays.AddAsync(new SmartEGov.Domain.Entities.PublicHoliday
        {
            Name = name,
            NameAr = nameAr,
            Day = day,
            Month = month,
            Year = year,
            IsActive = true
        });
        await _unitOfWork.SaveChangesAsync();
        TempData["Success"] = "Holiday added.";
        return RedirectToAction("Holidays");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHoliday(int id)
    {
        var h = await _holidays.GetByIdAsync(id);
        if (h != null)
        {
            _holidays.Remove(h);
            await _unitOfWork.SaveChangesAsync();
            TempData["Success"] = "Holiday deleted.";
        }
        return RedirectToAction("Holidays");
    }

    [HttpGet]
    public async Task<IActionResult> ExportAppointmentsCsv()
    {
        var all = await _unitOfWork.Appointments.GetAllAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Reference,Citizen,Service Center,Service,Date,Slot,Status,Queue #");

        foreach (var a in all.OrderByDescending(x => x.AppointmentDate))
        {
            sb.AppendLine(string.Join(",",
                Escape(a.ReferenceNumber),
                Escape(a.User?.FullName ?? ""),
                Escape(a.ServiceCenter?.CenterName ?? ""),
                Escape(a.GovernmentService?.Name ?? ""),
                a.AppointmentDate.ToString("yyyy-MM-dd"),
                Escape(a.TimeSlot),
                Escape(a.Status),
                a.QueueNumber));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"appointments_{DateTime.Now:yyyyMMdd}.csv");
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHoliday(int id)
    {
        var h = await _holidays.GetByIdAsync(id);
        if (h != null)
        {
            h.IsActive = !h.IsActive;
            _holidays.Update(h);
            await _unitOfWork.SaveChangesAsync();
        }
        return RedirectToAction("Holidays");
    }
    [HttpGet]
    public async Task<IActionResult> WorkingHours()
    {
        var all = (await _unitOfWork.WeekdaySchedules.GetAllAsync()).ToList();

        // Remove duplicates: keep only the highest Id (most recently saved) per day
        var duplicates = all
            .GroupBy(s => s.DayOfWeek)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.OrderBy(s => s.Id).SkipLast(1));

        foreach (var dup in duplicates)
        {
            _unitOfWork.WeekdaySchedules.Remove(dup);
        }
        if (duplicates.Any())
            await _unitOfWork.SaveChangesAsync();

        var schedules = all.GroupBy(s => s.DayOfWeek).Select(g => g.OrderBy(s => s.Id).Last()).ToList();

        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var day in weekdays)
        {
            if (!schedules.Any(s => s.DayOfWeek == day))
            {
                var newDay = new WeekdaySchedule { DayOfWeek = day };
                await _unitOfWork.WeekdaySchedules.AddAsync(newDay);
                schedules.Add(newDay);
            }
        }
        await _unitOfWork.SaveChangesAsync();

        // Return the view with the schedules as model
        return View(schedules.OrderBy(s => s.DayOfWeek).ToList());
    }

        [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WorkingHours(List<WeekdaySchedule> schedules)
    {
        foreach (var incoming in schedules)
        {
            var existing = await _unitOfWork.WeekdaySchedules.GetByIdAsync(incoming.Id);
            if (existing != null)
            {
                existing.OpenTime = incoming.OpenTime;
                existing.CloseTime = incoming.CloseTime;
                existing.BreakStart = incoming.BreakStart;
                existing.BreakEnd = incoming.BreakEnd;
                _unitOfWork.WeekdaySchedules.Update(existing);
            }
        }
        await _unitOfWork.SaveChangesAsync();
        TempData["Success"] = "Working hours updated.";
        return RedirectToAction("WorkingHours");
    }
}