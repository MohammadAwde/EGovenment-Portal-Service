using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    ICitizenRepository Citizens { get; }
    IServiceRequestRepository ServiceRequests { get; }
    IDocumentRepository Documents { get; }
    IGovernmentServiceRepository GovernmentServices { get; }
    IApprovalWorkflowRepository ApprovalWorkflows { get; }
    INotificationRepository Notifications { get; }
    IAuditLogRepository AuditLogs { get; }
    IDepartmentRepository Departments { get; }
    IPaymentRepository Payments { get; }
    IOfficerServiceAssignmentRepository OfficerServiceAssignments { get; }
    IAppointmentRepository Appointments { get; }
    IServiceCenterRepository ServiceCenters { get; }
    IDocumentProfileRepository DocumentProfiles { get; }
    IPublicHolidayRepository PublicHolidays { get; }
    ISupportMessageRepository SupportMessages { get; }
    IRepository<SupportReply> SupportReplies { get; }
    Task<int> SaveChangesAsync();
}
