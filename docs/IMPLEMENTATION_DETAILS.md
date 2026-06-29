# Implementation Notes: Service Request Document Requirements

## Code Changes Summary

### Files Modified

1. **src/SmartEGov.Web/Controllers/ServiceRequestController.cs**
   - Added `ValidateAllMandatoryRequirementsMetAsync()` method (new)
   - Updated `Create()` POST method with requirement validation
   - Updated `Edit()` POST method with requirement validation

2. **src/SmartEGov.Web/Views/ServiceRequest/Create.cshtml**
   - Added requirement status alert section
   - Updated submit button with disabled state
   - Enhanced JavaScript for real-time validation
   - Improved visual hierarchy with badges

3. **src/SmartEGov.Web/Views/ServiceRequest/Edit.cshtml**
   - Added requirement status alert section
   - Updated submit button with disabled state
   - Added JavaScript validation logic
   - Improved document requirement display

## Key Methods Explained

### ValidateAllMandatoryRequirementsMetAsync()

**Signature:**
```csharp
private async Task<(bool IsValid, List<string> MissingDocuments)> 
    ValidateAllMandatoryRequirementsMetAsync(
        int serviceRequestId, 
        int governmentServiceId, 
        IEnumerable<IFormFile>? newFiles)
```

**Flow:**
```
1. Check if service has any required documents
   ?? No ? Return (true, empty list) ?
   ?? Yes ? Continue

2. Get mandatory documents only (IsMandatory == true)
   ?? None ? Return (true, empty list) ?
   ?? Some ? Continue

3. Retrieve existing uploaded documents from DB
   ?? Add to collection

4. For each mandatory document:
   ?? Tokenize requirement name
   ?? For each existing + new document:
   ?  ?? Tokenize file name
   ?  ?? Check for token match
   ?? If match found ? Mark as present

5. Collect missing documents
   ?? Return (MissingCount == 0, MissingList)
```

**Token Matching Example:**
```
Requirement: "National Identification Document"
Normalized: "national identification document"
Tokens: ["national", "identification", "document"]

File: "National-ID-Card.pdf"
Normalized: "national-id-card.pdf"
Tokens: ["national", "id", "card"]

Match: YES (because "national" appears in both)
```

### Create() POST Method Flow

**When User Clicks "Submit Request":**
```csharp
if (!saveAsDraft)  // This is submission path
{
    // 1. Validate file names match required documents
    var (isValidFileNames, fileNameError) = 
        await ValidateUploadedFilesMatchRequirementsAsync(...);
    if (!isValidFileNames)
        return View with error;

    // 2. Create temporary draft to get ID
    var tempModel = model;
    tempModel.Status = ServiceRequestStatus.Draft;
    var tempRequest = await _serviceRequestService.CreateAsync(tempModel);

    // 3. Validate ALL mandatory requirements
    var (allRequirementsMet, missingDocs) = 
        await ValidateAllMandatoryRequirementsMetAsync(
            tempRequest.Id,                    // ID from temporary draft
            model.GovernmentServiceId,         // Service being requested
            documents);                        // Files being uploaded now

    if (!allRequirementsMet && missingDocs.Any())
    {
        // 4. DELETE temporary draft on validation failure
        await _serviceRequestService.DeleteAsync(tempRequest.Id);

        // Return error with missing documents
        ModelState.AddModelError("", 
            $"Please upload: {string.Join(", ", missingDocs)}");
        return View(model);
    }

    // 5. Update draft to Submitted
    tempModel.Id = tempRequest.Id;
    tempModel.Status = ServiceRequestStatus.Submitted;
    var created = await _serviceRequestService.UpdateAsync(tempModel);

    // 6. Upload documents
    if (documents != null && documents.Count > 0)
        await _serviceRequestService.UploadDocumentsAsync(created.Id, documents);

    return RedirectToAction("Details", new { id = created.Id });
}
```

**When User Clicks "Save as Draft":**
```csharp
if (saveAsDraft)
{
    // Only validate file names, NOT mandatory requirements
    var (isValidNames, fileError) = 
        await ValidateUploadedFilesMatchRequirementsAsync(...);

    if (!isValidNames)
        return View with error;

    model.Status = ServiceRequestStatus.Draft;
    var createdDraft = await _serviceRequestService.SaveDraftAsync(model);

    if (documents != null && documents.Count > 0)
        await _serviceRequestService.UploadDocumentsAsync(createdDraft.Id, documents);

    TempData["Success"] = "Service request saved as draft.";
    return RedirectToAction("Details", new { id = createdDraft.Id });
}
```

### Edit() POST Method Flow

Similar to Create(), but:
- Works with existing service request ID (no temporary creation)
- Directly validates against the existing request
- Updates existing record instead of creating new one

## JavaScript Validation (Client-Side)

### updateRequirementStatus() Function

```javascript
function updateRequirementStatus() {
    // 1. Get mandatory documents from service
    const mandatoryDocs = requiredDocuments.filter(d => d.isMandatory);

    // 2. For each mandatory document
    const missingDocs = [];
    mandatoryDocs.forEach(doc => {
        // Tokenize requirement
        const docTokens = tokenize(doc.documentName);

        let hasDoc = false;

        // Check each uploaded file
        selectedFiles.forEach(file => {
            const fileTokens = tokenize(file.name);

            // Match if any token intersects
            if (docTokens.some(dt => fileTokens.includes(dt))) {
                hasDoc = true;
            }
        });

        if (!hasDoc)
            missingDocs.push(doc.documentName);
    });

    // 3. Update UI
    if (missingDocs.length > 0) {
        submitBtn.disabled = true;
        requirementStatus.style.display = 'block';
        missingDocsList.innerHTML = missingDocs.join(', ');
    } else {
        submitBtn.disabled = false;
        requirementStatus.style.display = 'none';
    }
}
```

## Validation Flow Diagram

```
User Action: Click Submit
        ?
Service selected? 
    ?? NO ? Show error, stop
    ?? YES ?

File names match requirements?
    ?? NO ? Show error, stop
    ?? YES ?

Create temp draft request
        ?
Check ALL mandatory documents present:
    - Existing uploads? ?
    - New uploads? ?

    ?? Missing ANY ? Delete draft, show error, stop
    ?? ALL present ?

Update draft ? Submitted
        ?
Upload documents
        ?
Success! Redirect to Details
```

## Database Considerations

### RequiredDocument Entity
```csharp
public class RequiredDocument
{
    public int Id { get; set; }
    public int GovernmentServiceId { get; set; }
    public string DocumentName { get; set; }           // e.g., "National ID"
    public string? Description { get; set; }          // e.g., "A valid national..."
    public bool IsMandatory { get; set; }             // true = must upload
    public GovernmentService GovernmentService { get; set; }
}
```

### Document Entity
```csharp
public class Document
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    public string FileName { get; set; }              // Uploaded file name
    public string FilePath { get; set; }              // /uploads/guid_filename
    public string? DocumentType { get; set; }         // Explicit type category
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public DateTime UploadedAt { get; set; }
    public ServiceRequest ServiceRequest { get; set; }
}
```

## Error Messages

### Server-Side Validation Errors

**File Name Mismatch:**
```
The following files do not match required documents for this service: 
resume.pdf, photo.jpg. 
Required documents are: National ID, Tax Certificate, Employment Letter
```

**Missing Mandatory Documents:**
```
Before submitting your request, please upload all required documents:
• National ID
• Tax Certificate
• Employment Letter
```

### Client-Side Disabled State

When submit button is disabled:
- Button shows greyed out appearance
- Tooltip text: "Please upload all required documents"
- Alert box shows which documents are missing
- File upload area remains fully functional

## Performance Considerations

1. **Database Queries**: 
   - `GetWithDetailsAsync()` includes RequiredDocuments (single query with includes)
   - `GetByServiceRequestIdAsync()` for documents (cached in service if available)

2. **Token Matching**:
   - Lightweight string operations
   - O(n*m) where n = required docs, m = uploaded files
   - No regex in client-side matching (performance optimized)

3. **Draft Creation**:
   - Temporary draft is created then updated/deleted
   - Could be optimized to skip creation and only validate
   - Current approach ensures consistent ID handling

## Testing Checklist

- [ ] Create request with complete documents ? Submit succeeds
- [ ] Create request with incomplete documents ? Submit fails with list
- [ ] Save incomplete request as draft ? Succeeds
- [ ] Edit draft to complete ? Submit succeeds
- [ ] Token matching works (e.g., "ID" matches "National_ID_Card.pdf")
- [ ] Optional documents don't block submission
- [ ] Multiple mandatory documents all checked
- [ ] File validation still works (size, type)
- [ ] Service with no requirements allows submission
- [ ] Existing uploads + new uploads combined in validation
- [ ] Form validation summary shows clear error messages
- [ ] Submit button disabled state is visually obvious
- [ ] Auto-save still works for drafts

## Security Considerations

? Server-side validation always happens (don't trust client)  
? Anti-forgery tokens validated on all POST actions  
? Authorization checked (Citizen can only edit own requests)  
? Temporary draft cleanup on validation failure  
? File size and type validation separate from requirement validation  
? No path traversal possible (using Guid for file names)  

## Future Optimization Opportunities

1. **Combine Validations**: Skip temporary draft creation
2. **Batch Processing**: Validate multiple submissions in parallel
3. **Caching**: Cache required documents list at service level
4. **Event Publishing**: Emit domain events on submission milestone
5. **Async Validation**: Validate requirements as files upload (no temp draft)
