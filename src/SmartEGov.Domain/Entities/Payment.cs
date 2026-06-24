using SmartEGov.Domain.Enums;

namespace SmartEGov.Domain.Entities;

public class Payment
{
    public int Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? CardLast4 { get; set; }
    public string? PayPalEmail { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    // Stripe identifiers for reconciliation and webhook handling
    public string? StripeSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }

    public int ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}
