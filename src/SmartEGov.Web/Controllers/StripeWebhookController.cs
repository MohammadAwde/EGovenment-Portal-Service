using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using Newtonsoft.Json.Linq;
using Stripe;
using Stripe.Checkout;
using System.Net.Http;
using System.Net.Sockets;
using SmartEGov.Domain.Enums;

namespace SmartEGov.Web.Controllers;

[AllowAnonymous]
[ApiController]
[Route("stripe/webhook")]
public class StripeWebhookController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IAuditLogService auditLogService,
        IConfiguration configuration,
        ILogger<StripeWebhookController> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _configuration = configuration;
        _logger = logger;
    }

    // Simple retry helper for transient network errors when calling Stripe SDK
    private static async Task<T?> TryWithRetries<T>(Func<Task<T>> action, ILogger logger, int maxAttempts = 3, int initialDelayMs = 500)
        where T : class
    {
        var attempt = 0;
        var delay = initialDelayMs;
        while (true)
        {
            attempt++;
            try
            {
                return await action();
            }
            catch (HttpRequestException hre)
            {
                logger.LogWarning(hre, "HTTP error on attempt {Attempt} when calling Stripe: {Message}", attempt, hre.Message);
            }
            catch (SocketException se)
            {
                logger.LogWarning(se, "Socket error on attempt {Attempt} when calling Stripe: {Message}", attempt, se.Message);
            }
            catch (Exception ex)
            {
                // Non-transient - rethrow
                logger.LogError(ex, "Non-retryable error when calling Stripe API.");
                throw;
            }

            if (attempt >= maxAttempts)
            {
                logger.LogError("Exceeded max retry attempts ({MaxAttempts}) for Stripe API call.", maxAttempts);
                return null;
            }

            try { await Task.Delay(delay); } catch { }
            delay *= 2;
        }
    }

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("Stripe webhook received empty payload.");
            return BadRequest();
        }
        var stripeSignature = HttpContext.Request.Headers["Stripe-Signature"].FirstOrDefault();

        var webhookSecret = _configuration["Stripe:WebhookSecret"] ?? Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogWarning("Stripe webhook secret not configured.");
            return BadRequest();
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to construct Stripe event from payload (invalid signature or payload). Raw payload length: {Len}", json?.Length ?? 0);
            return BadRequest();
        }

        // Diagnostics: log basic event metadata and a trimmed payload for troubleshooting (avoid logging secrets)
        try
        {
            _logger.LogInformation("Stripe webhook received. Id={Id}, Type={Type}, Created={Created}, RawLength={Len}",
                stripeEvent.Id, stripeEvent.Type, stripeEvent.Created, json?.Length ?? 0);

            // Log a trimmed version of the payload (first 2000 chars) to help debugging without huge logs
            var trimmed = json?.Length > 2000 ? json.Substring(0, 2000) + "..." : json;
            _logger.LogDebug("Stripe webhook payload (trimmed): {Payload}", trimmed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log Stripe webhook diagnostic info.");
        }

        try
        {
            switch (stripeEvent.Type)
            {
                case Events.CheckoutSessionCompleted:
                {
                    // Resolve session id
                    string? sessionId = null;
                    try { sessionId = (stripeEvent.Data.Object as Stripe.IHasId)?.Id; } catch { sessionId = null; }
                    if (string.IsNullOrWhiteSpace(sessionId))
                    {
                        try { sessionId = JObject.Parse(json!)["data"]?["object"]?["id"]?.ToString(); } catch { }
                    }

                    if (string.IsNullOrWhiteSpace(sessionId))
                    {
                        _logger.LogWarning("CheckoutSessionCompleted received but no session id could be resolved.");
                        break;
                    }

                    _logger.LogInformation("Processing CheckoutSessionCompleted: sessionId={SessionId}", sessionId);

                    // Fetch full session from Stripe to obtain payment_intent and charges
                    var sessionService = new SessionService();
                    Session? fullSession = null;
                    try
                    {
                        fullSession = await TryWithRetries(async () => await sessionService.GetAsync(sessionId, new SessionGetOptions
                        {
                            Expand = new List<string> { "payment_intent", "payment_intent.charges.data.payment_method_details.card" }
                        }), _logger);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to retrieve full Checkout Session {SessionId} from Stripe.", sessionId);
                    }

                    string? paymentIntentId = null;
                    PaymentIntent? intent = null;
                    if (fullSession != null && fullSession.PaymentIntent != null)
                    {
                        try
                        {
                            if (fullSession.PaymentIntent is PaymentIntent piObj)
                            {
                                paymentIntentId = piObj.Id;
                                intent = piObj;
                            }
                            else
                            {
                                paymentIntentId = fullSession.PaymentIntent.ToString();
                            }
                        }
                        catch
                        {
                            paymentIntentId = fullSession.PaymentIntent.ToString();
                        }
                    }

                    // If we didn't get an expanded PaymentIntent, fetch it explicitly
                    var piService = new PaymentIntentService();
                    if (intent == null && !string.IsNullOrWhiteSpace(paymentIntentId))
                    {
                        try
                        {
                            intent = await TryWithRetries(async () => await piService.GetAsync(paymentIntentId, new PaymentIntentGetOptions
                            {
                                Expand = new List<string> { "charges.data.payment_method_details.card" }
                            }), _logger);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Unable to fetch PaymentIntent {PaymentIntentId} for session {SessionId}.", paymentIntentId, sessionId);
                        }
                    }

                    // Try to find matching Payment in DB
                    var payment = await _unitOfWork.Payments.GetByStripeSessionIdAsync(sessionId);
                    if (payment == null && !string.IsNullOrWhiteSpace(paymentIntentId))
                        payment = await _unitOfWork.Payments.GetByPaymentIntentIdAsync(paymentIntentId!);

                    if (payment == null)
                    {
                        _logger.LogWarning("CheckoutSessionCompleted: no matching Payment found for session={SessionId} or intent={PaymentIntentId}.", sessionId, paymentIntentId);
                        // If no matching payment, nothing to update; return OK so Stripe won't retry indefinitely
                        break;
                    }

                    // Persist identifiers if missing
                    var updated = false;
                    if (string.IsNullOrWhiteSpace(payment.StripeSessionId)) { payment.StripeSessionId = sessionId; updated = true; }
                    if (!string.IsNullOrWhiteSpace(paymentIntentId) && string.IsNullOrWhiteSpace(payment.StripePaymentIntentId)) { payment.StripePaymentIntentId = paymentIntentId; updated = true; }

                    // Extract card last4 from Intent/charges
                    string? last4 = null;
                    if (intent == null && fullSession?.PaymentIntent is PaymentIntent pObj) intent = pObj;
                    if (intent != null)
                    {
                        var charge = intent.Charges?.Data?.FirstOrDefault();
                        last4 = charge?.PaymentMethodDetails?.Card?.Last4;
                    }
                    if (!string.IsNullOrWhiteSpace(last4) && string.IsNullOrWhiteSpace(payment.CardLast4)) { payment.CardLast4 = last4; updated = true; }

                    // Only mark Completed when we have clear evidence of successful payment:
                    // - PaymentIntent status == "succeeded" OR
                    // - Session.PaymentStatus == "paid"
                    var sessionPaid = fullSession != null && string.Equals(fullSession.PaymentStatus, "paid", System.StringComparison.OrdinalIgnoreCase);
                    var intentSucceeded = intent != null && string.Equals(intent.Status, "succeeded", System.StringComparison.OrdinalIgnoreCase);

                    if (payment.Status != SmartEGov.Domain.Enums.PaymentStatus.Completed)
                    {
                        if (intentSucceeded || sessionPaid)
                        {
                            payment.Status = SmartEGov.Domain.Enums.PaymentStatus.Completed;
                            payment.CompletedAt = DateTime.UtcNow;
                            updated = true;
                        }
                        else
                        {
                            _logger.LogInformation("CheckoutSessionCompleted received for session={SessionId} intent={IntentId} but neither session.payment_status=='paid' nor payment_intent.status=='succeeded' were present. Skipping marking Completed.", sessionId, paymentIntentId);
                        }
                    }

                    if (updated)
                    {
                        _unitOfWork.Payments.Update(payment);
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogInformation("Payment(Id={PaymentId}) updated from CheckoutSessionCompleted: status={Status}, session={SessionId}, intent={IntentId}", payment.Id, payment.Status, sessionId, paymentIntentId);
                    }

                    // If payment completed, transition the related service request so officers can continue processing
                    try
                    {
                        if (payment.Status == SmartEGov.Domain.Enums.PaymentStatus.Completed)
                        {
                            var request = await _unitOfWork.ServiceRequests.GetWithDetailsAsync(payment.ServiceRequestId);
                            if (request != null && request.Status == ServiceRequestStatus.PendingPayment)
                            {
                                request.Status = ServiceRequestStatus.UnderReview;
                                _unitOfWork.ServiceRequests.Update(request);
                                await _unitOfWork.SaveChangesAsync();

                                // Notify the citizen that payment was received and the request is now pending officer processing
                                await _notificationService.SendAsync(request.Citizen.UserId, "Payment received",
                                    $"We received your payment for request {request.ReferenceNumber}. An officer will process your request shortly.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update ServiceRequest status after payment completion for Payment.Id={PaymentId}", payment.Id);
                    }

                    // Notify user and audit
                    await _notificationService.SendAsync(payment.UserId, "Payment successful",
                        $"Your payment of {payment.Amount:N0} LBP (Txn: {payment.TransactionNumber}) was successful.");

                    await _auditLogService.LogAsync(payment.UserId, "PaymentCompleted", "Payment",
                        payment.Id.ToString(), null, $"Stripe Checkout session completed: {sessionId}");

                    break;
                }
                case Events.PaymentIntentSucceeded:
                {
                    // Resolve intent id
                    string? intentId = null;
                    try { intentId = (stripeEvent.Data.Object as Stripe.IHasId)?.Id; } catch { intentId = null; }
                    if (string.IsNullOrWhiteSpace(intentId))
                    {
                        try { intentId = JObject.Parse(json!)["data"]?["object"]?["id"]?.ToString(); } catch { }
                    }

                    if (string.IsNullOrWhiteSpace(intentId))
                    {
                        _logger.LogWarning("PaymentIntentSucceeded received but no intent id could be resolved.");
                        break;
                    }

                    _logger.LogInformation("Processing PaymentIntentSucceeded: intentId={IntentId}", intentId);

                    // Fetch full PaymentIntent to get card info
                    var piService2 = new PaymentIntentService();
                    PaymentIntent? fullIntent = null;
                    try
                    {
                        fullIntent = await piService2.GetAsync(intentId, new PaymentIntentGetOptions
                        {
                            Expand = new List<string> { "charges.data.payment_method_details.card" }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Unable to fetch full PaymentIntent {IntentId}.", intentId);
                    }

                    var payment2 = await _unitOfWork.Payments.GetByPaymentIntentIdAsync(intentId!);
                    if (payment2 == null)
                    {
                        _logger.LogWarning("PaymentIntentSucceeded: no matching Payment found for intent={IntentId}.", intentId);
                        break;
                    }

                    var upd = false;
                    if (string.IsNullOrWhiteSpace(payment2.StripePaymentIntentId)) { payment2.StripePaymentIntentId = intentId; upd = true; }
                    // last4
                    var chargeInfo = fullIntent?.Charges?.Data?.FirstOrDefault();
                    var l4 = chargeInfo?.PaymentMethodDetails?.Card?.Last4;
                    if (!string.IsNullOrWhiteSpace(l4) && string.IsNullOrWhiteSpace(payment2.CardLast4)) { payment2.CardLast4 = l4; upd = true; }

                    if (payment2.Status != SmartEGov.Domain.Enums.PaymentStatus.Completed)
                    {
                        payment2.Status = SmartEGov.Domain.Enums.PaymentStatus.Completed;
                        payment2.CompletedAt = DateTime.UtcNow;
                        upd = true;
                    }

                    if (upd)
                    {
                        _unitOfWork.Payments.Update(payment2);
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogInformation("Payment(Id={PaymentId}) marked Completed from PaymentIntentSucceeded (intent={IntentId}).", payment2.Id, intentId);

                        // After payment completes, allow officers to upload documents by moving the request out of PendingPayment
                        try
                        {
                            var request2 = await _unitOfWork.ServiceRequests.GetWithDetailsAsync(payment2.ServiceRequestId);
                            if (request2 != null && request2.Status == ServiceRequestStatus.PendingPayment)
                            {
                                request2.Status = ServiceRequestStatus.UnderReview;
                                _unitOfWork.ServiceRequests.Update(request2);
                                await _unitOfWork.SaveChangesAsync();

                                await _notificationService.SendAsync(request2.Citizen.UserId, "Payment received",
                                    $"We received your payment for request {request2.ReferenceNumber}. An officer will process your request shortly.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to update ServiceRequest status after payment completion for Payment.Id={PaymentId}", payment2.Id);
                        }
                    }

                    await _notificationService.SendAsync(payment2.UserId, "Payment successful",
                        $"Your payment of {payment2.Amount:N0} LBP (Txn: {payment2.TransactionNumber}) was successful.");

                    await _auditLogService.LogAsync(payment2.UserId, "PaymentCompleted", "Payment",
                        payment2.Id.ToString(), null, $"PaymentIntent succeeded: {intentId}");

                    break;
                }
                default:
                    _logger.LogInformation("Unhandled Stripe event type: {Type}", stripeEvent.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Stripe webhook event {Type}", stripeEvent?.Type);
            return StatusCode(500);
        }

        return Ok();
    }
}
