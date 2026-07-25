using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Services;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ICitizenService _citizenService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _env;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AccountController> _logger;
    private readonly IAutoFillService _autoFillService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ICitizenService citizenService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IEmailSender emailSender,
        IWebHostEnvironment env,
        IUnitOfWork unitOfWork,
        IAutoFillService autoFillService,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _citizenService = citizenService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _env = env;
        _unitOfWork = unitOfWork;
        _autoFillService = autoFillService;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        // Request a redirect to the external login provider.
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrEmpty(remoteError))
        {
            ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
            return View(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
            return RedirectToAction(nameof(Login));

        // Try to sign in the user with this external login provider
        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signInResult.Succeeded)
        {
            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            await _auditLogService.LogAsync(user?.Id, "ExternalLogin", "ApplicationUser", user?.Id, null, $"User logged in with {info.LoginProvider}");
            return RedirectToLocal(returnUrl);
        }

        // If the user does not have an account, create one using the provider's email claim (if available)
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrEmpty(email))
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    IsActive = true,
                    FullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty
                };

                var createResult = await _userManager.CreateAsync(user);
                if (createResult.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Citizen");
                }
            }

            // Link the external login to the user
            var addLoginResult = await _userManager.AddLoginAsync(user, info);
            if (addLoginResult.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                await _auditLogService.LogAsync(user.Id, "ExternalLoginRegistered", "ApplicationUser", user.Id, null, $"User registered via {info.LoginProvider}");
                return RedirectToLocal(returnUrl);
            }
        }

        // If we reach here, we need to ask the user for an email or show an error
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["LoginProvider"] = info.LoginProvider;
        ModelState.AddModelError(string.Empty, "Unable to retrieve an email from the external provider. Please register using your email.");
        return View(nameof(Login));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(SmartEGov.Application.DTOs.ForgotPasswordDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        // Do not reveal whether the user exists
        if (user != null)
        {
            // Generate token and encode it using Base64 URL encoding to make it safe for URLs
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
            var encodedToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(tokenBytes);

            // Build absolute callback URL. Use the current request host/scheme so link works across environments.
            var callbackUrl = Url.Action("ResetPassword", "Account", new { token = encodedToken, email = user.Email }, Request.Scheme);

            // Render email template
            string templatePath = Path.Combine(_env.ContentRootPath, "Views", "Emails", "PasswordReset.html");
            string html;
            if (System.IO.File.Exists(templatePath))
            {
                html = await System.IO.File.ReadAllTextAsync(templatePath);
                html = html.Replace("{{CallbackUrl}}", callbackUrl)
                           .Replace("{{Email}}", user.Email ?? string.Empty);
            }
            else
            {
                html = $"<p>To reset your password click the following link: <a href=\"{callbackUrl}\">Reset password</a></p>";
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    await _emailSender.SendEmailAsync(user.Email, "Password Reset", html);
                }

                var notification = new SmartEGov.Domain.Entities.Notification
                {
                    UserId = user.Id,
                    Title = "Password Reset",
                    Message = "A password reset was requested for your account.",
                    IsRead = false
                };

                await _unitOfWork.Notifications.AddAsync(notification);
                await _unitOfWork.SaveChangesAsync();

                await _auditLogService.LogAsync(user.Id, "PasswordResetRequested", "ApplicationUser", user.Id, null, "Password reset requested.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email for user {Email}", model.Email);
            }
        }

        TempData["Info"] = "If an account with that email exists, a password reset link has been sent.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ResetPassword(string token, string email)
    {
        var model = new SmartEGov.Application.DTOs.ResetPasswordDto { Token = token, Email = email };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(SmartEGov.Application.DTOs.ResetPasswordDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            // Do not reveal that the user does not exist
            TempData["Success"] = "Password has been reset successfully.";
            return RedirectToAction(nameof(Login));
        }

        // Decode token produced by Base64 URL encoding in the email link
        string decodedToken;
        try
        {
            var tokenBytes = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(model.Token ?? string.Empty);
            decodedToken = System.Text.Encoding.UTF8.GetString(tokenBytes);
        }
        catch
        {
            ModelState.AddModelError(string.Empty, "Invalid password reset token.");
            return View(model);
        }

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);
        if (result.Succeeded)
        {
            await _auditLogService.LogAsync(user.Id, "PasswordReset", "ApplicationUser", user.Id, null, "User reset their password.");
            TempData["Success"] = "Your password has been reset successfully. You can now login.";
            return RedirectToAction(nameof(Login));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Citizen");

            await _auditLogService.LogAsync(
                user.Id, "UserRegistered", "ApplicationUser", user.Id,
                null, $"User {model.Email} registered successfully");

            _logger.LogInformation("User {Email} registered successfully", model.Email);

            // ?? Save DocumentProfile if OCR data was extracted ??????????????
            var extractedMethod = Request.Form["ExtractionMethod"].ToString();
            var extractedFirst = Request.Form["ExtractedFirstName"].ToString();
            var extractedLast = Request.Form["ExtractedLastName"].ToString();

            if (!string.IsNullOrEmpty(extractedFirst) || !string.IsNullOrEmpty(extractedLast))
            {
                try
                {
                    var profileDto = new DocumentProfileDto
                    {
                        DocumentType = "NationalID",
                        FirstName = extractedFirst,
                        LastName = extractedLast,
                        FatherName = Request.Form["ExtractedFatherName"].ToString(),
                        MotherName = Request.Form["ExtractedMotherName"].ToString(),
                        MotherLastName = Request.Form["ExtractedMotherLastName"].ToString(),
                        DocumentNumberMasked = Request.Form["ExtractedDocumentNumber"].ToString(),
                        RegistryNumber = Request.Form["ExtractedRegistryNumber"].ToString(),
                        Village = Request.Form["ExtractedVillage"].ToString(),
                        District = Request.Form["ExtractedDistrict"].ToString(),
                        Province = Request.Form["ExtractedProvince"].ToString(),
                        ExtractionMethod = string.IsNullOrEmpty(extractedMethod) ? "OCR" : extractedMethod
                    };

                    // Parse date of birth if provided
                    var dobStr = Request.Form["ExtractedDateOfBirth"].ToString();
                    if (DateTime.TryParse(dobStr, out var dob))
                        profileDto.DateOfBirth = dob;

                    await _autoFillService.SaveProfileAsync(profileDto, user.Id);
                    _logger.LogInformation("DocumentProfile saved for user {Email} from registration OCR", model.Email);
                }
                catch (Exception ex)
                {
                    // Don't fail registration if profile save fails
                    _logger.LogWarning(ex, "Failed to save DocumentProfile during registration for {Email}", model.Email);
                }
            }
            // ????????????????????????????????????????????????????????????????

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Create", "Citizen");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }


    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user != null && await _userManager.IsLockedOutAsync(user))
        {
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            var remainingTime = lockoutEnd?.Subtract(DateTimeOffset.UtcNow).Minutes ?? 0;

            ModelState.AddModelError(string.Empty, 
                $"Account is locked due to multiple failed login attempts. Please try again in {remainingTime} minutes.");

            _logger.LogWarning("User {Email} attempted to login while account is locked", model.Email);
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _auditLogService.LogAsync(
                user?.Id,
                "UserLogin",
                "ApplicationUser",
                user?.Id,
                null,
                $"User {model.Email} logged in successfully"
            );

            _logger.LogInformation("User {Email} logged in successfully", model.Email);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            await _auditLogService.LogAsync(
                user?.Id,
                "AccountLockedOut",
                "ApplicationUser",
                user?.Id,
                null,
                $"User {model.Email} account locked out due to failed login attempts"
            );

            _logger.LogWarning("User {Email} account locked out", model.Email);

            ModelState.AddModelError(string.Empty, 
                "Account locked due to multiple failed login attempts. Please try again in 15 minutes.");
            return View(model);
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "You are not allowed to sign in. Please confirm your email.");
            return View(model);
        }

        _logger.LogWarning("Failed login attempt for user {Email}", model.Email);

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = _userManager.GetUserId(User);
        var userEmail = _userManager.GetUserName(User);

        await _signInManager.SignOutAsync();

        await _auditLogService.LogAsync(
            userId,
            "UserLogout",
            "ApplicationUser",
            userId,
            null,
            $"User {userEmail} logged out"
        );

        _logger.LogInformation("User {Email} logged out", userEmail);

        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied()
    {
        return View();
    }
}
