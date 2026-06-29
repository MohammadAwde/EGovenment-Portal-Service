# Service Request Document Requirements Enhancement

## Overview
Citizens are now **required to upload all mandatory required documents** before they can submit a service request. This prevents incomplete submissions and ensures officers receive all necessary documentation upfront.

## Changes Made

### 1. **Backend Validation (ServiceRequestController.cs)**

#### New Validation Method: `ValidateAllMandatoryRequirementsMetAsync()`
- **Purpose**: Validates that all mandatory required documents are either already uploaded or being uploaded now
- **Parameters**:
  - `serviceRequestId`: The ID of the service request being validated
  - `governmentServiceId`: The ID of the government service
  - `newFiles`: List of files being uploaded in this submission
- **Returns**: A tuple containing:
  - `IsValid` (bool): Whether all mandatory requirements are met
  - `MissingDocuments` (List<string>): List of documents that are still missing
- **Logic**:
  1. Retrieves the service and its required documents
  2. Filters for only mandatory documents (where `IsMandatory == true`)
  3. Collects existing uploaded documents from the database
  4. Combines existing + new uploaded documents
  5. Checks each mandatory requirement against all available documents using token matching
  6. Returns missing documents list if validation fails

#### Updated `Create()` POST Method
- **For Submissions** (not drafts):
  1. Validates uploaded files match required document names
  2. Creates a temporary Draft request to get an ID
  3. Calls `ValidateAllMandatoryRequirementsMetAsync()` to check all requirements
  4. If validation fails, **deletes the temporary draft** and returns error message
  5. If validation passes, **updates the draft to Submitted status**
  6. Uploads all documents
- **For Drafts** (save as draft):
  1. Only validates file names match required documents
  2. Does NOT require all mandatory documents to be present
  3. Allows citizens to complete the form later

#### Updated `Edit()` POST Method
- **For Submissions** (not drafts):
  1. Validates uploaded files match required document names
  2. Calls `ValidateAllMandatoryRequirementsMetAsync()` to check all requirements
  3. If validation fails, returns error with missing documents list
  4. If validation passes, updates status to Submitted
  5. Uploads all documents
- **For Drafts** (save as draft):
  1. Only validates file names match required documents
  2. Does NOT require all mandatory documents
  3. Allows citizens to add documents incrementally

### 2. **Frontend Validation (Create.cshtml)**

#### Visual Indicators
- **Requirements Section**: Clearly displays which documents are required vs. optional
  - Mandatory documents show: ?? **Required** badge
  - Optional documents show: ? **Optional** badge
- **Missing Documents Alert**: Shows in real-time which mandatory documents are still missing
- **Submit Button**: Disabled until all mandatory requirements are met

#### Client-Side Logic
- `updateRequirementStatus()`: 
  - Called whenever files are selected or changed
  - Compares uploaded files against mandatory requirements
  - Disables submit button if any mandatory documents are missing
  - Shows alert with list of missing documents
  - Allows save as draft regardless of completion status

#### JavaScript Features
- **Token Matching**: Uses the same algorithm as the server for consistency
  - Normalizes document names (lowercase, non-alphanumeric to spaces)
  - Splits into tokens
  - Matches files that contain at least one matching token
- **Real-time Feedback**: Updates requirement status as user uploads/removes files
- **Auto-save**: Continues to save drafts every 15 seconds even if submission not allowed

### 3. **Frontend Validation (Edit.cshtml)**

#### Similar to Create.cshtml
- Mandatory vs. optional badges
- Missing documents alert
- Disabled submit button until requirements met
- Triggers requirement check on page load

#### Key Differences
- Operates on existing service request (update scenario)
- File input is simpler (standard HTML input, not drag-drop)
- Loads requirements based on current service selection

## User Experience Flow

### Creating a New Request
1. User selects a service
2. System displays required documents (mandatory vs optional)
3. **Mandatory badge**: "?? Required" (red)
4. **Optional badge**: "? Optional" (gray)
5. User uploads documents via drag-drop or file selection
6. Submit button is **disabled** with tooltip: "Please upload all required documents"
7. As user uploads files that match requirements, alert disappears
8. When all mandatory docs are present, submit button **enables**
9. Upon submission:
   - Server validates all requirements again
   - If missing, error message lists missing documents
   - Citizen must add documents and resubmit
10. User can save as draft anytime without uploading all documents

### Editing an Existing Request
1. User navigates to edit page for a draft request
2. System displays required documents
3. Same visual feedback and validation as Create
4. User can upload additional documents to complete submission
5. Once all mandatory docs are uploaded, user can submit
6. Submission validation same as new requests

## Error Handling

### Server-Side Errors (Displayed to User)
```
Before submitting your request, please upload all required documents:
• National ID
• Tax Certificate
• Employment Letter
```

### Client-Side Prevention
- Submit button disabled with visual feedback
- Tooltip shows why submission is prevented
- Alert box shows missing documents list

## Benefits

? **Prevents Incomplete Submissions**: Citizens cannot accidentally submit without required documents  
? **Reduces Processing Time**: Officers receive complete documentation upfront  
? **Clear Requirements**: Mandatory vs. optional status is obvious to citizens  
? **Real-time Feedback**: Citizens know immediately what's needed  
? **Draft Flexibility**: Drafts don't require all documents, encouraging partial completion  
? **Server-Side Safety**: All validation happens server-side too, preventing bypasses  
? **Token Matching**: Smart matching handles various naming conventions  

## Technical Implementation Details

### Token Matching Algorithm
```csharp
var reqName = "National ID";
var tokens = Regex.Replace(reqName, @"\W+", " ")
    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
    .Where(t => t.Length > 1)
    .ToArray();
// Result: ["National", "ID"]

// Checks for files like:
// - national_id.pdf ?
// - National-ID-scan.jpg ?
// - ID_national.png ?
// - national.pdf (missing "ID") ?
```

### Mandatory Documents Detection
```csharp
// From GovernmentService.RequiredDocuments
var mandatoryDocs = govService.RequiredDocuments
    .Where(d => d.IsMandatory)  // Only if IsMandatory == true
    .ToList();
```

### Document Matching Logic
1. First tries exact match on `DocumentType` field
2. Falls back to token-based heuristic matching on file names
3. Handles case-insensitive comparisons
4. Requires at least one token match

## Configuration

### File Upload Settings (appsettings.json)
```json
"FileUpload": {
  "MaxFileSizeMB": 10,
  "AllowedExtensions": ".pdf,.doc,.docx,.jpg,.jpeg,.png,.xlsx,.xls"
}
```

### Service Requirements (Database)
Set in `GovernmentService.RequiredDocuments`:
- `DocumentName`: Name of required document
- `Description`: Optional description for citizens
- `IsMandatory`: Set to `true` for required, `false` for optional

## Testing Scenarios

### Scenario 1: Create with Incomplete Requirements
1. User selects service with 3 mandatory documents
2. Uploads only 1 document
3. Clicks Submit ? Server validates ? Returns error with missing list
4. Submit button remains disabled until all 3 are uploaded

### Scenario 2: Save as Draft
1. User selects service with 3 mandatory documents
2. Uploads only 1 document
3. Clicks "Save as Draft" ? Draft saved successfully
4. Later, user returns to edit
5. Uploads remaining documents
6. Clicks Submit ? All validation passes ? Request submitted

### Scenario 3: Partial Token Match
1. User needs "National Identification"
2. Uploads file named "National_ID_Card.pdf"
3. Token matching finds "National" + "ID" in document
4. File is accepted as matching requirement

## Known Limitations

- Token matching is heuristic-based and case-insensitive
- Exact document type matching (via `DocumentType` field) takes precedence
- Single-token names (< 2 characters) are ignored in matching
- File extension validation is separate from requirement matching

## Future Enhancements

- Add document upload checklist with checkboxes
- Allow officers to explicitly mark documents received
- Implement document versioning (required vs. received)
- Add document scanning/OCR for automatic categorization
- Implement progress bar showing completion percentage
- Allow setting maximum number of documents per service
