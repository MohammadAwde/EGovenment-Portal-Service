using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;

namespace SmartEGov.Web.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("admin/payment-diagnostics")] 
public class PaymentDiagnosticsController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentDiagnosticsController(IPaymentService paymentService, IUnitOfWork unitOfWork)
    {
        _paymentService = paymentService;
        _unitOfWork = unitOfWork;
    }

    // Simple view model used by the admin UI
    public class DiagnosticModel
    {
        public string? SessionId { get; set; }
        public string? IntentId { get; set; }
        public PaymentDto? Result { get; set; }
        public string? Message { get; set; }
        public IEnumerable<PaidUserModel>? PaidUsers { get; set; }
    }

    public class PaidUserModel
    {
        public int PaymentId { get; set; }
        public string TransactionNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceRequestReference { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime? CompletedAt { get; set; }
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        // Load paid users for admin at page load
        var model = new DiagnosticModel();
        try
        {
            var payments = _unitOfWork.Payments.GetCompletedPaymentsAsync().GetAwaiter().GetResult();
            model.PaidUsers = payments.Select(p => new PaidUserModel
            {
                PaymentId = p.Id,
                TransactionNumber = p.TransactionNumber,
                Amount = p.Amount,
                ServiceName = p.ServiceRequest?.GovernmentService?.Name ?? string.Empty,
                ServiceRequestReference = p.ServiceRequest?.ReferenceNumber ?? string.Empty,
                UserFullName = p.User?.FullName ?? string.Empty,
                UserEmail = p.User?.Email ?? string.Empty,
                CompletedAt = p.CompletedAt
            }).ToList();
        }
        catch
        {
            model.PaidUsers = Enumerable.Empty<PaidUserModel>();
            model.Message = "Failed to load paid users.";
        }

        return View(model);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(DiagnosticModel model)
    {
        if (model == null) model = new DiagnosticModel();

        try
        {
            if (!string.IsNullOrWhiteSpace(model.SessionId))
            {
                var payment = await _unitOfWork.Payments.GetByStripeSessionIdAsync(model.SessionId.Trim());
                if (payment == null)
                {
                    model.Message = "No payment found for the provided Session ID.";
                    return View(model);
                }

                // Force reconciliation
                var updated = await _paymentService.CheckAndUpdatePaymentStatusAsync(payment.Id);
                model.Result = updated;
                model.Message = "Reconciliation attempted for session.";
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(model.IntentId))
            {
                var payment = await _unitOfWork.Payments.GetByPaymentIntentIdAsync(model.IntentId.Trim());
                if (payment == null)
                {
                    model.Message = "No payment found for the provided PaymentIntent ID.";
                    return View(model);
                }

                var updated = await _paymentService.CheckAndUpdatePaymentStatusAsync(payment.Id);
                model.Result = updated;
                model.Message = "Reconciliation attempted for payment intent.";
                return View(model);
            }

            model.Message = "Please provide either a Session ID or a PaymentIntent ID.";
            return View(model);
        }
        catch (Exception ex)
        {
            model.Message = "Error during diagnostics: " + ex.Message;
            return View(model);
        }
    }

    [HttpGet("get-by-session")] 
    public async Task<IActionResult> GetBySession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId is required");
        var payment = await _unitOfWork.Payments.GetByStripeSessionIdAsync(sessionId);
        if (payment == null) return NotFound();
        var dto = await _paymentService.GetByIdAsync(payment.Id);
        return Ok(dto);
    }

    [HttpGet("get-by-intent")] 
    public async Task<IActionResult> GetByIntent(string intentId)
    {
        if (string.IsNullOrWhiteSpace(intentId)) return BadRequest("intentId is required");
        var payment = await _unitOfWork.Payments.GetByPaymentIntentIdAsync(intentId);
        if (payment == null) return NotFound();
        var dto = await _paymentService.GetByIdAsync(payment.Id);
        return Ok(dto);
    }

    [HttpPost("reconcile-by-session")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReconcileBySession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId is required");
        var payment = await _unitOfWork.Payments.GetByStripeSessionIdAsync(sessionId);
        if (payment == null) return NotFound("Payment not found for session id");
        var updated = await _paymentService.CheckAndUpdatePaymentStatusAsync(payment.Id);
        return Ok(updated);
    }

    [HttpPost("reconcile-by-intent")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReconcileByIntent(string intentId)
    {
        if (string.IsNullOrWhiteSpace(intentId)) return BadRequest("intentId is required");
        var payment = await _unitOfWork.Payments.GetByPaymentIntentIdAsync(intentId);
        if (payment == null) return NotFound("Payment not found for intent id");
        var updated = await _paymentService.CheckAndUpdatePaymentStatusAsync(payment.Id);
        return Ok(updated);
    }
}
