using Microsoft.AspNetCore.Identity;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;
using SmartEGov.Domain.Enums;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly IMapper _mapper;

    public DashboardService(
        ApplicationDbContext context,
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        IMapper mapper)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _notificationService = notificationService;
        _mapper = mapper;
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync()
    {
        var allRequests = await _context.ServiceRequests
            .Include(sr => sr.GovernmentService)
            .Include(sr => sr.Citizen)
            .OrderByDescending(sr => sr.SubmittedAt)
            .ToListAsync();

        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
        var officerUsers = await _userManager.GetUsersInRoleAsync("Officer");
        var citizenUsers = await _userManager.GetUsersInRoleAsync("Citizen");

        var recentLogs = await _context.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .ToListAsync();

        return new AdminDashboardDto
        {
            TotalUsers = await _userManager.Users.CountAsync(),
            TotalAdmins = adminUsers.Count,
            TotalOfficers = officerUsers.Count,
            TotalCitizens = citizenUsers.Count,
            TotalServiceRequests = allRequests.Count,
            SubmittedRequests = allRequests.Count(r => r.Status == ServiceRequestStatus.Submitted),
            UnderReviewRequests = allRequests.Count(r => r.Status == ServiceRequestStatus.UnderReview),
            ApprovedRequests = allRequests.Count(r => r.Status == ServiceRequestStatus.Approved),
            RejectedRequests = allRequests.Count(r => r.Status == ServiceRequestStatus.Rejected),
            CompletedRequests = allRequests.Count(r => r.Status == ServiceRequestStatus.Completed),
            CancelledRequests = allRequests.Count(r => r.Status == ServiceRequestStatus.Cancelled),
            TotalGovernmentServices = await _context.GovernmentServices.CountAsync(),
            ActiveGovernmentServices = await _context.GovernmentServices.CountAsync(g => g.IsActive),
            TotalWorkflows = await _context.ApprovalWorkflows.CountAsync(w => w.IsActive),
            RecentRequests = _mapper.Map<IEnumerable<ServiceRequestDto>>(allRequests.Take(5)),
            RecentAuditLogs = _mapper.Map<IEnumerable<AuditLogDto>>(recentLogs)
        };
    }

    public async Task<OfficerDashboardDto> GetOfficerDashboardAsync(string officerId)
    {
        var assignments = await _context.OfficerServiceAssignments
            .Where(a => a.OfficerId == officerId)
            .Select(a => a.GovernmentServiceId)
            .ToListAsync();

        var pendingRequests = Enumerable.Empty<ServiceRequest>();
        if (assignments.Any())
        {
            // Use repository method to get non-terminal pending requests for assigned services
            pendingRequests = (await _unitOfWork.ServiceRequests.GetPendingByServiceIdsAsync(assignments)).ToList();
        }

        var myActions = await _context.ApprovalSteps
            .Include(s => s.ServiceRequest)
            .Where(s => s.OfficerId == officerId && s.Status != ApprovalStatus.Pending)
            .OrderByDescending(s => s.ActionDate)
            .ToListAsync();

        // Build assigned service summaries with pending counts
        var assignedServices = await _context.GovernmentServices
            .Where(g => assignments.Contains(g.Id))
            .ToListAsync();

        var assignedSummaries = assignedServices.Select(s => new AssignedServiceSummary
        {
            Id = s.Id,
            Name = s.Name,
            Fee = s.Fee,
            PendingCount = pendingRequests.Count(r => r.GovernmentServiceId == s.Id)
        }).ToList();

        return new OfficerDashboardDto
        {
            PendingRequests = pendingRequests.Count(r => r.Status == ServiceRequestStatus.Submitted),
            UnderReviewRequests = pendingRequests.Count(r => r.Status == ServiceRequestStatus.UnderReview),
            TotalReviewedByMe = myActions.Count,
            ApprovedByMe = myActions.Count(a => a.Status == ApprovalStatus.Approved),
            RejectedByMe = myActions.Count(a => a.Status == ApprovalStatus.Rejected),
            PendingRequestsList = _mapper.Map<IEnumerable<ServiceRequestDto>>(pendingRequests.Take(10)),
            RecentActions = _mapper.Map<IEnumerable<ApprovalStepDto>>(myActions.Take(10)),
            AssignedServices = _mapper.Map<IEnumerable<GovernmentServiceDto>>(assignedServices),
            AssignedServiceSummaries = assignedSummaries
        };
    }

    public async Task<CitizenDashboardDto> GetCitizenDashboardAsync(int citizenId, string userId)
    {
        var citizen = await _context.Citizens.FirstOrDefaultAsync(c => c.Id == citizenId);

        var requests = await _context.ServiceRequests
            .Include(sr => sr.GovernmentService)
            .Where(sr => sr.CitizenId == citizenId)
            .OrderByDescending(sr => sr.SubmittedAt)
            .ToListAsync();

        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
        var notifications = await _notificationService.GetByUserIdAsync(userId);

        return new CitizenDashboardDto
        {
            CitizenName = citizen != null ? $"{citizen.FirstName} {citizen.LastName}" : "",
            TotalRequests = requests.Count,
            SubmittedRequests = requests.Count(r => r.Status == ServiceRequestStatus.Submitted),
            UnderReviewRequests = requests.Count(r => r.Status == ServiceRequestStatus.UnderReview),
            ApprovedRequests = requests.Count(r => r.Status == ServiceRequestStatus.Approved),
            RejectedRequests = requests.Count(r => r.Status == ServiceRequestStatus.Rejected),
            CompletedRequests = requests.Count(r => r.Status == ServiceRequestStatus.Completed),
            CancelledRequests = requests.Count(r => r.Status == ServiceRequestStatus.Cancelled),
            UnreadNotifications = unreadCount,
            RecentRequests = _mapper.Map<IEnumerable<ServiceRequestDto>>(requests.Take(5)),
            RecentNotifications = notifications.Take(5)
        };
    }
}
