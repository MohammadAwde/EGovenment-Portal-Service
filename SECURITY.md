# SmartEGov Application Security Documentation

## Overview
This document outlines the comprehensive security measures implemented in the SmartEGov e-government portal application.

## Security Features Implemented

### 1. HTTPS Enforcement
- **HTTPS Redirection**: All HTTP requests are automatically redirected to HTTPS
- **HSTS (HTTP Strict Transport Security)**: Enforced with:
  - Preload enabled
  - Subdomain inclusion
  - Max-Age: 365 days
- **Secure Cookie Policy**: All cookies are set with `Secure` flag requiring HTTPS

**Location**: `Program.cs` - `app.UseHttpsRedirection()` and HSTS configuration

### 2. Enhanced Identity Password Policy
Strong password requirements enforced:
- **Minimum Length**: 8 characters
- **Required Character Types**:
  - At least 1 uppercase letter
  - At least 1 lowercase letter
  - At least 1 digit
  - At least 1 special character (non-alphanumeric)
- **Unique Characters**: Minimum 4 unique characters
- **Unique Email**: Email addresses must be unique across users

**Location**: `Program.cs` - Identity options configuration

### 3. Account Lockout Protection
Automatic account lockout after failed login attempts:
- **Max Failed Attempts**: 5 consecutive failed login attempts
- **Lockout Duration**: 15 minutes
- **Applies to New Users**: Lockout enabled for all users
- **Lockout Handling**: Users receive clear messages about lockout status and remaining time

**Location**: 
- `Program.cs` - Lockout configuration
- `AccountController.cs` - Login action with lockout handling

### 4. Role-Based Authorization Policies
Pre-defined authorization policies for consistent access control:
- **AdminOnly**: Requires Admin role
- **OfficerOnly**: Requires Officer role
- **CitizenOnly**: Requires Citizen role
- **AdminOrOfficer**: Requires Admin or Officer role
- **AllRoles**: Requires any authenticated user with a role

**Usage**: Apply `[Authorize(Policy = "PolicyName")]` to controllers or actions

**Location**: `Infrastructure/Authorization/AuthorizationPolicies.cs`

### 5. Anti-Forgery Token Validation
Protection against Cross-Site Request Forgery (CSRF) attacks:
- **Enhanced Configuration**:
  - Custom header name: `X-CSRF-TOKEN`
  - Secure cookies only (HTTPS required)
  - HttpOnly cookies (no JavaScript access)
  - SameSite: Strict mode
- **Automatic Validation**: All POST, PUT, DELETE requests require valid tokens
- **View Integration**: Use `@Html.AntiForgeryToken()` in forms or `[ValidateAntiForgeryToken]` attribute

**Location**: `Program.cs` - Antiforgery configuration

### 6. Security Headers Middleware
Custom middleware adding security-focused HTTP headers:

- **X-Content-Type-Options**: `nosniff` - Prevents MIME type sniffing
- **X-Frame-Options**: `DENY` - Prevents clickjacking attacks
- **X-XSS-Protection**: `1; mode=block` - Enables XSS filter
- **Referrer-Policy**: `strict-origin-when-cross-origin` - Controls referrer information
- **Permissions-Policy**: Disables geolocation, microphone, camera
- **Content-Security-Policy**: 
  - Restricts resource loading to trusted sources
  - Allows self-hosted resources
  - Permits specific CDNs (Bootstrap, etc.)
  - Prevents framing from other domains

**Location**: `Infrastructure/Middleware/SecurityHeadersMiddleware.cs`

### 7. Secure File Upload Validation
Comprehensive file validation service:

**Validations Performed**:
- **File Size**: Configurable maximum size (default: 10MB)
- **File Extension**: Whitelist of allowed extensions (.pdf, .doc, .docx, .jpg, .jpeg, .png, .xlsx, .xls)
- **MIME Type**: Validation against allowed content types
- **File Content**: Magic number/file signature validation to prevent spoofing
- **Filename Sanitization**: Prevents path traversal and invalid characters

**Supported File Types with Signature Validation**:
- PDF documents
- Microsoft Word (.doc, .docx)
- Microsoft Excel (.xls, .xlsx)
- Images (JPEG, PNG)

**Location**: 
- `Infrastructure/Services/FileValidationService.cs`
- `Infrastructure/Services/DocumentService.cs` - Integration

### 8. Comprehensive Audit Logging
Audit logging for important security and business actions:

**Logged Events**:
- User Registration
- User Login (successful)
- User Logout
- Account Lockout
- Role Changes
- Service Request Creation
- Service Request Status Updates
- Service Request Cancellation
- Digital Signature Upload/Removal
- Other administrative actions

**Audit Log Information**:
- User ID performing the action
- Action type
- Entity affected
- Entity ID
- Old values (when applicable)
- New values (when applicable)
- Timestamp (automatic)

**Location**: 
- `Application/Services/IAuditLogService.cs`
- `Infrastructure/Services/AuditLogService.cs`
- Integrated in: `AccountController`, `ServiceRequestController`, `AdminController`, `OfficerController`

### 9. Secure Session Management
Enhanced cookie and session security:
- **Cookie Settings**:
  - HttpOnly: Prevents JavaScript access
  - Secure: HTTPS only
  - SameSite: Strict mode
- **Session Timeout**: 2 hours with sliding expiration
- **Custom Login Path**: `/Account/Login`
- **Custom Access Denied Path**: `/Account/AccessDenied`

**Location**: `Program.cs` - Cookie authentication configuration

## Configuration

### appsettings.json Security Settings

```json
{
  "FileUpload": {
    "MaxFileSizeInMB": 10,
    "AllowedExtensions": [".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".xlsx", ".xls"]
  }
}
```

### Environment-Specific Security

**Production**:
- Exception handler: `/Home/Error`
- HSTS enabled
- Security headers active
- Detailed errors disabled

**Development**:
- Developer exception page
- Security headers still active
- Detailed error messages for debugging

## Best Practices Applied

### Input Validation
- All user inputs validated on both client and server side
- Model validation with data annotations
- Anti-forgery tokens on all state-changing operations

### Secure Data Storage
- Passwords hashed using Identity's default secure hash algorithm (PBKDF2)
- Sensitive configuration in appsettings (should be moved to environment variables or Azure Key Vault in production)

### Least Privilege Principle
- Role-based access control
- Authorization policies on all sensitive operations
- Separate roles for Admin, Officer, and Citizen

### Defense in Depth
- Multiple layers of security controls
- Security headers + HTTPS + authentication + authorization
- File validation at multiple levels

## Additional Recommendations for Production

1. **Move Secrets to Secure Storage**:
   - Use Azure Key Vault or environment variables for connection strings
   - Remove sensitive data from appsettings.json

2. **Enable Email Confirmation**:
   - Set `options.SignIn.RequireConfirmedEmail = true`
   - Implement email verification workflow

3. **Implement Rate Limiting**:
   - Add rate limiting middleware to prevent brute force attacks
   - Consider using AspNetCoreRateLimit package

4. **Add Two-Factor Authentication (2FA)**:
   - Implement 2FA for admin accounts at minimum
   - Use Identity's built-in 2FA support

5. **Database Security**:
   - Use parameterized queries (already done via EF Core)
   - Enable database encryption at rest
   - Implement database audit logging

6. **Monitoring and Alerting**:
   - Implement Application Insights or similar
   - Set up alerts for security events (multiple failed logins, etc.)
   - Regular security audit log reviews

7. **Content Security Policy Refinement**:
   - Review and tighten CSP as needed for your specific requirements
   - Remove 'unsafe-inline' and 'unsafe-eval' if possible

8. **Regular Security Updates**:
   - Keep all NuGet packages updated
   - Monitor security advisories for .NET and dependencies
   - Implement automated vulnerability scanning

## Testing Security Features

### Test Account Lockout
1. Attempt to login with wrong password 5 times
2. Verify account is locked for 15 minutes
3. Verify appropriate error message is shown

### Test File Upload Validation
1. Try uploading file larger than 10MB - should be rejected
2. Try uploading disallowed file type (.exe, .bat) - should be rejected
3. Try uploading file with spoofed extension - should be rejected

### Test Authorization
1. Try accessing admin routes as Officer - should see Access Denied
2. Try accessing citizen routes as Officer - should see Access Denied
3. Verify each role can only access their designated areas

### Test CSRF Protection
1. Remove anti-forgery token from form
2. Submit form - should be rejected

### Test Security Headers
1. Use browser developer tools to inspect response headers
2. Verify all security headers are present
3. Test CSP by attempting to load external scripts

## Compliance Considerations

This implementation addresses common security requirements for:
- **OWASP Top 10** vulnerabilities
- **GDPR** data protection requirements (partial - audit logging, secure storage)
- **ISO 27001** information security controls
- **PCI DSS** (if handling payment card data)

## Audit Log Queries

Common queries for security monitoring:

```csharp
// Get all failed login attempts
var failedLogins = auditLogs.Where(a => a.Action == "FailedLogin");

// Get all role changes
var roleChanges = auditLogs.Where(a => a.Action == "RoleChanged");

// Get all actions by a specific user
var userActions = auditLogs.Where(a => a.UserId == userId);

// Get recent security events
var recentEvents = auditLogs
    .Where(a => a.Timestamp >= DateTime.UtcNow.AddDays(-7))
    .OrderByDescending(a => a.Timestamp);
```

## Security Incident Response

If a security incident is detected:

1. **Immediate Actions**:
   - Review audit logs for the affected timeframe
   - Lock affected user accounts if necessary
   - Preserve logs for investigation

2. **Investigation**:
   - Check audit logs for unauthorized actions
   - Review access patterns
   - Identify scope of breach

3. **Remediation**:
   - Reset passwords for affected accounts
   - Update security rules if needed
   - Apply additional restrictions if necessary

4. **Prevention**:
   - Update security policies based on findings
   - Implement additional monitoring
   - Review and update access controls

## Maintenance

Regular security maintenance tasks:
- Review audit logs weekly
- Update NuGet packages monthly
- Review user roles and permissions quarterly
- Conduct security assessments annually
- Update security policies as needed
