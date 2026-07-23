using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Citizen> Citizens => Set<Citizen>();
    public DbSet<GovernmentService> GovernmentServices => Set<GovernmentService>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ApprovalWorkflow> ApprovalWorkflows => Set<ApprovalWorkflow>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RequiredDocument> RequiredDocuments => Set<RequiredDocument>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OfficerServiceAssignment> OfficerServiceAssignments => Set<OfficerServiceAssignment>();
    public DbSet<DepartmentLocation> DepartmentLocations => Set<DepartmentLocation>();
    public DbSet<ServiceCenter> ServiceCenters => Set<ServiceCenter>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<DocumentProfile> DocumentProfiles => Set<DocumentProfile>();
    public DbSet<PublicHoliday> PublicHolidays { get; set; }
    public DbSet<AppointmentReminderLog> AppointmentReminderLogs { get; set; }
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();
    public DbSet<SupportReply> SupportReplies => Set<SupportReply>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Citizen>(entity =>
        {
            entity.HasIndex(c => c.NationalId).IsUnique();
            entity.HasIndex(c => c.UserId).IsUnique();

            entity.HasOne(c => c.User)
                .WithOne(u => u.Citizen)
                .HasForeignKey<Citizen>(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GovernmentService>(entity =>
        {
            entity.Property(g => g.Fee).HasPrecision(18, 2);

            entity.HasOne(g => g.ApprovalWorkflow)
                .WithMany(w => w.GovernmentServices)
                .HasForeignKey(g => g.ApprovalWorkflowId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(g => g.Department)
                .WithMany(d => d.GovernmentServices)
                .HasForeignKey(g => g.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<RequiredDocument>(entity =>
        {
            entity.HasOne(r => r.GovernmentService)
                .WithMany(g => g.RequiredDocuments)
                .HasForeignKey(r => r.GovernmentServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ServiceRequest>(entity =>
        {
            entity.HasIndex(s => s.ReferenceNumber).IsUnique();

            entity.HasOne(s => s.Citizen)
                .WithMany(c => c.ServiceRequests)
                .HasForeignKey(s => s.CitizenId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.GovernmentService)
                .WithMany(g => g.ServiceRequests)
                .HasForeignKey(s => s.GovernmentServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(s => s.Status)
                .HasConversion<string>();
        });

        builder.Entity<Document>(entity =>
        {
            entity.HasOne(d => d.ServiceRequest)
                .WithMany(s => s.Documents)
                .HasForeignKey(d => d.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApprovalStep>(entity =>
        {
            entity.HasOne(a => a.ServiceRequest)
                .WithMany(s => s.ApprovalSteps)
                .HasForeignKey(a => a.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Officer)
                .WithMany()
                .HasForeignKey(a => a.OfficerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(a => a.Status)
                .HasConversion<string>();
        });

        builder.Entity<Notification>(entity =>
        {
            entity.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OfficerServiceAssignment>(entity =>
        {
            entity.HasIndex(o => o.OfficerId).IsUnique();

            entity.HasOne(o => o.Officer)
                .WithMany(u => u.OfficerServiceAssignments)
                .HasForeignKey(o => o.OfficerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(o => o.GovernmentService)
                .WithMany(g => g.OfficerServiceAssignments)
                .HasForeignKey(o => o.GovernmentServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DepartmentLocation>(entity =>
        {
            entity.Property(l => l.Latitude).HasPrecision(9, 6);
            entity.Property(l => l.Longitude).HasPrecision(9, 6);

            entity.HasOne(l => l.Department)
                .WithMany(d => d.Locations)
                .HasForeignKey(l => l.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.HasIndex(p => p.TransactionNumber).IsUnique();

            entity.Property(p => p.Amount).HasPrecision(18, 2);

            entity.Property(p => p.Status).HasConversion<string>();
            entity.Property(p => p.Method).HasConversion<string>();

            entity.HasOne(p => p.ServiceRequest)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.ServiceRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Appointment>(entity =>
        {
            entity.HasIndex(a => a.ReferenceNumber).IsUnique();
            entity.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.ServiceCenter).WithMany(c => c.Appointments).HasForeignKey(a => a.ServiceCenterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.GovernmentService).WithMany().HasForeignKey(a => a.GovernmentServiceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DocumentProfile>(entity =>
        {
            entity.HasIndex(p => new { p.UserId, p.DocumentType }).IsUnique();
            entity.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ServiceCenter>(entity =>
        {
            entity.Property(c => c.Latitude).HasPrecision(9, 6);
            entity.Property(c => c.Longitude).HasPrecision(9, 6);
        });
    }
}
