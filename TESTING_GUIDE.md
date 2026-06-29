# Testing Guide - Service Request Document Requirements

## Pre-Test Setup

### 1. Ensure Database has Test Data

**Create a test government service with mandatory requirements:**

```sql
-- Create or update a service with requirements
UPDATE GovernmentServices 
SET Name = 'Visa Application',
    Description = 'International travel visa application',
    Fee = 150.00,
    IsActive = 1
WHERE Id = 1;  -- Adjust ID as needed

-- Add required documents
INSERT INTO RequiredDocuments (GovernmentServiceId, DocumentName, Description, IsMandatory)
VALUES 
    (1, 'Passport', 'Valid international passport', 1),
    (1, 'Visa Application Form', 'Completed visa form', 1),
    (1, 'Travel Itinerary', 'Flight and hotel bookings', 0);
```

Or use the Admin UI:
1. Admin ? Government Services
2. Edit service ? Add Required Documents
3. Set some as Mandatory (checkbox)
4. Save

### 2. Test Citizen Account

- Email: `citizen@test.com`
- Password: (from appsettings.json DefaultAdmin)
- Role: Citizen

## Test Cases

### Test 1: Cannot Submit Without Any Documents
**Objective**: Verify submit button is disabled when no documents uploaded

**Steps:**
1. Login as Citizen
2. Navigate to Create Service Request
3. Select "Visa Application" service
4. See required documents listed with badges
5. Don't upload any files
6. Try to click Submit button
7. **Expected**: Button should be DISABLED (grayed out)
8. **Verify**: Tooltip shows "Please upload all required documents"

**Expected Result**: ? PASS
- Submit button visually disabled
- Cannot click it
- Alert shows missing documents

**Troubleshooting if FAIL:**
- Check browser console for JavaScript errors
- Verify service has RequiredDocuments in database
- Clear browser cache

---

### Test 2: Submit Enabled After Uploading All Mandatory Documents
**Objective**: Verify submit button enables when all mandatory docs uploaded

**Steps:**
1. Start from Test 1 state
2. Upload file: `Passport_Copy.pdf`
3. Observe alert for missing documents
4. Upload file: `Visa_Application_Form.pdf`
5. **Expected**: Alert disappears, Submit button ENABLES
6. Click Submit
7. **Expected**: Form submits successfully (no error)

**Expected Result**: ? PASS
- Alert disappears when all mandatory docs present
- Submit button becomes clickable
- Form submits without errors

**Troubleshooting if FAIL:**
- Check uploaded filenames - must contain tokens matching requirements
  - "Passport" must contain word "passport" (case-insensitive)
  - "Visa Application Form" must contain "visa" or "application" or "form"
- Check browser console
- Verify files are actually uploading (check network tab)

---

### Test 3: Token Matching Works for Various Filenames
**Objective**: Verify intelligent filename matching

**Steps:**
1. Create new request for "Visa Application"
2. Upload these files in order, observing real-time feedback:
   - `Passport_123456.pdf` ? Should match "Passport"
   - `VISA-APPLICATION-FORM.docx` ? Should match "Visa Application Form"
   - `travel_itinerary_copy.xlsx` ? Should match "Travel Itinerary"

**After each upload observe:**
- Does alert update correctly?
- Are required documents detected?

**Expected Result**: ? PASS
- Each file recognized as matching requirement
- Alert updates after each upload
- Submit button enables after second file (both mandatory)

**Troubleshooting if FAIL:**
- Verify token matching: requirements split into words > 1 char
  - "Passport" ? ["Passport"]
  - "Visa Application" ? ["Visa", "Application"]
- Filenames must share at least one token
- Check JavaScript console for errors

---

### Test 4: Optional Documents Don't Block Submission
**Objective**: Verify optional documents not required

**Steps:**
1. Create request for "Visa Application"
2. Upload ONLY mandatory documents:
   - `Passport.pdf`
   - `Application_Form.pdf`
3. Do NOT upload "Travel Itinerary"
4. Click Submit
5. **Expected**: Submits successfully despite optional doc missing

**Expected Result**: ? PASS
- Submit button enabled with just mandatory docs
- Doesn't require optional documents
- Submission succeeds

---

### Test 5: Save as Draft Works Without Documents
**Objective**: Verify drafts can be saved anytime

**Steps:**
1. Create new request for "Visa Application"
2. Don't upload any files
3. Click "Save as Draft"
4. **Expected**: Saves successfully
5. Navigate back to this request
6. Verify status shows "Draft"

**Expected Result**: ? PASS
- Draft saved without documents
- Can return and complete later
- No validation on draft saves

---

### Test 6: Edit Existing Draft and Submit
**Objective**: Complete and submit a draft request

**Steps:**
1. Start from Test 5 (saved draft)
2. Navigate to Edit page for draft
3. Upload required documents
4. Click Submit
5. **Expected**: Submits successfully

**Expected Result**: ? PASS
- Can edit draft
- Documents uploaded
- Submission validated and accepted
- Status changes to "Submitted"

---

### Test 7: Server-Side Validation Catches Incomplete Submissions
**Objective**: Verify backend validation as safety measure

**Steps:**
1. Open browser DevTools (F12)
2. Open Network tab
3. Create request and upload only 1 of 2 required documents
4. Notice Submit button is disabled
5. Open Console and run:
   ```javascript
   document.getElementById('submitBtn').disabled = false;
   ```
6. Click Submit button
7. **Expected**: Server validation catches it and returns error

**Expected Result**: ? PASS
- Shows error message with missing documents
- Form redisplays with data intact
- No request created in broken state

**Note**: This tests security - should never happen in normal use

---

### Test 8: File Upload Validation Still Works
**Objective**: Verify file size and type validation

**Steps:**
1. Create test image file > 10 MB
2. Try to upload as document
3. **Expected**: Rejected before requirement validation
4. Try to upload `.exe` file
5. **Expected**: Rejected due to file type

**Expected Result**: ? PASS
- File validation happens BEFORE requirement checking
- Size limits enforced
- File type restrictions enforced

---

### Test 9: Service With No Requirements
**Objective**: Verify submit works with no requirements

**Steps:**
1. Create service with NO required documents
2. Create request for this service
3. Don't upload any files
4. **Expected**: Submit button is ENABLED
5. Click Submit
6. **Expected**: Submits successfully

**Expected Result**: ? PASS
- No requirements = no blocking
- Can submit empty
- Works as designed

---

### Test 10: Mobile/Responsive Behavior
**Objective**: Verify form works on mobile

**Steps:**
1. Open form on mobile device (or Chrome DevTools mobile mode)
2. Select service
3. See requirement badges
4. Upload files via mobile file picker
5. Verify submit button behavior
6. Submit form

**Expected Result**: ? PASS
- Layout responsive
- Touch targets adequate
- File upload works
- Validation works same as desktop

---

## Automated Test Cases (For QA Team)

### Test Class: ServiceRequestRequirementValidationTests

```csharp
[TestClass]
public class ServiceRequestRequirementValidationTests
{
    // Test 1: Missing all mandatory documents blocks submission
    [TestMethod]
    public async Task Create_WithoutMandatoryDocuments_ShouldFail()
    {
        // Arrange: Service with 2 mandatory requirements
        // Act: Submit without documents
        // Assert: ValidationException thrown with missing docs
    }

    // Test 2: All mandatory documents allows submission
    [TestMethod]
    public async Task Create_WithAllMandatoryDocuments_ShouldSucceed()
    {
        // Arrange: Upload all required files
        // Act: Submit form
        // Assert: Request created with Submitted status
    }

    // Test 3: Optional documents not required
    [TestMethod]
    public async Task Create_SkippingOptionalDocuments_ShouldSucceed()
    {
        // Arrange: Upload only mandatory docs
        // Act: Submit form
        // Assert: Success
    }

    // Test 4: Draft saves without requirements
    [TestMethod]
    public async Task Create_SaveDraft_SkipsRequirementValidation()
    {
        // Arrange: No documents
        // Act: Save as draft
        // Assert: Draft created with no error
    }

    // Test 5: Token matching works
    [TestMethod]
    public async Task TokenMatch_WithVariousFileNames_ShouldMatch()
    {
        // Arrange: Requirement "National ID"
        // Files: "NationalID.pdf", "national-id-card.pdf", etc.
        // Assert: All recognized as matches
    }
}
```

## Performance Checks

### Page Load Time
- Create form should load < 2 seconds
- Requirements fetch via AJAX < 1 second

### File Upload
- Multiple files should respond immediately
- Real-time validation < 100ms

### Form Submission
- Validation + database save < 2 seconds
- Redirect to Details page after success

## Browser Compatibility

Test on:
- ? Chrome/Edge (Latest)
- ? Firefox (Latest)
- ? Safari (Latest)
- ? Mobile Safari (iOS)
- ? Chrome Mobile (Android)

## Regression Testing

**After changes, verify:**
1. Can still download documents
2. Can still delete documents
3. Can still view document details
4. Officers can still request more info
5. Officers can still update status
6. Payment workflow still works
7. Workflow approval steps still work

## Known Issues & Workarounds

### Issue: Submit button not disabling
**Cause**: JavaScript not loading  
**Fix**: Clear browser cache, hard refresh F5

### Issue: Token matching too loose
**Workaround**: Use exact filename match in DocumentType field  
**Future**: Admin UI to set exact match requirement

### Issue: Can't submit on slow network
**Cause**: Validation completes but form still posting  
**Fix**: Wait for button to fully enable before clicking

## Success Criteria

? All 10 manual tests PASS  
? Submit button state management works  
? Real-time feedback accurate  
? Draft/Submit distinction clear  
? Error messages helpful  
? Mobile responsive  
? No JavaScript console errors  
? No regression in other features  

## Sign-Off Checklist

- [ ] All test cases passed
- [ ] No new bugs introduced
- [ ] Documentation complete
- [ ] Admin can configure requirements
- [ ] Citizens understand feedback
- [ ] Officers receive complete submissions
- [ ] Ready for production release
