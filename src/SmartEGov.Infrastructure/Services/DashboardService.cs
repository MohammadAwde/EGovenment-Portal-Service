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
            .AsNoTracking()
            .Where(a => a.OfficerId == officerId)
            .Select(a => a.GovernmentServiceId)
            .ToListAsync();

        // Query pending requests only for assigned services and only the counts and a small list
        var pendingQuery = _context.ServiceRequests.AsNoTracking().Where(r => assignments.Contains(r.GovernmentServiceId)
            && (r.Status == ServiceRequestStatus.Submitted || r.Status == ServiceRequestStatus.UnderReview));

        var pendingSubmittedCount = await pendingQuery.CountAsync(r => r.Status == ServiceRequestStatus.Submitted);
        var pendingUnderReviewCount = await pendingQuery.CountAsync(r => r.Status == ServiceRequestStatus.UnderReview);

        var pendingList = await pendingQuery
            .Include(r => r.GovernmentService)
            .Include(r => r.Citizen)
            .OrderByDescending(r => r.SubmittedAt)
            .Take(10)
            .ToListAsync();

        // Recent actions by this officer - only need counts and a small recent list
        var myActionsQuery = _context.ApprovalSteps.AsNoTracking().Where(s => s.OfficerId == officerId && s.Status != ApprovalStatus.Pending);
        var totalReviewedByMe = await myActionsQuery.CountAsync();
        var approvedByMe = await myActionsQuery.CountAsync(a => a.Status == ApprovalStatus.Approved);
        var rejectedByMe = await myActionsQuery.CountAsync(a => a.Status == ApprovalStatus.Rejected);
        var recentActions = await myActionsQuery.Include(s => s.ServiceRequest).OrderByDescending(s => s.ActionDate).Take(10).ToListAsync();

        var assignedServices = await _context.GovernmentServices.AsNoTracking().Where(g => assignments.Contains(g.Id)).ToListAsync();

        var assignedSummaries = assignedServices.Select(s => new AssignedServiceSummary
        {
            Id = s.Id,
            Name = s.Name,
            Fee = s.Fee,
            PendingCount = pendingList.Count(r => r.GovernmentServiceId == s.Id)
        }).ToList();

        return new OfficerDashboardDto
        {
            PendingRequests = pendingSubmittedCount,
            UnderReviewRequests = pendingUnderReviewCount,
            TotalReviewedByMe = totalReviewedByMe,
            ApprovedByMe = approvedByMe,
            RejectedByMe = rejectedByMe,
            PendingRequestsList = _mapper.Map<IEnumerable<ServiceRequestDto>>(pendingList),
            RecentActions = _mapper.Map<IEnumerable<ApprovalStepDto>>(recentActions),
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
