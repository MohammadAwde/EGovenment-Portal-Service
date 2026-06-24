using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;
using SmartEGov.Domain.Enums;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    private static readonly Dictionary<ServiceRequestStatus, ServiceRequestStatus[]> AllowedTransitions = new()
    {
        [ServiceRequestStatus.Submitted] = new[] { ServiceRequestStatus.UnderReview, ServiceRequestStatus.PendingPayment, ServiceRequestStatus.PendingInformation, ServiceRequestStatus.Cancelled },
        [ServiceRequestStatus.UnderReview] = new[] { ServiceRequestStatus.Approved, ServiceRequestStatus.Rejected, ServiceRequestStatus.PendingInformation },
        [ServiceRequestStatus.PendingInformation] = new[] { ServiceRequestStatus.UnderReview, ServiceRequestStatus.Cancelled },
        [ServiceRequestStatus.PendingPayment] = new[] { ServiceRequestStatus.UnderReview, ServiceRequestStatus.Completed, ServiceRequestStatus.Cancelled },
        [ServiceRequestStatus.Approved] = new[] { ServiceRequestStatus.Completed },
        [ServiceRequestStatus.Rejected] = Array.Empty<ServiceRequestStatus>(),
        [ServiceRequestStatus.Completed] = Array.Empty<ServiceRequestStatus>(),
        [ServiceRequestStatus.Cancelled] = Array.Empty<ServiceRequestStatus>()
    };

    public WorkflowService(IUnitOfWork unitOfWork, INotificationService notificationService, ApplicationDbContext context, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ApprovalStepDto>> GetStepsByRequestIdAsync(int serviceRequestId)
    {
        var steps = await _context.ApprovalSteps
            .Include(s => s.Officer)
            .Where(s => s.ServiceRequestId == serviceRequestId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        return _mapper.Map<IEnumerable<ApprovalStepDto>>(steps);
    }

    public async Task<ApprovalStepDto?> GetCurrentStepAsync(int serviceRequestId)
    {
        var step = await _context.ApprovalSteps
            .Include(s => s.Officer)
            .Where(s => s.ServiceRequestId == serviceRequestId && s.Status == ApprovalStatus.Pending)
            .OrderBy(s => s.StepOrder)
            .FirstOrDefaultAsync();

        return step == null ? null : _mapper.Map<ApprovalStepDto>(step);
    }

    public async Task ApproveStepAsync(int stepId, string officerId, string? comments)
    {
        var step = await _context.ApprovalSteps
            .FirstOrDefaultAsync(s => s.Id == stepId);

        if (step == null) throw new KeyNotFoundException("Approval step not found.");
        if (step.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException("This step has already been processed.");

        var officer = await _context.Users.FindAsync(officerId);
        if (officer == null) throw new KeyNotFoundException("Officer not found.");
        if (string.IsNullOrWhiteSpace(officer.DigitalSignaturePath))
            throw new InvalidOperationException("You must upload your digital signature before approving requests. Please go to My Signature to set it up.");

        var hasPendingPriorSteps = await _context.ApprovalSteps
            .AnyAsync(s => s.ServiceRequestId == step.ServiceRequestId
                        && s.StepOrder < step.StepOrder
                        && s.Status != ApprovalStatus.Approved);

        if (hasPendingPriorSteps)
            throw new InvalidOperationException("Previous steps must be completed before this step can be approved.");

        // Use EF optimistic concurrency with RowVersion to prevent concurrent processing
        step.Status = ApprovalStatus.Approved;
        step.OfficerId = officerId;
        step.Comments = comments;
        step.OfficerSignaturePath = officer.DigitalSignaturePath;
        step.ActionDate = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("This step has already been processed or locked by another officer.");
        }

        var allApproved = !await _context.ApprovalSteps
            .AnyAsync(s => s.ServiceRequestId == step.ServiceRequestId && s.Status != ApprovalStatus.Approved);

        var request = await _unitOfWork.ServiceRequests.GetWithDetailsAsync(step.ServiceRequestId);
        if (request != null)
        {
            if (allApproved)
            {
                request.Status = ServiceRequestStatus.Approved;
                _unitOfWork.ServiceRequests.Update(request);
                await _unitOfWork.SaveChangesAsync();

                await _notificationService.SendAsync(request.Citizen.UserId, "Request Approved",
                    $"Your service request {request.ReferenceNumber} has been fully approved through all workflow steps.");
            }
            else
            {
                var nextStep = await _context.ApprovalSteps
                    .Where(s => s.ServiceRequestId == step.ServiceRequestId && s.Status == ApprovalStatus.Pending)
                    .OrderBy(s => s.StepOrder)
                    .FirstOrDefaultAsync();

                if (nextStep != null)
                {
                    await _notificationService.SendAsync(request.Citizen.UserId, "Step Approved",
                        $"Step '{step.StepName}' of your request {request.ReferenceNumber} has been approved. Next: '{nextStep.StepName}'.");
                }
            }
        }
    }

    public async Task RejectStepAsync(int stepId, string officerId, string? comments)
    {
        var step = await _context.ApprovalSteps
            .FirstOrDefaultAsync(s => s.Id == stepId);

        if (step == null) throw new KeyNotFoundException("Approval step not found.");
        if (step.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException("This step has already been processed.");

        var officer = await _context.Users.FindAsync(officerId);
        if (officer == null) throw new KeyNotFoundException("Officer not found.");
        if (string.IsNullOrWhiteSpace(officer.DigitalSignaturePath))
            throw new InvalidOperationException("You must upload your digital signature before rejecting requests. Please go to My Signature to set it up.");

        // Use EF optimistic concurrency with RowVersion to prevent concurrent processing
        step.Status = ApprovalStatus.Rejected;
        step.OfficerId = officerId;
        step.Comments = comments;
        step.OfficerSignaturePath = officer.DigitalSignaturePath;
        step.ActionDate = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("This step has already been processed or locked by another officer.");
        }

        var request = await _unitOfWork.ServiceRequests.GetWithDetailsAsync(step.ServiceRequestId);
        if (request != null)
        {
            request.Status = ServiceRequestStatus.Rejected;
            request.CompletedAt = DateTime.UtcNow;
            _unitOfWork.ServiceRequests.Update(request);

            await _notificationService.SendAsync(request.Citizen.UserId, "Request Rejected",
                $"Your service request {request.ReferenceNumber} was rejected at step '{step.StepName}'." +
                (string.IsNullOrEmpty(comments) ? "" : $" Reason: {comments}"));
        }

        await _context.SaveChangesAsync();
    }

    public async Task InitializeWorkflowAsync(int serviceRequestId, int workflowId)
    {
        var workflow = await _unitOfWork.ApprovalWorkflows.GetByIdAsync(workflowId);
        if (workflow == null) throw new KeyNotFoundException("Workflow not found.");

        var request = await _unitOfWork.ServiceRequests.GetByIdAsync(serviceRequestId);
        if (request == null) throw new KeyNotFoundException("Service request not found.");

        for (int i = 1; i <= workflow.TotalSteps; i++)
        {
            var step = new ApprovalStep
            {
                StepOrder = i,
                StepName = $"{workflow.Name} - Step {i}",
                Status = ApprovalStatus.Pending,
                ServiceRequestId = serviceRequestId
            };

            await _context.ApprovalSteps.AddAsync(step);
        }

        request.Status = ServiceRequestStatus.UnderReview;
        _unitOfWork.ServiceRequests.Update(request);
        await _context.SaveChangesAsync();
    }

    public bool IsValidTransition(string currentStatus, string newStatus)
    {
        if (!Enum.TryParse<ServiceRequestStatus>(currentStatus, out var current)) return false;
        if (!Enum.TryParse<ServiceRequestStatus>(newStatus, out var target)) return false;

        return AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(target);
    }

    public IEnumerable<string> GetAllowedTransitions(string currentStatus)
    {
        if (!Enum.TryParse<ServiceRequestStatus>(currentStatus, out var current))
            return Enumerable.Empty<string>();

        return AllowedTransitions.TryGetValue(current, out var allowed)
            ? allowed.Select(s => s.ToString())
            : Enumerable.Empty<string>();
    }
}
