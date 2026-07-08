using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Mappings;
using SmartEGov.Application.Services;
using SmartEGov.Infrastructure.Data;
using SmartEGov.Infrastructure.Repositories;
using SmartEGov.Infrastructure.Services;

namespace SmartEGov.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        var smsSettings = new SmsSettings();
        configuration.GetSection("SmsSettings").Bind(smsSettings);
        services.AddSingleton(smsSettings);

        // Bind SMTP settings
        var smtpSettings = new SmtpSettings();
        configuration.GetSection("SmtpSettings").Bind(smtpSettings);
        services.AddSingleton(smtpSettings);

        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(MappingProfile).Assembly));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICitizenRepository, CitizenRepository>();
        services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IGovernmentServiceRepository, GovernmentServiceRepository>();
        services.AddScoped<IApprovalWorkflowRepository, ApprovalWorkflowRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IOfficerServiceAssignmentRepository, OfficerServiceAssignmentRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IServiceCenterRepository, ServiceCenterRepository>();
        services.AddScoped<IDocumentProfileRepository, DocumentProfileRepository>();
        services.AddScoped<IRequiredDocumentRepository, RequiredDocumentRepository>();


        services.AddScoped<ICitizenService, CitizenService>();
        services.AddScoped<IServiceRequestService, ServiceRequestService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISmsNotificationService, SmsNotificationService>();
        services.AddScoped<IEmailSender, SmtpEmailService>();
        services.AddScoped<IMailtrapService, MailtrapService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IFileValidationService, FileValidationService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IAutoFillService, AutoFillService>();

        return services;
    }
}
