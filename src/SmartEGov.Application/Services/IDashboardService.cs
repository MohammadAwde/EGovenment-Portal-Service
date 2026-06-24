using SmartEGov.Application.DTOs;

namespace SmartEGov.Application.Services;

public interface IDashboardService
{
    Task<AdminDashboardDto> GetAdminDashboardAsync();
    Task<OfficerDashboardDto> GetOfficerDashboardAsync(string officerId);
    Task<CitizenDashboardDto> GetCitizenDashboardAsync(int citizenId, string userId);
}

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int TotalCitizens { get; set; }
    public int TotalOfficers { get; set; }
    public int TotalAdmins { get; set; }
    public int TotalServiceRequests { get; set; }
    public int SubmittedRequests { get; set; }
    public int UnderReviewRequests { get; set; }
    public int ApprovedRequests { get; set; }
    public int RejectedRequests { get; set; }
    public int CompletedRequests { get; set; }
    public int CancelledRequests { get; set; }
    public int TotalGovernmentServices { get; set; }
    public int ActiveGovernmentServices { get; set; }
    public int TotalWorkflows { get; set; }
    public IEnumerable<ServiceRequestDto> RecentRequests { get; set; } = [];
    public IEnumerable<AuditLogDto> RecentAuditLogs { get; set; } = [];
}

public class AssignedServiceSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public int PendingCount { get; set; }
}

public class OfficerDashboardDto
{
    public int PendingRequests { get; set; }
    public int UnderReviewRequests { get; set; }
    public int TotalReviewedByMe { get; set; }
    public int ApprovedByMe { get; set; }
    public int RejectedByMe { get; set; }
    public IEnumerable<ServiceRequestDto> PendingRequestsList { get; set; } = [];
    public IEnumerable<ApprovalStepDto> RecentActions { get; set; } = [];
    public IEnumerable<GovernmentServiceDto> AssignedServices { get; set; } = [];
    public IEnumerable<AssignedServiceSummary> AssignedServiceSummaries { get; set; } = [];
}

public class CitizenDashboardDto
{
    public string CitizenName { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int SubmittedRequests { get; set; }
    public int UnderReviewRequests { get; set; }
    public int ApprovedRequests { get; set; }
    public int RejectedRequests { get; set; }
    public int CompletedRequests { get; set; }
    public int CancelledRequests { get; set; }
    public int UnreadNotifications { get; set; }
    public IEnumerable<ServiceRequestDto> RecentRequests { get; set; } = [];
    public IEnumerable<NotificationDto> RecentNotifications { get; set; } = [];
}
