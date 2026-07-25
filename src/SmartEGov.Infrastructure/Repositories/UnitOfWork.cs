using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Citizens = new CitizenRepository(context);
        ServiceRequests = new ServiceRequestRepository(context);
        Documents = new DocumentRepository(context);
        GovernmentServices = new GovernmentServiceRepository(context);
        ApprovalWorkflows = new ApprovalWorkflowRepository(context);
        Notifications = new NotificationRepository(context);
        AuditLogs = new AuditLogRepository(context);
        Departments = new DepartmentRepository(context);
        Payments = new PaymentRepository(context);
        OfficerServiceAssignments = new OfficerServiceAssignmentRepository(context);
        Appointments = new AppointmentRepository(context);
        ServiceCenters = new ServiceCenterRepository(context);
        DocumentProfiles = new DocumentProfileRepository(context);
        PublicHolidays = new PublicHolidayRepository(context);
        SupportMessages = new SupportMessageRepository(context);
        SupportReplies = new Repository<SupportReply>(context);
        ServiceCenterDaySchedules = new Repository<ServiceCenterDaySchedule>(context);
        WeekdaySchedules = new Repository<WeekdaySchedule>(context);
    }

    public ICitizenRepository Citizens { get; }
    public IServiceRequestRepository ServiceRequests { get; }
    public IDocumentRepository Documents { get; }
    public IGovernmentServiceRepository GovernmentServices { get; }
    public IApprovalWorkflowRepository ApprovalWorkflows { get; }
    public INotificationRepository Notifications { get; }
    public IAuditLogRepository AuditLogs { get; }
    public IDepartmentRepository Departments { get; }
    public IPaymentRepository Payments { get; }
    public IOfficerServiceAssignmentRepository OfficerServiceAssignments { get; }
    public IAppointmentRepository Appointments { get; }
    public IServiceCenterRepository ServiceCenters { get; }
    public IDocumentProfileRepository DocumentProfiles { get; }
    public IPublicHolidayRepository PublicHolidays { get; }
    public ISupportMessageRepository SupportMessages { get; }
    public IRepository<SupportReply> SupportReplies { get; }
    public IRepository<ServiceCenterDaySchedule> ServiceCenterDaySchedules { get; }
    public IRepository<WeekdaySchedule> WeekdaySchedules { get; }


    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
