using AutoMapper;
using SmartEGov.Application.DTOs;
using Microsoft.Extensions.Logging;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using Microsoft.AspNetCore.Http;
using SmartEGov.Domain.Entities;
using SmartEGov.Domain.Enums;

namespace SmartEGov.Infrastructure.Services;

public class ServiceRequestService : IServiceRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly IWorkflowService _workflowService;
    private readonly IMapper _mapper;
    private readonly ILogger<ServiceRequestService> _logger;
    private readonly IDocumentService _documentService;

    public ServiceRequestService(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IAuditLogService auditLogService,
        IWorkflowService workflowService,
        IMapper mapper,
        IDocumentService documentService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _workflowService = workflowService;
        _mapper = mapper;
        _documentService = documentService;
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(int id)
    {
        var request = await _unitOfWork.ServiceRequests.GetWithDetailsAsync(id);
        return request == null ? null : _mapper.Map<ServiceRequestDto>(request);
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetAllAsync()
    {
        var requests = await _unitOfWork.ServiceRequests.GetAllAsync();
        return _mapper.Map<IEnumerable<ServiceRequestDto>>(requests);
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetByCitizenIdAsync(int citizenId)
    {
        var requests = await _unitOfWork.ServiceRequests.GetByCitizenIdAsync(citizenId);
        return _mapper.Map<IEnumerable<ServiceRequestDto>>(requests);
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetPendingRequestsAsync()
    {
        var requests = await _unitOfWork.ServiceRequests.GetPendingRequestsAsync();
        return _mapper.Map<IEnumerable<ServiceRequestDto>>(requests);
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetPendingRequestsByOfficerAsync(string officerId)
    {
        // Only return pending requests for services this officer is explicitly assigned to.
        var assignments = await _unitOfWork.OfficerServiceAssignments.GetByOfficerIdAsync(officerId);
        var serviceIds = assignments.Select(a => a.GovernmentServiceId).ToList();

        if (!serviceIds.Any())
            return Enumerable.Empty<ServiceRequestDto>();

        var requests = await _unitOfWork.ServiceRequests.GetPendingByServiceIdsAsync(serviceIds);
        return _mapper.Map<IEnumerable<ServiceRequestDto>>(requests);
    }

    public async Task<ServiceRequestDto> CreateAsync(ServiceRequestDto dto)
    {
        _logger?.LogInformation("CreateAsync called for CitizenId={CitizenId}, GovernmentServiceId={GovernmentServiceId}", dto.CitizenId, dto.GovernmentServiceId);

        // Duplicate detection: if user submitted same service within last 24 hours, block
        var since = DateTime.UtcNow.AddHours(-24);
        var recent = await _unitOfWork.ServiceRequests.GetRecentByCitizenAndServiceAsync(dto.CitizenId, dto.GovernmentServiceId, since);
        if (recent != null && recent.Any())
        {
            throw new InvalidOperationException("A similar service request was submitted recently. Please check your requests before submitting again.");
        }

        // This method creates a submitted request. Drafts should be created via SaveDraftAsync.
        var serviceRequest = new ServiceRequest
        {
            ReferenceNumber = GenerateReferenceNumber(),
            GovernmentServiceId = dto.GovernmentServiceId,
            CitizenId = dto.CitizenId,
            Notes = dto.Notes ?? string.Empty,
            Status = ServiceRequestStatus.Submitted,
            SubmittedAt = DateTime.UtcNow
        };

        await _unitOfWork.ServiceRequests.AddAsync(serviceRequest);
        await _unitOfWork.SaveChangesAsync();

        var citizen = await _unitOfWork.Citizens.GetByIdAsync(dto.CitizenId);
        if (citizen != null)
        {
            await _notificationService.SendAsync(citizen.UserId, "Request Submitted",
                $"Your service request {serviceRequest.ReferenceNumber} has been submitted successfully.");
        }

        await _auditLogService.LogAsync(citizen?.UserId, "Created", "ServiceRequest",
            serviceRequest.Id.ToString(), null, $"Reference: {serviceRequest.ReferenceNumber}");

        var govService = await _unitOfWork.GovernmentServices.GetWithWorkflowAsync(dto.GovernmentServiceId);
        if (govService?.ApprovalWorkflowId != null)
        {
            await _workflowService.InitializeWorkflowAsync(serviceRequest.Id, govService.ApprovalWorkflowId.Value);
        }

        dto.Id = serviceRequest.Id;
        dto.ReferenceNumber = serviceRequest.ReferenceNumber;
        dto.Status = serviceRequest.Status;
        dto.SubmittedAt = serviceRequest.SubmittedAt;
        return dto;
    }

    public async Task<ServiceRequestDto> SaveDraftAsync(ServiceRequestDto dto)
    {
        _logger?.LogInformation("SaveDraftAsync called for CitizenId={CitizenId}, GovernmentServiceId={GovernmentServiceId}", dto.CitizenId, dto.GovernmentServiceId);

        var serviceRequest = new ServiceRequest
        {
            ReferenceNumber = GenerateReferenceNumber(),
            GovernmentServiceId = dto.GovernmentServiceId,
            CitizenId = dto.CitizenId,
            Notes = dto.Notes ?? string.Empty,
            Status = ServiceRequestStatus.Draft,
            // The database has SubmittedAt as NOT NULL; set a placeholder timestamp for drafts so inserts succeed.
            // The Status property is the authoritative indicator of a draft vs submitted request.
            SubmittedAt = DateTime.UtcNow
        };

        await _unitOfWork.ServiceRequests.AddAsync(serviceRequest);
        await _unitOfWork.SaveChangesAsync();

        var citizen = await _unitOfWork.Citizens.GetByIdAsync(dto.CitizenId);
        await _auditLogService.LogAsync(citizen?.UserId, "DraftCreated", "ServiceRequest",
            serviceRequest.Id.ToString(), null, $"Draft request created: {serviceRequest.ReferenceNumber}");

        dto.Id = serviceRequest.Id;
        dto.ReferenceNumber = serviceRequest.ReferenceNumber;
        dto.Status = serviceRequest.Status;
        dto.SubmittedAt = serviceRequest.SubmittedAt;
        return dto;
    }

    public async Task<ServiceRequestDto> UpdateAsync(ServiceRequestDto dto)
    {
        var request = await _unitOfWork.ServiceRequests.GetWithDetailsAsync(dto.Id);
        if (request == null) throw new KeyNotFoundException("Service request not found.");

        var originalStatus = request.Status;

        // Only allow edits for drafts, submitted requests, or requests needing more information
        // Business rule: if request already progressed beyond these stages, editing core fields is restricted
        if (originalStatus != ServiceRequestStatus.Draft
            && originalStatus != ServiceRequestStatus.Submitted
            && originalStatus != ServiceRequestStatus.PendingInformation)
        {
            throw new InvalidOperationException("Only draft, submitted, or requests needing additional information can be edited.");
        }

        request.GovernmentServiceId = dto.GovernmentServiceId;
        request.Notes = dto.Notes ?? string.Empty;

        // If the caller changes status from Draft to Submitted, set SubmittedAt and initialize workflow
        if (originalStatus == ServiceRequestStatus.Draft && dto.Status == ServiceRequestStatus.Submitted)
        {
            request.Status = ServiceRequestStatus.Submitted;
            request.SubmittedAt = DateTime.UtcNow;

            // Initialize workflow if applicable
            var govService = await _unitOfWork.GovernmentServices.GetWithWorkflowAsync(request.GovernmentServiceId);
            if (govService?.ApprovalWorkflowId != null)
            {
                await _workflowService.InitializeWorkflowAsync(request.Id, govService.ApprovalWorkflowId.Value);
            }

            // Notify citizen
            var citizen = await _unitOfWork.Citizens.GetByIdAsync(request.CitizenId);
            if (citizen != null)
            {
                await _notificationService.SendAsync(citizen.UserId, "Request Submitted",
                    $"Your service request {request.ReferenceNumber} has been submitted successfully.");
            }

            await _auditLogService.LogAsync(citizen?.UserId, "Submitted", "ServiceRequest",
                request.Id.ToString(), ServiceRequestStatus.Draft.ToString(), ServiceRequestStatus.Submitted.ToString());
        }
        else
        {
            // For drafts or minor edits, just update
            request.Status = dto.Status;
            await _auditLogService.LogAsync(null, "Updated", "ServiceRequest",
                request.Id.ToString(), originalStatus.ToString(), request.Status.ToString());
        }

        _unitOfWork.ServiceRequests.Update(request);
        await _unitOfWork.SaveChangesAsync();

        // Map back
        var result = _mapper.Map<ServiceRequestDto>(request);
        return result;
    }

    public async Task UpdateStatusAsync(int id, string status, string? comments, string? completionFileName = null, string? completionFilePath = null)
    {
        var request = await _unitOfWork.ServiceRequests.GetWithDetailsAsync(id);
        if (request == null) throw new KeyNotFoundException("Service request not found.");

        var oldStatus = request.Status.ToString();

        if (!_workflowService.IsValidTransition(oldStatus, status))
        {
            var allowed = _workflowService.GetAllowedTransitions(oldStatus);
            throw new InvalidOperationException(
                $"Cannot transition from '{oldStatus}' to '{status}'. Allowed transitions: {string.Join(", ", allowed)}.");
        }

        request.Status = Enum.Parse<ServiceRequestStatus>(status);

        if (request.Status == ServiceRequestStatus.Completed)
        {
            if (string.IsNullOrWhiteSpace(completionFilePath))
                throw new InvalidOperationException("A completion file must be uploaded when marking a request as Completed.");

            request.CompletionFileName = completionFileName;
            request.CompletionFilePath = completionFilePath;
            request.CompletedAt = DateTime.UtcNow;
        }
        else if (request.Status == ServiceRequestStatus.Rejected)
        {
            request.CompletedAt = DateTime.UtcNow;
        }

        _unitOfWork.ServiceRequests.Update(request);
        await _unitOfWork.SaveChangesAsync();

        if (request.Status == ServiceRequestStatus.Completed)
        {
            await _notificationService.SendAsync(request.Citizen.UserId, "Request Completed",
                $"Your service request {request.ReferenceNumber} has been completed. The final document is now available for download.");
        }
        else
        {
            await _notificationService.SendAsync(request.Citizen.UserId, "Status Updated",
                $"Your service request {request.ReferenceNumber} status has been updated to {request.Status}.");
        }

        await _auditLogService.LogAsync(null, "StatusUpdated", "ServiceRequest",
            request.Id.ToString(), oldStatus, request.Status.ToString());
    }

    public async Task CancelAsync(int id, string userId)
    {
        var request = await _unitOfWork.ServiceRequests.GetWithDetailsAsync(id);
        if (request == null) throw new KeyNotFoundException("Service request not found.");

        if (request.Citizen.UserId != userId)
            throw new UnauthorizedAccessException("You can only cancel your own requests.");

        var oldStatus = request.Status.ToString();

        if (!_workflowService.IsValidTransition(oldStatus, ServiceRequestStatus.Cancelled.ToString()))
            throw new InvalidOperationException($"Cannot cancel a request with status '{oldStatus}'.");

        request.Status = ServiceRequestStatus.Cancelled;
        request.CompletedAt = DateTime.UtcNow;

        _unitOfWork.ServiceRequests.Update(request);
        await _unitOfWork.SaveChangesAsync();

        await _notificationService.SendAsync(request.Citizen.UserId, "Request Cancelled",
            $"Your service request {request.ReferenceNumber} has been cancelled.");

        await _auditLogService.LogAsync(userId, "Cancelled", "ServiceRequest",
            request.Id.ToString(), oldStatus, ServiceRequestStatus.Cancelled.ToString());
    }

    public async Task DeleteAsync(int id)
    {
        var request = await _unitOfWork.ServiceRequests.GetByIdAsync(id);
        if (request == null) throw new KeyNotFoundException("Service request not found.");

        _unitOfWork.ServiceRequests.Remove(request);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<DocumentDto>> UploadDocumentsAsync(int serviceRequestId, IEnumerable<IFormFile> files)
    {
        var request = await _unitOfWork.ServiceRequests.GetByIdAsync(serviceRequestId);
        if (request == null) throw new KeyNotFoundException("Service request not found.");

        // Allow uploads for most active statuses. Disallow only when completed or cancelled.
        if (request.Status == ServiceRequestStatus.Completed || request.Status == ServiceRequestStatus.Cancelled)
            throw new InvalidOperationException("Cannot upload documents for this request in its current status.");

        var uploaded = await _documentService.UploadMultipleAsync(serviceRequestId, files);

        // If officer had requested additional information, revert to UnderReview after citizen uploads
        if (request.Status == ServiceRequestStatus.PendingInformation)
        {
            request.Status = ServiceRequestStatus.UnderReview;
            _unitOfWork.ServiceRequests.Update(request);
            await _unitOfWork.SaveChangesAsync();

            // Notify assigned officers for this service that new documents were provided
            var assignments = await _unitOfWork.OfficerServiceAssignments.GetByServiceIdAsync(request.GovernmentServiceId);
            foreach (var a in assignments)
            {
                try
                {
                    await _notificationService.SendAsync(a.OfficerId, "Information Provided",
                        $"Citizen has provided additional documents for request {request.ReferenceNumber}.");
                }
                catch
                {
                    // Ignore notification failures
                }
            }

            await _auditLogService.LogAsync(null, "StatusUpdated", "ServiceRequest",
                request.Id.ToString(), ServiceRequestStatus.PendingInformation.ToString(), ServiceRequestStatus.UnderReview.ToString());
        }

        await _auditLogService.LogAsync(null, "DocumentsUploaded", "ServiceRequest",
            serviceRequestId.ToString(), null, $"Uploaded {uploaded?.Count() ?? 0} document(s)");

        return uploaded;
    }

    private static string GenerateReferenceNumber()
    {
        return $"SR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }
}
