# Service Request Feature - Quick Reference Guide

## Overview
The Service Request feature implements a complete workflow for citizens to submit government service requests with supporting documents.

## Key Workflows

### 1. Citizen Creates Service Request

```
Create Request Form
  ?
Select Service (validates against available services)
  ?
View Required Documents (loaded dynamically)
  ?
Enter Details & Notes
  ?
Upload Supporting Documents
  ?
Choose: Save as Draft or Submit
  ?
Auto-generate Reference Number
  ?
Initialize Approval Workflow (if service has workflow)
  ?
Send Confirmation Notification
```

**Reference**: `ServiceRequestController.Create()` ? `ServiceRequestService.CreateAsync()` or `SaveDraftAsync()`

### 2. Citizen Edits Draft Request

```
View Draft Requests
  ?
Edit Request Details
  ?
Add/Remove Documents
  ?
Save Changes or Submit
```

**Reference**: `ServiceRequestController.Edit()` ? `ServiceRequestService.UpdateAsync()`

### 3. Citizen Uploads Supporting Documents

```
View Request Details
  ?
Drag & Drop or Click to Upload Files
  ?
Client-side Validation:
  - Check file size (<= maxSizeMB)
  - Check file type (whitelist)
  - Show preview
  ?
Server-side Validation:
  - Sanitize filename
  - Scan for malware
  - Store with unique name
  ?
Display Success Message
```

**Reference**: `ServiceRequestController.UploadDocument()` ? `DocumentService.UploadMultipleAsync()`

### 4. Officer Reviews Pending Requests

```
View Pending Requests (grouped by status)
  ?
Click Review Button
  ?
View Request Details
  ?
Review Supporting Documents
  ?
View Approval Workflow Status
  ?
Choose Next Status:
  - Approve ? Complete with document
  - Reject ? Add rejection comments
  - Forward ? Move to next officer
  ?
Send Notification to Citizen
  ?
Log Action in Audit Trail
```

**Reference**: `ServiceRequestController.Pending()` ? Details view ? `UpdateStatus()`

### 5. Admin Views All Requests

```
Admin Dashboard
  ?
View Request Statistics by Status
  ?
Access All Requests in System
  ?
Filter/Search as needed
  ?
Audit logs available for compliance
```

## API Endpoints

### Service Request Operations
| Endpoint | Method | Role | Purpose |
|----------|--------|------|---------|
| `/ServiceRequest/Create` | GET | Citizen | Show creation form |
| `/ServiceRequest/Create` | POST | Citizen | Submit new request |
| `/ServiceRequest/Edit/{id}` | GET | Citizen | Edit draft form |
| `/ServiceRequest/Edit/{id}` | POST | Citizen | Save changes |
| `/ServiceRequest/Details/{id}` | GET | All | View request |
| `/ServiceRequest/Index` | GET | All | List requests |
| `/ServiceRequest/Pending` | GET | Officer/Admin | View pending |
| `/ServiceRequest/UpdateStatus` | POST | Officer/Admin | Update status |
| `/ServiceRequest/Cancel/{id}` | POST | Citizen | Cancel request |

### Document Operations
| Endpoint | Method | Role | Purpose |
|----------|--------|------|---------|
| `/ServiceRequest/UploadDocument` | POST | Citizen | Upload documents |
| `/ServiceRequest/DeleteDocument` | POST | Citizen/Admin | Remove document |
| `/ServiceRequest/DownloadDocument/{id}` | GET | All | Download document |
| `/ServiceRequest/DownloadCompletionFile/{id}` | GET | All | Download result |
| `/ServiceRequest/GetRequiredDocuments` | GET | All | Get requirements |

## Status Transitions

```
Draft
  ? (Submit)
Submitted
  ? (Review)
UnderReview
  ??? (Approve)
  ?   ?
  ?   Approved
  ?   ? (Complete)
  ?   Completed
  ?
  ??? (Reject)
      ?
      Rejected

Cancelled (from Draft or Submitted)
```

## File Upload Configuration

**Location**: `appsettings.json`

```json
{
  "FileUpload": {
    "MaxFileSizeInMB": 10,
    "AllowedExtensions": [
      ".pdf",
      ".doc",
      ".docx",
      ".jpg",
      ".jpeg",
      ".png",
      ".xlsx",
      ".xls"
    ]
  }
}
```

**Storage Location**: `wwwroot/uploads/`

## Key Classes

### Domain Models
- `ServiceRequest`: Main request entity
- `Document`: Uploaded files
- `ServiceRequestStatus`: Enum with status values

### Services
- `IServiceRequestService`: Service request operations
- `IDocumentService`: Document operations
- `IFileValidationService`: File validation & malware scanning

### Controllers
- `ServiceRequestController`: All request/document endpoints

### DTOs
- `ServiceRequestDto`: Request data transfer object
- `DocumentDto`: Document data transfer object

## Form Validation

### Client-Side (JavaScript)
- File size validation
- File type validation
- Service selection requirement
- Real-time error display

### Server-Side (C#)
- Model state validation
- Authorization checks
- File validation
- Malware scanning
- Status transition rules

## Security Features

1. **File Upload Security**
   - Filename sanitization
   - GUID-based unique naming
   - Malware scanning
   - Type whitelist validation
   - Size limit enforcement

2. **Authorization**
   - Citizens can only access own requests
   - Officers can only access assigned services
   - Admins have full access
   - Document deletion restricted

3. **Audit Trail**
   - All actions logged
   - User tracking
   - Status changes recorded
   - Document uploads tracked

4. **Data Protection**
   - Files stored outside web root
   - Secure file paths
   - HTTPS enforced
   - CSRF tokens on forms

## Common Tasks

### Add New Government Service
1. Create service in Admin ? Government Services
2. Optionally assign approval workflow
3. Add required document types
4. Assign to departments/locations
5. Citizens will see it in request creation

### Change File Upload Settings
1. Edit `appsettings.json`
2. Update `MaxFileSizeInMB`
3. Modify `AllowedExtensions` array
4. Restart application

### Monitor Service Requests
1. Go to Admin Dashboard
2. View status breakdown chart
3. Click to view all requests
4. Use Pending view for officer queue
5. Check audit logs for compliance

### Troubleshoot Upload Issues
1. Check file size vs. MaxFileSizeInMB setting
2. Verify file extension in AllowedExtensions
3. Ensure storage directory permissions
4. Check malware scanner logs
5. Review audit trail for error details

## Testing Scenarios

### Test Case 1: Complete Request Lifecycle
1. Create account as citizen
2. Submit service request
3. Upload supporting documents
4. View status as citizen
5. Login as officer
6. Review request and documents
7. Approve request
8. Upload completion document
9. Citizen downloads result

### Test Case 2: Draft Management
1. Start creating request
2. Upload some documents
3. Save as draft
4. Logout and login later
5. Edit draft
6. Add more documents
7. Submit draft

### Test Case 3: File Validation
1. Try uploading oversized file (should fail)
2. Try uploading wrong file type (should fail)
3. Upload correct file
4. Try malicious file (should be rejected by scanner)

## Performance Notes

- Document uploads are asynchronous
- File storage optimized for quick retrieval
- Database indexes on common queries
- Caching on service requirements
- Consider async file processing for large files

## Related Features

- **Payment Processing**: Service fees payment
- **Workflow Engine**: Approval workflow management
- **Notifications**: Email notifications on status changes
- **Audit Logging**: Complete action history
- **Location Services**: Find nearest office
- **Department Management**: Service location tracking

## Support Resources

- See `IMPLEMENTATION_SUMMARY.md` for detailed feature list
- Review code comments in Controllers and Services
- Check database migrations for schema
- Review test files for usage examples

## Quick Debug Checklist

- [ ] File upload folder exists and is writable
- [ ] Database connection string is correct
- [ ] FileUpload settings in appsettings.json
- [ ] User has correct role assignment
- [ ] Service workflow is configured
- [ ] File validation service is registered
- [ ] Audit logging is enabled
- [ ] Notification service is working
