# Service Request Document Requirements - Quick Reference

## ?? What Changed?

Citizens **cannot submit** a service request until **all mandatory required documents are uploaded**.

## ?? Where to Find Changes

### Backend (C#)
**File**: `src/SmartEGov.Web/Controllers/ServiceRequestController.cs`

**New Method:**
- `ValidateAllMandatoryRequirementsMetAsync()` - Validates all mandatory docs are uploaded

**Updated Methods:**
- `Create()` POST - Now validates requirements before accepting submission
- `Edit()` POST - Now validates requirements before accepting submission

### Frontend (Razor)
**Files**: 
- `src/SmartEGov.Web/Views/ServiceRequest/Create.cshtml`
- `src/SmartEGov.Web/Views/ServiceRequest/Edit.cshtml`

**Changes:**
- Submit button disabled until all mandatory docs uploaded
- Real-time feedback showing missing documents
- Clear mandatory vs. optional badges
- Warning alert about requirements

## ?? User Flow

### Before (Old Way - ? REMOVED)
```
User fills form ? User uploads 1 document ? User clicks Submit ? ? May fail server-side
```

### After (New Way - ? WORKING)
```
User fills form
  ?
User selects service ? See requirements
  ?
User uploads documents ? Submit button enabled only when complete
  ?
User clicks Submit ? Always succeeds (all validation already done client-side)
  ?
OR
User clicks "Save as Draft" ? Saved immediately (no requirement to complete)
```

## ??? Two-Layer Validation

### Client-Side (JavaScript)
- ? Instant feedback
- Disables submit button
- Shows missing documents list
- Allows save as draft anytime

### Server-Side (C#)
- ?? Security layer
- Final validation before accepting
- Can't be bypassed by clever browser tricks
- Returns helpful error message if somehow incomplete

## ?? Key Difference: Draft vs. Submit

| Action | Requires All Documents? | Can Save? |
|--------|------------------------|-----------|
| **Save as Draft** | ? No | ? Always |
| **Submit Request** | ? Yes | ? Only if complete |

## ??? Document Labels

### ?? Required (Mandatory)
- Must upload to submit
- Example: National ID, Tax Certificate
- Set via: `RequiredDocument.IsMandatory = true`

### ? Optional
- Nice to have but not required
- Can be added anytime
- Set via: `RequiredDocument.IsMandatory = false`

## ?? How to Test

### Test 1: Can't Submit Without Documents
1. Go to Create Service Request page
2. Select a service with mandatory documents
3. Try to click Submit without uploading
4. ? Submit button should be **DISABLED** (grayed out)
5. ? Alert shows missing documents

### Test 2: Can Submit After Upload
1. Same as Test 1
2. Upload required documents that match the requirement names
3. ? Submit button **ENABLES** (becomes active/clickable)
4. ? Alert disappears
5. ? Can now click Submit

### Test 3: Can Always Save Draft
1. Select service with requirements
2. Upload 0 documents (or incomplete)
3. Click "Save as Draft"
4. ? Saves successfully
5. Later return and finish

### Test 4: Token Matching Works
1. Service requires: "National Identification"
2. Upload file: "National_ID_Card.pdf"
3. System should recognize this matches
4. ? Submit button enables

## ?? Common Issues & Solutions

**Problem: Submit button won't enable**
- Solution: Check that uploaded filename contains at least 2 tokens matching requirement
- Example: "ID" matches "National ID" but needs 2+ character words

**Problem: Can't save draft**
- Solution: Draft always works - try "Save as Draft" button instead of Submit

**Problem: Getting error message on submit despite button being enabled**
- Solution: Server validation failed - upload more/different files matching requirements

## ?? Configuration

### Where Requirements Come From
Database table: `RequiredDocuments`
Linked to: `GovernmentServices`

### How to Add Requirements
1. Go to Admin ? Government Services
2. Edit service
3. Add required documents with:
   - Name (e.g., "National ID")
   - Description (optional)
   - Mandatory checkbox

### File Upload Settings
File: `appsettings.json`
```json
"FileUpload": {
  "MaxFileSizeMB": 10,
  "AllowedExtensions": ".pdf,.doc,.docx,.jpg,.jpeg,.png,.xlsx,.xls"
}
```

## ?? Benefits

? No more incomplete submissions  
? Officers get all documents upfront  
? Citizens know exactly what's needed  
? Real-time feedback (no surprises at submit time)  
? Can still save incomplete drafts for later  
? Works even if JavaScript disabled (server validates too)  

## ?? Support

**For Citizens:**
- See mandatory vs. optional badges
- Upload matching files (token matching)
- Use "Save as Draft" if not ready

**For Admins:**
- Set IsMandatory flag on required documents
- Citizens will see requirement in real-time

**For Developers:**
- See `IMPLEMENTATION_DETAILS.md` for technical details
- See `SERVICE_REQUEST_DOCUMENT_REQUIREMENTS.md` for full documentation
