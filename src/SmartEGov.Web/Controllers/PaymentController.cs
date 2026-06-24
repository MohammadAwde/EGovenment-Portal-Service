using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;
using SmartEGov.Domain.Enums;

namespace SmartEGov.Web.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly IServiceRequestService _serviceRequestService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public PaymentController(
        IPaymentService paymentService,
        IServiceRequestService serviceRequestService,
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _paymentService = paymentService;
        _serviceRequestService = serviceRequestService;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _configuration = configuration;
    }

    [Authorize(Roles = "Citizen")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reconcile(int paymentId)
    {
        try
        {
            var updated = await _paymentService.CheckAndUpdatePaymentStatusAsync(paymentId);

            if (updated.Status == PaymentStatus.Completed)
            {
                TempData["Success"] = "Payment reconciled and marked as completed.";
            }
            else
            {
                TempData["Info"] = $"Payment status: {updated.Status}. Reconciliation did not mark it as completed.";
            }

            return RedirectToAction("Receipt", new { paymentId = updated.Id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Reconciliation failed: " + ex.Message;
            return RedirectToAction("Receipt", new { paymentId });
        }
    }


    [Authorize(Roles = "Citizen")]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var payments = await _paymentService.GetByUserIdAsync(userId);
        return View(payments);
    }

    [Authorize(Roles = "Citizen")]
    [HttpGet]
    public async Task<IActionResult> Pay(int serviceRequestId)
    {
        var request = await _serviceRequestService.GetByIdAsync(serviceRequestId);
        if (request == null) return NotFound();

        var isPaid = await _paymentService.IsServiceRequestPaidAsync(serviceRequestId);
        if (isPaid)
        {
            TempData["Error"] = "This service request has already been paid.";
            return RedirectToAction("Details", "ServiceRequest", new { id = serviceRequestId });
        }

        var govService = await _unitOfWork.GovernmentServices.GetByIdAsync(request.GovernmentServiceId);
        if (govService == null || govService.Fee <= 0)
        {
            TempData["Error"] = "This service has no fee to pay.";
            return RedirectToAction("Details", "ServiceRequest", new { id = serviceRequestId });
        }

        ViewBag.ServiceRequest = request;
        ViewBag.Fee = govService.Fee;
        ViewBag.ServiceName = govService.Name;
        return View();
    }

    [Authorize(Roles = "Citizen")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int serviceRequestId, PaymentMethod method)
    {
        var request = await _serviceRequestService.GetByIdAsync(serviceRequestId);
        if (request == null) return NotFound();

        var isPaid = await _paymentService.IsServiceRequestPaidAsync(serviceRequestId);
        if (isPaid)
        {
            TempData["Error"] = "This service request has already been paid.";
            return RedirectToAction("Details", "ServiceRequest", new { id = serviceRequestId });
        }

        var govService = await _unitOfWork.GovernmentServices.GetByIdAsync(request.GovernmentServiceId);
        if (govService == null || govService.Fee <= 0)
        {
            TempData["Error"] = "This service has no fee to pay.";
            return RedirectToAction("Details", "ServiceRequest", new { id = serviceRequestId });
        }

        var userId = _userManager.GetUserId(User)!;

        var createDto = new CreatePaymentDto
        {
            ServiceRequestId = serviceRequestId,
            UserId = userId,
            Amount = govService.Fee,
            Method = method
        };

        // Use the current request origin for Checkout redirects so Stripe returns to the running host/port
        try
        {
            var origin = $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
            createDto.BaseUrl = origin;
        }
        catch
        {
            // ignore and let service fall back to configured App:BaseUrl
        }

        var payment = await _paymentService.InitiatePaymentAsync(createDto);

        if (!string.IsNullOrWhiteSpace(payment.CheckoutUrl))
        {
            return Redirect(payment.CheckoutUrl);
        }

        return RedirectToAction("Process", new { paymentId = payment.Id });
    }

    [Authorize(Roles = "Citizen")]
    [HttpGet]
    public async Task<IActionResult> Process(int paymentId)
    {
        var payment = await _paymentService.GetByIdAsync(paymentId);
        if (payment == null) return NotFound();

        if (payment.Status != PaymentStatus.Pending)
            return RedirectToAction("Receipt", new { paymentId = payment.Id });

        // If payment is by card, expose a publishable key for optional client-side integrations
        if (payment.Method == PaymentMethod.CreditCard || payment.Method == PaymentMethod.DebitCard)
        {
            var publishable = _configuration["Stripe:PublishableKey"] ?? Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY");
            ViewBag.StripePublishableKey = publishable;
        }

        return View(payment);
    }

    [Authorize(Roles = "Citizen")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartCheckout(int paymentId)
    {
        var payment = await _paymentService.GetByIdAsync(paymentId);
        if (payment == null) return NotFound();

        if (payment.Status != PaymentStatus.Pending)
        {
            TempData["Error"] = "This payment is not pending.";
            return RedirectToAction("Receipt", new { paymentId });
        }

        if (payment.Method != PaymentMethod.CreditCard && payment.Method != PaymentMethod.DebitCard)
        {
            TempData["Error"] = "Checkout is only available for card payments.";
            return RedirectToAction("Process", new { paymentId });
        }

        var origin = $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
        var sessionUrl = await _paymentService.CreateCheckoutSessionAsync(paymentId, origin);
        if (!string.IsNullOrWhiteSpace(sessionUrl))
            return Redirect(sessionUrl);

        TempData["Error"] = "Unable to create Checkout session. Please try again.";
        return RedirectToAction("Process", new { paymentId });
    }

    [Authorize(Roles = "Citizen")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(int paymentId, string? cardNumber, string? cardHolder, string? expiryDate, string? cvv, string? payPalEmail)
    {
        try
        {
            var processDto = new ProcessPaymentDto
            {
                CardNumber = cardNumber,
                CardHolder = cardHolder,
                ExpiryDate = expiryDate,
                Cvv = cvv,
                PayPalEmail = payPalEmail
            };

            var result = await _paymentService.ProcessPaymentAsync(paymentId, processDto);

            if (result.Status == PaymentStatus.Completed)
            {
                TempData["Success"] = $"Payment of {result.Amount:N0} LBP completed successfully! Transaction: {result.TransactionNumber}";
                return RedirectToAction("Receipt", new { paymentId = result.Id });
            }

            TempData["Error"] = $"Payment failed: {result.FailureReason}";
            return RedirectToAction("Process", new { paymentId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Process", new { paymentId });
        }
        catch (KeyNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index");
        }
    }

    [Authorize(Roles = "Citizen")]
    [HttpGet]
    public async Task<IActionResult> Receipt(int paymentId)
    {
        // Attempt to reconcile status with Stripe before showing the receipt
        try
        {
            var reconciled = await _paymentService.CheckAndUpdatePaymentStatusAsync(paymentId);
            return View(reconciled);
        }
        catch
        {
            // Fallback to previous behavior
            var payment = await _paymentService.GetByIdAsync(paymentId);
            if (payment == null) return NotFound();
            return View(payment);
        }
    }
}
