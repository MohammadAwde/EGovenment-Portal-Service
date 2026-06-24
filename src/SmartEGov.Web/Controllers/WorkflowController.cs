using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Web.Controllers;

[Authorize(Roles = "Officer,Admin")]
public class WorkflowController : Controller
{
    private readonly IWorkflowService _workflowService;
    private readonly IServiceRequestService _serviceRequestService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkflowController(
        IWorkflowService workflowService,
        IServiceRequestService serviceRequestService,
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager)
    {
        _workflowService = workflowService;
        _serviceRequestService = serviceRequestService;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<IActionResult> Steps(int serviceRequestId)
    {
        var request = await _serviceRequestService.GetByIdAsync(serviceRequestId);
        if (request == null) return NotFound();

        var steps = await _workflowService.GetStepsByRequestIdAsync(serviceRequestId);
        var currentStep = await _workflowService.GetCurrentStepAsync(serviceRequestId);
        var allowedTransitions = _workflowService.GetAllowedTransitions(request.Status.ToString());

        ViewBag.ServiceRequestId = serviceRequestId;
        ViewBag.ReferenceNumber = request.ReferenceNumber;
        ViewBag.RequestStatus = request.Status.ToString();
        ViewBag.CurrentStepId = currentStep?.Id;
        ViewBag.AllowedTransitions = allowedTransitions;

        return View(steps);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int stepId, int serviceRequestId, string? comments)
    {
        try
        {
            var officerId = _userManager.GetUserId(User)!;
            await _workflowService.ApproveStepAsync(stepId, officerId, comments);
            TempData["Success"] = "Step approved successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (KeyNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Steps", new { serviceRequestId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int stepId, int serviceRequestId, string? comments)
    {
        try
        {
            var officerId = _userManager.GetUserId(User)!;
            await _workflowService.RejectStepAsync(stepId, officerId, comments);
            TempData["Success"] = "Step rejected. The service request has been rejected.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (KeyNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Steps", new { serviceRequestId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InitializeWorkflow(int serviceRequestId, int workflowId)
    {
        try
        {
            await _workflowService.InitializeWorkflowAsync(serviceRequestId, workflowId);
            TempData["Success"] = "Workflow initialized successfully.";
        }
        catch (KeyNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Steps", new { serviceRequestId });
    }
}
