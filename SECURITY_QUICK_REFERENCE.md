# Security Quick Reference Guide

## For Developers Working on SmartEGov

### Adding a New Controller Action

#### 1. Add Authorization Attribute
```csharp
[Authorize(Roles = "Admin")]  // or use Policy: [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> YourAction(YourModel model)
{
    // Your code here
}
```

#### 2. Add Audit Logging for Important Actions
```csharp
await _auditLogService.LogAsync(
    userId: _userManager.GetUserId(User),
    action: "ActionName",
    entityName: "EntityType",
    entityId: entityId.ToString(),
    oldValues: null,  // or JSON of old values
    newValues: "Description of what happened"
);
```

### File Upload Validation

#### In Controller
```csharp
private readonly IFileValidationService _fileValidationService;

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Upload(IFormFile file)
{
    var (isValid, errorMessage) = await _fileValidationService.ValidateFileAsync(file);
    if (!isValid)
    {
        ModelState.AddModelError("file", errorMessage);
        return View();
    }

    // Process file
}
```

### Forms - Always Include Anti-Forgery Token

```html
<form method="post" asp-action="YourAction" asp-controller="YourController">
    @Html.AntiForgeryToken()
    <!-- form fields -->
    <button type="submit">Submit</button>
</form>
```

Or using tag helpers:
```html
<form method="post" asp-action="YourAction" asp-controller="YourController">
    <!-- Anti-forgery token is automatically included with tag helpers -->
    <!-- form fields -->
    <button type="submit">Submit</button>
</form>
```

### Authorization Policies Available

```csharp
// Use one of these pre-defined policies
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Authorize(Policy = AuthorizationPolicies.OfficerOnly)]
[Authorize(Policy = AuthorizationPolicies.CitizenOnly)]
[Authorize(Policy = AuthorizationPolicies.AdminOrOfficer)]
[Authorize(Policy = AuthorizationPolicies.AllRoles)]

// Or use roles directly
[Authorize(Roles = "Admin")]
[Authorize(Roles = "Admin,Officer")]
```

### Password Requirements

When creating users programmatically:
- Minimum 8 characters
- At least 1 uppercase letter
- At least 1 lowercase letter
- At least 1 digit
- At least 1 special character
- At least 4 unique characters

Example valid passwords:
- `Admin@123456`
- `Officer#2024`
- `Citizen$Pass1`

### Audit Log Action Names (Use Consistently)

```csharp
// Authentication
"UserRegistered"
"UserLogin"
"UserLogout"
"AccountLockedOut"
"FailedLogin"

// User Management
"RoleChanged"
"UserActivated"
"UserDeactivated"

// Service Requests
"ServiceRequestCreated"
"ServiceRequestStatusUpdated"
"ServiceRequestCancelled"

// Documents
"DocumentUploaded"
"DocumentDeleted"

// Other
"DigitalSignatureUpdated"
"DigitalSignatureRemoved"
"SettingsChanged"
```

### Logging Best Practices

```csharp
// Inject ILogger
private readonly ILogger<YourController> _logger;

// Log security events
_logger.LogWarning("Failed login attempt for user {Email}", email);
_logger.LogInformation("User {Email} logged in successfully", email);
_logger.LogError(ex, "Error occurred while processing request");

// DO NOT log sensitive data
_logger.LogInformation("Processing payment"); // ? Good
_logger.LogInformation("Processing payment for card {CardNumber}", cardNumber); // ? Bad
```

### File Upload Configuration

Edit `appsettings.json`:
```json
{
  "FileUpload": {
    "MaxFileSizeInMB": 10,
    "AllowedExtensions": [".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".xlsx", ".xls"]
  }
}
```

### Security Headers

Headers are automatically added by middleware. No action needed in controllers.

To customize, edit: `src/SmartEGov.Infrastructure/Middleware/SecurityHeadersMiddleware.cs`

### Common Security Mistakes to Avoid

? **DON'T**:
```csharp
// Missing authorization
public async Task<IActionResult> DeleteUser(string id)

// Missing anti-forgery token
[HttpPost]
public async Task<IActionResult> ChangePassword(string newPassword)

// Not validating files
var file = Request.Form.Files[0];
await file.CopyToAsync(stream);

// Logging sensitive data
_logger.LogInformation("User password: {Password}", password);

// Using raw SQL without parameters
var sql = $"SELECT * FROM Users WHERE Email = '{email}'";
```

? **DO**:
```csharp
// With authorization
[Authorize(Roles = "Admin")]
public async Task<IActionResult> DeleteUser(string id)

// With anti-forgery token
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ChangePassword(string newPassword)

// Validate files first
var (isValid, error) = await _fileValidationService.ValidateFileAsync(file);
if (isValid)
{
    await file.CopyToAsync(stream);
}

// Don't log sensitive data
_logger.LogInformation("Password changed for user {UserId}", userId);

// Use EF Core or parameterized queries
var users = await _context.Users.Where(u => u.Email == email).ToListAsync();
```

### Testing Your Changes

#### Local Testing
1. Run the application
2. Try to access protected resources without login ? should redirect to login
3. Try invalid password 5 times ? should lock account
4. Upload invalid files ? should be rejected
5. Check browser dev tools ? verify security headers

#### Code Review Checklist
- [ ] All POST actions have `[ValidateAntiForgeryToken]`
- [ ] All sensitive actions have authorization
- [ ] File uploads are validated
- [ ] Important actions are audit logged
- [ ] No sensitive data in logs
- [ ] Error messages don't reveal system details

### Getting Help

1. Check `SECURITY.md` for detailed documentation
2. Review existing controllers for examples
3. Check audit logs to see what's being logged
4. Ask team lead for security review before deploying

### Quick Commands

```bash
# Build the project
dotnet build

# Run the project
dotnet run --project src/SmartEGov.Web

# Check for security vulnerabilities in packages
dotnet list package --vulnerable

# Update packages
dotnet add package PackageName
```

### Environment Variables for Production

Set these as environment variables (not in appsettings.json):

```bash
ConnectionStrings__DefaultConnection="your-production-connection-string"
DefaultAdmin__Password="your-secure-admin-password"
SmsSettings__AccountSid="your-twilio-sid"
SmsSettings__AuthToken="your-twilio-token"
```

### Helpful Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [ASP.NET Core Security](https://docs.microsoft.com/en-us/aspnet/core/security/)
- [Entity Framework Core Security](https://docs.microsoft.com/en-us/ef/core/miscellaneous/security)
- Internal: `SECURITY.md` in project root
