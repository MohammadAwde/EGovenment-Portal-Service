using SmartEGov.Domain.Enums;

namespace SmartEGov.Application.Services;

public interface IPaymentService
{
    Task<PaymentDto> InitiatePaymentAsync(CreatePaymentDto dto);
    Task<PaymentDto> ProcessPaymentAsync(int paymentId, ProcessPaymentDto dto);
    Task<PaymentDto?> GetByIdAsync(int id);
    Task<IEnumerable<PaymentDto>> GetByServiceRequestIdAsync(int serviceRequestId);
    Task<IEnumerable<PaymentDto>> GetByUserIdAsync(string userId);
    Task<bool> IsServiceRequestPaidAsync(int serviceRequestId);
    // Create a Stripe Checkout session for an existing pending payment and return the session URL
    Task<string?> CreateCheckoutSessionAsync(int paymentId, string baseUrl);
    // Check Stripe for payment status and update Payment entity accordingly (used when webhook not yet arrived)
    Task<PaymentDto> CheckAndUpdatePaymentStatusAsync(int paymentId);
}

public class PaymentDto
{
    public int Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string? CardLast4 { get; set; }
    public string? PayPalEmail { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ServiceRequestId { get; set; }
    public string? ServiceRequestReference { get; set; }
    public string? GovernmentServiceName { get; set; }
    public string UserId { get; set; } = string.Empty;
    // If a Checkout Session is created, this contains the URL to redirect the user to Stripe Checkout
    public string? CheckoutUrl { get; set; }
    // Persisted Stripe identifiers for diagnostics and reconciliation
    public string? StripeSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    // Latest observed Stripe status for diagnostics (e.g. "succeeded", "requires_action")
    public string? LastStripeStatus { get; set; }
}

public class CreatePaymentDto
{
    public int ServiceRequestId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    // Optional base URL to use for Checkout redirects (e.g. https://localhost:7246). If provided, it overrides app configuration.
    public string? BaseUrl { get; set; }
}

public class ProcessPaymentDto
{
    public string? CardNumber { get; set; }
    public string? CardHolder { get; set; }
    public string? ExpiryDate { get; set; }
    public string? Cvv { get; set; }
    public string? PayPalEmail { get; set; }
    // When using Stripe Elements or client-side tokenization, the client will submit a PaymentMethodId
    // that the server can use to create/confirm a PaymentIntent. This avoids sending raw card data to the server.
    public string? PaymentMethodId { get; set; }
}
