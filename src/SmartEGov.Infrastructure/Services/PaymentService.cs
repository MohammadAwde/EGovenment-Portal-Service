using AutoMapper;
using Microsoft.Extensions.Configuration;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;
using SmartEGov.Domain.Enums;
using Stripe;
using Stripe.Checkout;
using Microsoft.Extensions.Logging;

namespace SmartEGov.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IAuditLogService auditLogService,
        IMapper mapper,
        IConfiguration configuration,
        ILogger<PaymentService> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _mapper = mapper;
        _configuration = configuration;
        _logger = logger;

        // Configure Stripe API key from configuration or environment variable
        var stripeKey = _configuration["Stripe:SecretKey"] ?? Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
        if (!string.IsNullOrWhiteSpace(stripeKey))
        {
            StripeConfiguration.ApiKey = stripeKey;
        }
        else
        {
            _logger.LogWarning("Stripe API key is not configured.");
        }
    }

    public async Task<PaymentDto> InitiatePaymentAsync(CreatePaymentDto dto)
    {
        var payment = new Payment
        {
            TransactionNumber = GenerateTransactionNumber(),
            Amount = dto.Amount,
            Method = dto.Method,
            Status = PaymentStatus.Pending,
            ServiceRequestId = dto.ServiceRequestId,
            UserId = dto.UserId
        };

        await _unitOfWork.Payments.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // If card payment, create a Stripe Checkout Session and return its URL
        string? checkoutUrl = null;
        if (payment.Method == SmartEGov.Domain.Enums.PaymentMethod.CreditCard || payment.Method == SmartEGov.Domain.Enums.PaymentMethod.DebitCard)
        {
            // Determine base URL for redirect targets: prefer DTO.BaseUrl when provided
            var baseUrl = _configuration["App:BaseUrl"] ?? Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5000";
            if (!string.IsNullOrWhiteSpace(dto.BaseUrl))
            {
                baseUrl = dto.BaseUrl.TrimEnd('/');
            }
            baseUrl = baseUrl.TrimEnd('/');

            // Get friendly product/service name
            string productName = "Government Service";
            try
            {
                var req = await _unitOfWork.ServiceRequests.GetWithDetailsAsync(dto.ServiceRequestId);
                if (req != null)
                {
                    productName = req.GovernmentService?.Name ?? productName;
                }
            }
            catch { }

            var currency = _configuration["Stripe:Currency"] ?? "usd";

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(payment.Amount * 100m),
                            Currency = currency,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = productName
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = $"{baseUrl}/Payment/Receipt?paymentId={payment.Id}",
                CancelUrl = $"{baseUrl}/Payment/Process?paymentId={payment.Id}"
            };

                try
                {
                    var service = new SessionService();
                    var session = await service.CreateAsync(options);
                    checkoutUrl = session.Url;

                    // Persist Stripe session and payment intent IDs on the payment entity for webhook reconciliation
                    payment.StripeSessionId = session.Id;
                    // session.PaymentIntent may be a string id or a PaymentIntent object depending on the SDK response
                    if (session.PaymentIntent != null)
                    {
                        // session.PaymentIntent may be a PaymentIntent object; store its id
                        try { payment.StripePaymentIntentId = session.PaymentIntent.Id; } catch { payment.StripePaymentIntentId = session.PaymentIntent.ToString(); }
                    }
                    _unitOfWork.Payments.Update(payment);
                    await _unitOfWork.SaveChangesAsync();
                }
            catch (Exception ex)
            {
                // log or attach failure reason — for now, store in payment failure reason
                payment.FailureReason = ex.Message;
                _unitOfWork.Payments.Update(payment);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        var dtoOut = _mapper.Map<PaymentDto>(payment);
        dtoOut.CheckoutUrl = checkoutUrl;
        return dtoOut;
    }

    public async Task<string?> CreateCheckoutSessionAsync(int paymentId, string baseUrl)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId);
        if (payment == null) throw new KeyNotFoundException("Payment not found.");
        if (payment.Status != PaymentStatus.Pending) throw new InvalidOperationException("Only pending payments can be processed.");

        // Build session options
        baseUrl = baseUrl?.TrimEnd('/') ?? (_configuration["App:BaseUrl"] ?? Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5000");
        var currency = _configuration["Stripe:Currency"] ?? "usd";

        string productName = "Government Service";
        string? stripeStatus = null;
        try
        {
            var req = await _unitOfWork.ServiceRequests.GetWithDetailsAsync(payment.ServiceRequestId);
            if (req != null) productName = req.GovernmentService?.Name ?? productName;
        }
        catch { }

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(payment.Amount * 100m),
                        Currency = currency,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = productName
                        }
                    },
                    Quantity = 1
                }
            },
            Mode = "payment",
            SuccessUrl = $"{baseUrl}/Payment/Receipt?paymentId={payment.Id}",
            CancelUrl = $"{baseUrl}/Payment/Process?paymentId={payment.Id}"
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        // persist ids
        payment.StripeSessionId = session.Id;
        try { payment.StripePaymentIntentId = session.PaymentIntent?.Id ?? session.PaymentIntent?.ToString(); } catch { payment.StripePaymentIntentId = session.PaymentIntent?.ToString(); }
        _unitOfWork.Payments.Update(payment);
        await _unitOfWork.SaveChangesAsync();

        return session.Url;
    }

    public async Task<PaymentDto> CheckAndUpdatePaymentStatusAsync(int paymentId)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId);
        if (payment == null) throw new KeyNotFoundException("Payment not found.");

        // If already completed or failed, return current state
        if (payment.Status != PaymentStatus.Pending)
            return _mapper.Map<PaymentDto>(payment);

        // Try to reconcile using PaymentIntent first, then Session
        string? intentId = payment.StripePaymentIntentId;
        string? sessionId = payment.StripeSessionId;

        _logger.LogInformation("Reconciling payment Id={PaymentId} Transaction={Txn} StripeSession={SessionId} StripeIntent={IntentId}", payment.Id, payment.TransactionNumber, sessionId, intentId);

        PaymentIntent? intent = null;
        Session? session = null;
        string? stripeStatus = null;
        try
        {

            var piService = new PaymentIntentService();
            var sessionService = new SessionService();

            if (!string.IsNullOrWhiteSpace(intentId))
            {
                try
                {
                    _logger.LogDebug("Attempting to fetch PaymentIntent {IntentId}", intentId);
                    intent = await piService.GetAsync(intentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch PaymentIntent {IntentId}", intentId);
                    intent = null;
                }
            }

            if (intent == null && !string.IsNullOrWhiteSpace(sessionId))
            {
                try
                {
                    _logger.LogDebug("Attempting to fetch Checkout Session {SessionId}", sessionId);
                    session = await sessionService.GetAsync(sessionId);
                    // session.PaymentIntent may be id or object
                    if (session?.PaymentIntent != null && intent == null)
                    {
                        try { intent = session.PaymentIntent as PaymentIntent; } catch { intent = null; }
                        if (intent == null)
                        {
                            try
                            {
                                var pid = session.PaymentIntent.ToString();
                                _logger.LogDebug("Session {SessionId} references PaymentIntent {Pid}", sessionId, pid);
                                intent = await piService.GetAsync(pid);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to fetch PaymentIntent referenced by Session {SessionId}", sessionId);
                                intent = null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch Session {SessionId}", sessionId);
                    session = null;
                }
            }

            if (intent != null)
            {
                _logger.LogInformation("PaymentIntent {IntentId} status={Status}", intent.Id, intent.Status);
                if (string.Equals(intent.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    payment.Status = PaymentStatus.Completed;
                    payment.CompletedAt = DateTime.UtcNow;
                    _unitOfWork.Payments.Update(payment);
                    await _unitOfWork.SaveChangesAsync();

                    await _notificationService.SendAsync(payment.UserId, "Payment successful",
                        $"Your payment of {payment.Amount:N0} LBP (Txn: {payment.TransactionNumber}) was successful.");

                    await _auditLogService.LogAsync(payment.UserId, "PaymentCompleted", "Payment",
                        payment.Id.ToString(), null, $"Reconciled via Stripe API: {intent.Id}");
                    stripeStatus = intent.Status;

                    _logger.LogInformation("Payment {PaymentId} marked Completed via intent {IntentId}", payment.Id, intent.Id);
                }
                else if (string.Equals(intent.Status, "requires_payment_method", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(intent.Status, "requires_action", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(intent.Status, "processing", StringComparison.OrdinalIgnoreCase))
                {
                    // still pending; do nothing
                    stripeStatus = intent.Status;
                    _logger.LogInformation("PaymentIntent {IntentId} remains in state {Status}", intent.Id, intent.Status);
                }
                else
                {
                    // other states treat as failed
                    payment.Status = PaymentStatus.Failed;
                    payment.FailureReason = $"Stripe status: {intent.Status}";
                    _unitOfWork.Payments.Update(payment);
                    await _unitOfWork.SaveChangesAsync();

                    await _notificationService.SendAsync(payment.UserId, "Payment failed",
                        $"Your payment of {payment.Amount:N0} LBP (Txn: {payment.TransactionNumber}) failed: {payment.FailureReason}");
                    stripeStatus = intent.Status;

                    _logger.LogInformation("Payment {PaymentId} marked Failed via intent {IntentId} status={Status}", payment.Id, intent.Id, intent.Status);
                }
            }
            else if (session != null)
            {
                // Session exists but no PaymentIntent resolved
                stripeStatus = session.PaymentStatus;
                _logger.LogInformation("Session {SessionId} has PaymentStatus {Status}", sessionId, session.PaymentStatus);

                // Mark completed when session.payment_status == "paid"
                if (string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
                {
                    payment.Status = PaymentStatus.Completed;
                    payment.CompletedAt = DateTime.UtcNow;
                    _unitOfWork.Payments.Update(payment);
                    await _unitOfWork.SaveChangesAsync();

                    await _notificationService.SendAsync(payment.UserId, "Payment successful",
                        $"Your payment of {payment.Amount:N0} LBP (Txn: {payment.TransactionNumber}) was successful.");

                    await _auditLogService.LogAsync(payment.UserId, "PaymentCompleted", "Payment",
                        payment.Id.ToString(), null, $"Reconciled via Stripe Checkout Session: {session.Id}");

                    _logger.LogInformation("Payment {PaymentId} marked Completed via session {SessionId}", payment.Id, session.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during reconciliation for payment {PaymentId}", payment.Id);
            // swallow errors but log via audit
            await _auditLogService.LogAsync(null, "PaymentReconcileError", "Payment", payment.Id.ToString(), null, ex.Message);
        }

        var payments = await _unitOfWork.Payments.GetByServiceRequestIdAsync(payment.ServiceRequestId);
        var updated = payments.FirstOrDefault(p => p.Id == payment.Id) ?? payment;
        var dto = _mapper.Map<PaymentDto>(updated);
        if (!string.IsNullOrWhiteSpace(stripeStatus)) dto.LastStripeStatus = stripeStatus;
        return dto;
    }

    public async Task<PaymentDto> ProcessPaymentAsync(int paymentId, ProcessPaymentDto dto)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId);
        if (payment == null)
            throw new KeyNotFoundException("Payment not found.");

        if (payment.Status != PaymentStatus.Pending)
            throw new InvalidOperationException("This payment has already been processed.");

        try
        {
            if (payment.Method == SmartEGov.Domain.Enums.PaymentMethod.PayPal)
            {
                // PayPal is not integrated with Stripe. Keep the previous simulation behavior.
                if (string.IsNullOrWhiteSpace(dto.PayPalEmail))
                    throw new InvalidOperationException("PayPal email is required.");

                var success = !dto.PayPalEmail.Contains("fail", StringComparison.OrdinalIgnoreCase);
                payment.PayPalEmail = dto.PayPalEmail;

                if (success)
                {
                    payment.Status = PaymentStatus.Completed;
                    payment.CompletedAt = DateTime.UtcNow;
                }
                else
                {
                    payment.Status = PaymentStatus.Failed;
                    payment.FailureReason = "PayPal payment was declined.";
                }
            }
            else
            {
                // For PCI safety, do not accept raw card details on the server in production.
                // Require client-side tokenization (Stripe PaymentMethodId) to be provided.
                var stripePaymentMethodId = dto.PaymentMethodId;

                if (string.IsNullOrWhiteSpace(stripePaymentMethodId))
                {
                    throw new InvalidOperationException("Card payments must be performed using client-side tokenization (PaymentMethodId). Configure Stripe publishable key and use Stripe Elements or Checkout.");
                }

                // Create and confirm PaymentIntent using the provided PaymentMethodId
                var paymentIntentService = new PaymentIntentService();
                var createOptions = new PaymentIntentCreateOptions
                {
                    Amount = (long)(payment.Amount * 100m), // amount in cents
                    Currency = "usd",
                    PaymentMethod = stripePaymentMethodId,
                    Confirm = true,
                    OffSession = true
                };

                try
                {
                    var intent = await paymentIntentService.CreateAsync(createOptions);

                    if (intent.Status == "succeeded")
                    {
                        payment.Status = PaymentStatus.Completed;
                        payment.CompletedAt = DateTime.UtcNow;
                        // CardLast4 will be populated from Stripe once webhooks or intent payment_method details are stored.
                    }
                    else
                    {
                        payment.Status = PaymentStatus.Failed;
                        payment.FailureReason = $"Stripe payment failed: {intent.Status}";
                    }
                }
                catch (StripeException sx)
                {
                    payment.Status = PaymentStatus.Failed;
                    payment.FailureReason = sx.Message;
                }
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = ex.Message;
        }

        _unitOfWork.Payments.Update(payment);
        await _unitOfWork.SaveChangesAsync();

        // Send notification
        var statusText = payment.Status == PaymentStatus.Completed ? "successful" : "failed";
        await _notificationService.SendAsync(payment.UserId, "Payment " + statusText,
            $"Your payment of {payment.Amount:N0} LBP (Txn: {payment.TransactionNumber}) was {statusText}.");

        await _auditLogService.LogAsync(payment.UserId, "Payment" + payment.Status, "Payment",
            payment.Id.ToString(), null, $"Txn: {payment.TransactionNumber}, Amount: {payment.Amount}, Method: {payment.Method}");

        // Reload with navigation properties for the DTO
        var payments = await _unitOfWork.Payments.GetByServiceRequestIdAsync(payment.ServiceRequestId);
        var updated = payments.FirstOrDefault(p => p.Id == payment.Id) ?? payment;
        return _mapper.Map<PaymentDto>(updated);
    }

    public async Task<PaymentDto?> GetByIdAsync(int id)
    {
        var payments = await _unitOfWork.Payments.GetByUserIdAsync(string.Empty);
        var payment = await _unitOfWork.Payments.GetByIdAsync(id);
        if (payment == null) return null;

        var loaded = (await _unitOfWork.Payments.GetByServiceRequestIdAsync(payment.ServiceRequestId))
            .FirstOrDefault(p => p.Id == id);
        return _mapper.Map<PaymentDto>(loaded ?? payment);
    }

    public async Task<IEnumerable<PaymentDto>> GetByServiceRequestIdAsync(int serviceRequestId)
    {
        var payments = await _unitOfWork.Payments.GetByServiceRequestIdAsync(serviceRequestId);
        return _mapper.Map<IEnumerable<PaymentDto>>(payments);
    }

    public async Task<IEnumerable<PaymentDto>> GetByUserIdAsync(string userId)
    {
        var payments = await _unitOfWork.Payments.GetByUserIdAsync(userId);
        return _mapper.Map<IEnumerable<PaymentDto>>(payments);
    }

    public async Task<bool> IsServiceRequestPaidAsync(int serviceRequestId)
    {
        var payments = await _unitOfWork.Payments.GetByServiceRequestIdAsync(serviceRequestId);
        return payments.Any(p => p.Status == PaymentStatus.Completed);
    }

    private static string GenerateTransactionNumber()
    {
        return $"PAY-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }

    private static bool TryParseExpiry(string expiry, out long month, out long year)
    {
        month = 0;
        year = 0;
        if (string.IsNullOrWhiteSpace(expiry)) return false;

        var parts = expiry.Split('/');
        if (parts.Length != 2) return false;

        if (!int.TryParse(parts[0], out var mm)) return false;

        var yyPart = parts[1];
        if (yyPart.Length == 2 && int.TryParse(yyPart, out var yy2))
        {
            // assume 20xx
            year = 2000 + yy2;
        }
        else if (yyPart.Length == 4 && int.TryParse(yyPart, out var yy4))
        {
            year = yy4;
        }
        else
        {
            return false;
        }

        month = mm;
        return month >= 1 && month <= 12;
    }
}
