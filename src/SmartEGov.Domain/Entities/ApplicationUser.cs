using Microsoft.AspNetCore.Identity;

namespace SmartEGov.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string? DigitalSignaturePath { get; set; }

    public Citizen? Citizen { get; set; }
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<OfficerServiceAssignment> OfficerServiceAssignments { get; set; } = new List<OfficerServiceAssignment>();
}
