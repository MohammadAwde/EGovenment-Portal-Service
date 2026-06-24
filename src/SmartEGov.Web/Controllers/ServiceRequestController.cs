using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SmartEGov.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Web.Controllers;

[Authorize]
public class ServiceRequestController : Controller
{
    private readonly IServiceRequestService _serviceRequestService;
    private readonly ICitizenService _citizenService;
    private readonly IDocumentService _documentService;
    private readonly IWorkflowService _workflowService;
    private readonly IPaymentService _paymentService;
    private readonly ILocationService _locationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ServiceRequestController> _logger;

    public ServiceRequestController(
        IServiceRequestService serviceRequestService,
        ICitizenService citizenService,
        IDocumentService documentService,
        IWorkflowService workflowService,
        IPaymentService paymentService,
        ILocationService locationService,
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        ILogger<ServiceRequestController> logger)
    {
        _serviceRequestService = serviceRequestService;
        _citizenService = citizenService;
        _documentService = documentService;
        _workflowService = workflowService;
        _paymentService = paymentService;
        _locationService = locationService;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _configuration = configuration;
        _environment = environment;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetRequiredDocuments(int serviceId)
    {
        try
        {
            var svc = await _unitOfWork.GovernmentServices.GetWithDetailsAsync(serviceId);
            if (svc == null) return Json(Array.Empty<object>());

            var docs = svc.RequiredDocuments?.Select(d => new {
                documentName = d.DocumentName,
                description = d.Description,
                isMandatory = d.IsMandatory
            }) ?? Enumerable.Empty<object>();

            return Json(docs);
        }
        catch
        {
            return Json(Array.Empty<object>());
        }
    }

    [Authorize(Roles = "Citizen")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var request = await _serviceRequestService.GetByIdAsync(id);
        if (request == null) return NotFound();

        // Only allow owner to edit drafts or when more info requested
        var userId = _userManager.GetUserId(User);
        var citizen = await _citizenService.GetByUserIdAsync(userId!);
        if (citizen == null) return Forbid();

        if (request.CitizenId != citizen.Id)
            return Forbid();

        if (request.Status != Domain.Enums.ServiceRequestStatus.Draft && request.Status != Domain.Enums.ServiceRequestStatus.Submitted && request.Status != Domain.Enums.ServiceRequestStatus.PendingInformation)
            return BadRequest("Only draft, submitted, or requests needing information can be edited.");

        var services = await _unitOfWork.GovernmentServices.GetActiveServicesAsync();
        ViewBag.Services = new SelectList(services, "Id", "Name", request.GovernmentServiceId);
        PopulateFileUploadSettings();
        return View(request);
    }

    [Authorize(Roles = "Citizen")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ServiceRequestDto model, List<IFormFile>? documents, bool saveAsDraft = false)
    {
        var userId = _userManager.GetUserId(User);
        var citizen = await _citizenService.GetByUserIdAsync(userId!);
        if (citizen == null) return RedirectToAction("Create", "Citizen");

        model.CitizenId = citizen.Id;

        if (!ModelState.IsValid)
        {
            var services = await _unitOfWork.GovernmentServices.GetActiveServicesAsync();
            ViewBag.Services = new SelectList(services, "Id", "Name", model.GovernmentServiceId);
            PopulateFileUploadSettings();
            return View(model);
        }

        model.Status = saveAsDraft ? Domain.Enums.ServiceRequestStatus.Draft : ServiceRequestStatus.Submitted;

        // If submitting (not saving as draft), ensure there is at least one document either existing or uploaded now
        if (!saveAsDraft)
        {
            var existingDocs = await _documentService.GetByServiceRequestIdAsync(model.Id);
            var existingCount = existingDocs?.Count() ?? 0;
            if ((documents == null || documents.Count == 0) && existingCount == 0)
            {
                ModelState.AddModelError("", "Please upload at least one supporting document before submitting the request.");
                var services = await _unitOfWork.GovernmentServices.GetActiveServicesAsync();
                ViewBag.Services = new SelectList(services, "Id", "Name", model.GovernmentServiceId);
                PopulateFileUploadSettings();
                return View(model);
            }
        }

        var updated = await _serviceRequestService.UpdateAsync(model);

        if (documents != null && documents.Count > 0)
        {
            await _serviceRequestService.UploadDocumentsAsync(updated.Id, documents);
        }

        await _auditLogService.LogAsync(
            userId,
            "ServiceRequestUpdated",
            "ServiceRequest",
            updated.Id.ToString(),
            null,
            $"Service request #{updated.Id} updated for service ID {model.GovernmentServiceId}"
        );

        if (saveAsDraft)
            TempData["Success"] = "Service request saved as draft.";
        else
            TempData["Success"] = "Service request updated successfully.";

        return RedirectToAction("Details", new { id = updated.Id });
    }

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Citizen"))
        {
            var userId = _userManager.GetUserId(User);
            var citizen = await _citizenService.GetByUserIdAsync(userId!);
            if (citizen == null) return RedirectToAction("Create", "Citizen");

            var requests = await _serviceRequestService.GetByCitizenIdAsync(citizen.Id);
            return View(requests);
        }

        var allRequests = await _serviceRequestService.GetAllAsync();
        return View(allRequests);
    }

    public async Task<IActionResult> Details(int id)
    {
        var request = await _serviceRequestService.GetByIdAsync(id);
        if (request == null) return NotFound();

        // If current user is an officer, ensure they are assigned to this service
        if (User.IsInRole("Officer"))
        {
            var officerId = _userManager.GetUserId(User)!;
            var assignments = await _unitOfWork.OfficerServiceAssignments.GetByOfficerIdAsync(officerId);
            var serviceIds = assignments.Select(a => a.GovernmentServiceId).ToList();
            if (!serviceIds.Contains(request.GovernmentServiceId) && !User.IsInRole("Admin"))
            {
                // Show access denied page instead of hiding the resource
                return RedirectToAction("AccessDenied", "Account");
            }
        }

        var documents = await _documentService.GetByServiceRequestIdAsync(id);
        ViewBag.Documents = documents;

        var workflowSteps = await _workflowService.GetStepsByRequestIdAsync(id);
        ViewBag.WorkflowSteps = workflowSteps;
        ViewBag.AllowedTransitions = _workflowService.GetAllowedTransitions(request.Status.ToString());

        // Payment info
        var isPaid = await _paymentService.IsServiceRequestPaidAsync(id);
        ViewBag.IsPaid = isPaid;
        var govService = await _unitOfWork.GovernmentServices.GetWithDetailsAsync(request.GovernmentServiceId);
        ViewBag.ServiceFee = govService?.Fee ?? 0m;
        var payments = await _paymentService.GetByServiceRequestIdAsync(id);
        ViewBag.Payments = payments;

        // Compute missing required documents using filename heuristics (case-insensitive token match)
        var missing = new List<string>();
        if (govService?.RequiredDocuments != null && govService.RequiredDocuments.Any())
        {
            foreach (var req in govService.RequiredDocuments)
            {
                var reqName = req.DocumentName?.ToLowerInvariant() ?? string.Empty;
                // Normalize to tokens
                var tokens = System.Text.RegularExpressions.Regex.Replace(reqName, "\\W+", " ")
                    .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => t.Length > 1)
                    .ToArray();

                bool has = false;

                // First try explicit DocumentType match if present
                has = documents.Any(d => !string.IsNullOrEmpty(d.DocumentType) && string.Equals(d.DocumentType, req.DocumentName, StringComparison.OrdinalIgnoreCase));

                if (!has && tokens.Length > 0)
                {
                    // Heuristic: check if any token appears in file name
                    has = documents.Any(d =>
                    {
                        var fname = (d.FileName ?? string.Empty).ToLowerInvariant();
                        return tokens.Any(t => fname.Contains(t));
                    });
                }

                if (!has)
                {
                    missing.Add(req.DocumentName);
                }
            }
        }
        ViewBag.MissingRequiredDocuments = missing;

        // Nearest department location
        if (govService?.DepartmentId != null)
        {
            var userId = _userManager.GetUserId(User);
            var citizen = await _citizenService.GetByUserIdAsync(userId!);
            if (citizen != null)
            {
                var nearestLocation = await _locationService.GetNearestLocationAsync(govService.DepartmentId.Value, citizen.Address);
                ViewBag.NearestLocation = nearestLocation;
            }
        }

        PopulateFileUploadSettings();

        return View(request);
    }

    [Authorize(Roles = "Officer,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestMoreInfo(int id, string? message)
    {
        try
        {
            var request = await _serviceRequestService.GetByIdAsync(id);
            if (request == null) return NotFound();

            // If officer, ensure they are assigned to this service
            if (User.IsInRole("Officer"))
            {
                var officerId = _userManager.GetUserId(User)!;
                var assignments = await _unitOfWork.OfficerServiceAssignments.GetByOfficerIdAsync(officerId);
                var serviceIds = assignments.Select(a => a.GovernmentServiceId).ToList();
                if (!serviceIds.Contains(request.GovernmentServiceId) && !User.IsInRole("Admin"))
                    return NotFound();
            }

            // Update status to PendingInformation to indicate officer requested more info
            await _serviceRequestService.UpdateStatusAsync(id, ServiceRequestStatus.PendingInformation.ToString(), message);

            // Notify citizen
            var citizen = await _unitOfWork.Citizens.GetByIdAsync(request.CitizenId);
            if (citizen != null)
            {
                var note = string.IsNullOrEmpty(message) ? "An officer has requested additional information for your service request." : message;
                await _notificationService.SendAsync(citizen.UserId, "More Information Required", note);
            }

            await _auditLogService.LogAsync(_userManager.GetUserId(User), "RequestedMoreInfo", "ServiceRequest", id.ToString(), null, message);
            TempData["Success"] = "Requested more information from the citizen.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Details", new { id });
    }

    [Authorize(Roles = "Citizen")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var services = await _unitOfWork.GovernmentServices.GetActiveServicesAsync();
        ViewBag.Services = new SelectList(services, "Id", "Name");
        PopulateFileUploadSettings();
        // Ensure view has a model instance to avoid NullReferenceExceptions in Razor when using @Model
        return View(new ServiceRequestDto());
    }

    [Authorize(Roles = "Citizen")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceRequestDto model, List<IFormFile>? documents, bool saveAsDraft = false)
    {
        var userId = _userManager.GetUserId(User);
        var citizen = await _citizenService.GetByUserIdAsync(userId!);
        if (citizen == null) return RedirectToAction("Create", "Citizen");

        model.CitizenId = citizen.Id;

        if (!ModelState.IsValid)
        {
            var services = await _unitOfWork.GovernmentServices.GetActiveServicesAsync();
            ViewBag.Services = new SelectList(services, "Id", "Name");
            PopulateFileUploadSettings();
            return View(model);
        }

        var (maxSizeBytes, allowedExtensions) = GetFileUploadSettings();

        // If user is submitting (not saving draft) require at least one uploaded file
        if (!saveAsDraft)
        {
            if (documents == null || documents.Count == 0)
            {
                ModelState.AddModelError("", "Please upload at least one supporting document before submitting the request.");
                var services = await _unitOfWork.GovernmentServices.GetActiveServicesAsync();
                ViewBag.Services = new SelectList(services, "Id", "Name");
                PopulateFileUploadSettings();
                return View(model);
            }
        }

        // Validate uploaded file names match required documents for the service
        var (isValidFileNames, fileNameError) = await ValidateUploadedFilesMatchRequirementsAsync(model.GovernmentServiceId, documents);
        if (!isValidFileNames)
        {
            ModelState.AddModelError("", fileNameError ?? "File names do not match required documents.");
            var services = await _unitOfWork.GovernmentServices.GetActiveServicesAsync();
            ViewBag.Services = new SelectList(services, "Id", "Name");
            PopulateFileUploadSettings();
            return View(model);
        }

        if (documents != null && documents.Count > 0)
        {
            var allowedExtArray = allowedExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim().ToLowerInvariant()).ToArray();
            foreach (var file in documents)
            {
                var (isValid, error) = _documentService.ValidateFile(file, maxSizeBytes, allowedExtArray);
                if (!isValid)
                {
                    ModelState.AddModelError("", error!);
                    var services = await _unitOfWork.GovernmentServices.GetActiveServicesAsync();
                    ViewBag.Services = new SelectList(services, "Id", "Name");
                    PopulateFileUploadSettings();
                    return View(model);
                }
            }
        }

        _logger?.LogInformation("Create POST called. saveAsDraft={saveAsDraft}, CitizenId={CitizenId}, ServiceId={ServiceId}", saveAsDraft, model.CitizenId, model.GovernmentServiceId);

        // If user chose to save as draft, use the draft path
        if (saveAsDraft)
        {
            model.Status = Domain.Enums.ServiceRequestStatus.Draft;
            var createdDraft = await _serviceRequestService.SaveDraftAsync(model);

            if (documents != null && documents.Count > 0)
            {
                await _serviceRequestService.UploadDocumentsAsync(createdDraft.Id, documents);
            }

            await _auditLogService.LogAsync(
                userId,
                "ServiceRequestDraftSaved",
                "ServiceRequest",
                createdDraft.Id.ToString(),
                null,
                $"Service request draft #{createdDraft.Id} saved for service ID {model.GovernmentServiceId}"
            );

            TempData["Success"] = "Service request saved as draft.";
            return RedirectToAction("Details", new { id = createdDraft.Id });
        }

        // Otherwise create/submission path
        model.Status = ServiceRequestStatus.Submitted;
        ServiceRequestDto created;
        try
        {
            created = await _serviceRequestService.CreateAsync(model);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var services = await _unitOfWork.GovernmentServices.GetActiveServicesAsync();
            ViewBag.Services = new SelectList(services, "Id", "Name");
            PopulateFileUploadSettings();
            return View(model);
        }

        if (documents != null && documents.Count > 0)
        {
            await _serviceRequestService.UploadDocumentsAsync(created.Id, documents);
        }

        await _auditLogService.LogAsync(
            userId,
            "ServiceRequestCreated",
            "ServiceRequest",
            created.Id.ToString(),
            null,
            $"Service request #{created.Id} created for service ID {model.GovernmentServiceId}"
        );

        TempData["Success"] = "Service request submitted successfully.";
        return RedirectToAction("Details", new { id = created.Id });
    }

    [Authorize(Roles = "Citizen")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraftAjax(ServiceRequestDto model, List<IFormFile>? documents)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var citizen = await _citizenService.GetByUserIdAsync(userId!);
            if (citizen == null) return Json(new { success = false, error = "Citizen not found." });

            model.CitizenId = citizen.Id;

            // Validate uploaded file names match required documents for the service
            var (isValidFileNames, fileNameError) = await ValidateUploadedFilesMatchRequirementsAsync(model.GovernmentServiceId, documents);
            if (!isValidFileNames)
            {
                return Json(new { success = false, error = fileNameError ?? "File names do not match required documents." });
            }

            ServiceRequestDto saved;
            if (model.Id > 0)
            {
                // Update existing draft
                saved = await _serviceRequestService.UpdateAsync(model);
            }
            else
            {
                // Create new draft
                saved = await _serviceRequestService.SaveDraftAsync(model);
            }

            if (documents != null && documents.Count > 0)
            {
                await _serviceRequestService.UploadDocumentsAsync(saved.Id, documents);
            }

            return Json(new { success = true, id = saved.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [Authorize(Roles = "Officer,Admin")]
    [HttpGet]
    public async Task<IActionResult> Pending(int? serviceId = null)
    {
        IEnumerable<ServiceRequestDto> requests;

        if (User.IsInRole("Admin"))
        {
            requests = await _serviceRequestService.GetPendingRequestsAsync();
        }
        else
        {
            var officerId = _userManager.GetUserId(User)!;
            requests = await _serviceRequestService.GetPendingRequestsByOfficerAsync(officerId);
        }

        if (serviceId.HasValue)
        {
            requests = requests.Where(r => r.GovernmentServiceId == serviceId.Value);
        }

        return View(requests);
    }

    [Authorize(Roles = "Officer,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, string? comments, IFormFile? completionFile)
    {
        try
        {
            // If officer, ensure assignment to the service
            if (User.IsInRole("Officer"))
            {
                var officerId = _userManager.GetUserId(User)!;
                var assignments = await _unitOfWork.OfficerServiceAssignments.GetByOfficerIdAsync(officerId);
                var serviceIds = assignments.Select(a => a.GovernmentServiceId).ToList();
                var request = await _serviceRequestService.GetByIdAsync(id);
                if (request == null) return NotFound();
                if (!serviceIds.Contains(request.GovernmentServiceId) && !User.IsInRole("Admin"))
                    return NotFound();
            }

            string? completionFileName = null;
            string? completionFilePath = null;

            if (status == "Completed" && completionFile != null && completionFile.Length > 0)
            {
                var (maxSizeBytes, allowedExtensions) = GetFileUploadSettings();
                var allowedExtArray = allowedExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim().ToLowerInvariant()).ToArray();
                var (isValid, error) = _documentService.ValidateFile(completionFile, maxSizeBytes, allowedExtArray);
                if (!isValid)
                {
                    TempData["Error"] = error;
                    return RedirectToAction("Details", new { id });
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "completions");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}_{completionFile.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await completionFile.CopyToAsync(stream);
                }

                completionFileName = completionFile.FileName;
                completionFilePath = $"/uploads/completions/{uniqueFileName}";
            }

            await _serviceRequestService.UpdateStatusAsync(id, status, comments, completionFileName, completionFilePath);

            var userId = _userManager.GetUserId(User);
            await _auditLogService.LogAsync(
                userId,
                "ServiceRequestStatusUpdated",
                "ServiceRequest",
                id.ToString(),
                null,
                $"Service request #{id} status updated to {status}"
            );

            TempData["Success"] = $"Status updated to {status} successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (KeyNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(int id, int serviceRequestId)
    {
        try
        {
            var document = await _documentService.GetByIdAsync(id);
            if (document == null) return NotFound();

            var request = await _serviceRequestService.GetByIdAsync(serviceRequestId);
            if (request == null) return NotFound();

            // Authorization: owner citizen or admin may delete; officers cannot
            if (User.IsInRole("Citizen"))
            {
                var userId = _userManager.GetUserId(User);
                var citizen = await _citizenService.GetByUserIdAsync(userId!);
                if (citizen == null) return Forbid();
                if (request.CitizenId != citizen.Id && !User.IsInRole("Admin")) return Forbid();

                // Allow delete only in editable statuses
                if (request.Status == Domain.Enums.ServiceRequestStatus.Completed || request.Status == Domain.Enums.ServiceRequestStatus.Cancelled)
                    return BadRequest("Cannot delete documents for this request in its current status.");
            }
            else if (!User.IsInRole("Admin"))
            {
                // Non-citizen non-admins (e.g., officers) cannot delete documents
                return Forbid();
            }

            await _documentService.DeleteAsync(id);

            await _auditLogService.LogAsync(_userManager.GetUserId(User), "DocumentDeleted", "Document", id.ToString(), null, $"Document {document.FileName} removed from request {serviceRequestId}");
            TempData["Success"] = "Document deleted.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Details", new { id = serviceRequestId });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadDocument(int id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();

        var request = await _serviceRequestService.GetByIdAsync(doc.ServiceRequestId);
        if (request == null) return NotFound();

        // Authorization: citizen owner or assigned officer or admin
        if (User.IsInRole("Citizen"))
        {
            var userId = _userManager.GetUserId(User);
            var citizen = await _citizenService.GetByUserIdAsync(userId!);
            if (citizen == null) return Forbid();
            if (request.CitizenId != citizen.Id && !User.IsInRole("Admin")) return Forbid();
        }
        else if (User.IsInRole("Officer"))
        {
            var officerId = _userManager.GetUserId(User)!;
            var assignments = await _unitOfWork.OfficerServiceAssignments.GetByOfficerIdAsync(officerId);
            var serviceIds = assignments.Select(a => a.GovernmentServiceId).ToList();
            if (!serviceIds.Contains(request.GovernmentServiceId) && !User.IsInRole("Admin"))
                return NotFound();
        }

        var physicalPath = Path.Combine(_environment.WebRootPath, doc.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(physicalPath)) return NotFound();

        return PhysicalFile(physicalPath, doc.ContentType ?? "application/octet-stream", doc.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(int serviceRequestId, List<IFormFile> files)
    {
        try
        {
            var request = await _serviceRequestService.GetByIdAsync(serviceRequestId);
            if (request == null) return NotFound();

            // Only allow uploads when citizen owner or admin
            if (User.IsInRole("Citizen"))
            {
                var userId = _userManager.GetUserId(User);
                var citizen = await _citizenService.GetByUserIdAsync(userId!);
                if (citizen == null) return Forbid();
                if (request.CitizenId != citizen.Id && !User.IsInRole("Admin")) return Forbid();

                if (request.Status == Domain.Enums.ServiceRequestStatus.Completed || request.Status == Domain.Enums.ServiceRequestStatus.Cancelled)
                    return BadRequest("Cannot upload documents for this request in its current status.");
            }
            else if (!User.IsInRole("Admin"))
            {
                // Officers cannot upload documents on behalf of citizen
                return Forbid();
            }

            // Reuse service to upload
            if (files != null && files.Count > 0)
            {
                await _serviceRequestService.UploadDocumentsAsync(serviceRequestId, files);
            }

            TempData["Success"] = "Documents uploaded successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Details", new { id = serviceRequestId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceDocument(int id, int serviceRequestId, List<IFormFile> files)
    {
        try
        {
            var documentDto = await _documentService.GetByIdAsync(id);
            if (documentDto == null) return NotFound();

            var request = await _serviceRequestService.GetByIdAsync(serviceRequestId);
            if (request == null) return NotFound();

            // Only owner or admin may replace
            if (User.IsInRole("Citizen"))
            {
                var userId = _userManager.GetUserId(User);
                var citizen = await _citizenService.GetByUserIdAsync(userId!);
                if (citizen == null) return Forbid();
                if (request.CitizenId != citizen.Id && !User.IsInRole("Admin")) return Forbid();

                if (request.Status == Domain.Enums.ServiceRequestStatus.Completed || request.Status == Domain.Enums.ServiceRequestStatus.Cancelled)
                    return BadRequest("Cannot replace documents for this request in its current status.");
            }
            else if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (files == null || files.Count == 0)
            {
                TempData["Error"] = "No file selected.";
                return RedirectToAction("Details", new { id = serviceRequestId });
            }

            var (maxSizeBytes, allowedExtensions) = GetFileUploadSettings();
            var allowedExtArray = allowedExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim().ToLowerInvariant()).ToArray();
            var firstFile = files.First();
            var (isValid, error) = _documentService.ValidateFile(firstFile, maxSizeBytes, allowedExtArray);
            if (!isValid)
            {
                TempData["Error"] = error;
                return RedirectToAction("Details", new { id = serviceRequestId });
            }

            // Update existing document entity with new file (preserve id)
            var docEntity = await _unitOfWork.Documents.GetByIdAsync(id);
            if (docEntity == null) return NotFound();

            var oldPhysicalPath = Path.Combine(_environment.WebRootPath, docEntity.FilePath.TrimStart('/'));

            // Save new file
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);
            var sanitizedFileName = Path.GetFileName(firstFile.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
            var newPhysicalPath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = new FileStream(newPhysicalPath, FileMode.Create))
            {
                await firstFile.CopyToAsync(stream);
            }

            // update entity
            docEntity.FileName = sanitizedFileName;
            docEntity.FilePath = $"/uploads/{uniqueFileName}";
            docEntity.ContentType = firstFile.ContentType;
            docEntity.FileSize = firstFile.Length;
            docEntity.UploadedAt = DateTime.UtcNow;

            _unitOfWork.Documents.Update(docEntity);
            await _unitOfWork.SaveChangesAsync();

            // delete old physical file if exists
            try
            {
                if (System.IO.File.Exists(oldPhysicalPath)) System.IO.File.Delete(oldPhysicalPath);
            }
            catch { }

            // If there are additional files selected, treat them as new uploads
            if (files.Count > 1)
            {
                var extra = files.Skip(1).ToList();
                await _serviceRequestService.UploadDocumentsAsync(serviceRequestId, extra);
            }

            await _auditLogService.LogAsync(_userManager.GetUserId(User), "DocumentReplaced", "Document", id.ToString(), null, $"Replaced document {documentDto.FileName} on request {serviceRequestId}");
            TempData["Success"] = "Document replaced successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Details", new { id = serviceRequestId });
    }

    private (long maxSizeBytes, string allowedExtensions) GetFileUploadSettings()
    {
        var maxSizeMB = _configuration.GetValue<int?>("FileUpload:MaxFileSizeMB") ?? 10;
        var allowed = _configuration.GetValue<string>("FileUpload:AllowedExtensions") ?? ".pdf,.jpg,.jpeg,.png,.doc,.docx";
        return (maxSizeMB * 1024L * 1024L, allowed);
    }

    private void PopulateFileUploadSettings()
    {
        var maxSizeMB = _configuration.GetValue<int?>("FileUpload:MaxFileSizeMB") ?? 10;
        var allowed = _configuration.GetValue<string>("FileUpload:AllowedExtensions") ?? ".pdf,.jpg,.jpeg,.png,.doc,.docx";
        ViewBag.MaxFileSizeMB = maxSizeMB;
        ViewBag.AllowedExtensions = allowed;
        // Build accept attribute value
        var accept = string.Join(",",
            allowed.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(ext => ext.Trim()));
        ViewBag.AllowedExtensionsAccept = accept;
    }

    private async Task<(bool IsValid, string? Error)> ValidateUploadedFilesMatchRequirementsAsync(int governmentServiceId, IEnumerable<IFormFile>? files)
    {
        // If no files uploaded, validation passes (files are optional at upload time)
        if (files == null || !files.Any()) return (true, null);

        // Get required documents for this service
        var govService = await _unitOfWork.GovernmentServices.GetWithDetailsAsync(governmentServiceId);
        if (govService?.RequiredDocuments == null || !govService.RequiredDocuments.Any())
        {
            // No required documents, all files are acceptable
            return (true, null);
        }

        // Build list of required document name tokens (lowercase, normalized)
        var requiredTokens = new List<HashSet<string>>();
        foreach (var req in govService.RequiredDocuments)
        {
            var reqName = req.DocumentName?.ToLowerInvariant() ?? string.Empty;
            var tokens = System.Text.RegularExpressions.Regex.Replace(reqName, "\\W+", " ")
                .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 1)
                .ToHashSet();
            requiredTokens.Add(tokens);
        }

        // Validate each uploaded file name matches at least one required document
        var unmatchedFiles = new List<string>();
        foreach (var file in files)
        {
            var fileName = file.FileName?.ToLowerInvariant() ?? string.Empty;
            var fileTokens = System.Text.RegularExpressions.Regex.Replace(fileName, "\\W+", " ")
                .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 1)
                .ToHashSet();

            // Check if this file matches at least one required document (at least one token match)
            bool matches = requiredTokens.Any(reqTokens => fileTokens.Intersect(reqTokens).Any());

            if (!matches)
            {
                unmatchedFiles.Add(file.FileName ?? "unknown");
            }
        }

        if (unmatchedFiles.Any())
        {
            var error = $"The following files do not match required documents for this service: {string.Join(", ", unmatchedFiles)}. " +
                       $"Required documents are: {string.Join(", ", govService.RequiredDocuments.Select(d => d.DocumentName))}";
            return (false, error);
        }

        return (true, null);
    }
}
