using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebHostEnvironment _environment;
    private readonly IMapper _mapper;
    private readonly IFileValidationService _fileValidationService;

    public DocumentService(
        IUnitOfWork unitOfWork, 
        IWebHostEnvironment environment, 
        IMapper mapper,
        IFileValidationService fileValidationService)
    {
        _unitOfWork = unitOfWork;
        _environment = environment;
        _mapper = mapper;
        _fileValidationService = fileValidationService;
    }

    public async Task<DocumentDto?> GetByIdAsync(int id)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id);
        return document is null ? null : _mapper.Map<DocumentDto>(document);
    }

    public async Task<IEnumerable<DocumentDto>> GetByServiceRequestIdAsync(int serviceRequestId)
    {
        var documents = await _unitOfWork.Documents.GetByServiceRequestIdAsync(serviceRequestId);
        return _mapper.Map<IEnumerable<DocumentDto>>(documents);
    }

    public async Task<DocumentDto> UploadAsync(int serviceRequestId, IFormFile file, string documentType = "")
    {
        var validationResult = await _fileValidationService.ValidateFileAsync(file);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(validationResult.ErrorMessage);
        }

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var sanitizedFileName = Path.GetFileName(file.FileName);
        var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var document = new Document
        {
            FileName = sanitizedFileName,
            FilePath = $"/uploads/{uniqueFileName}",
            ContentType = file.ContentType,
            FileSize = file.Length,
            DocumentType = documentType,
            ServiceRequestId = serviceRequestId
        };

        await _unitOfWork.Documents.AddAsync(document);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<DocumentDto>(document);
    }

    public async Task<IEnumerable<DocumentDto>> UploadMultipleAsync(int serviceRequestId, IEnumerable<IFormFile> files)
    {
        var results = new List<DocumentDto>();
        foreach (var file in files)
        {
            var dto = await UploadAsync(serviceRequestId, file);
            results.Add(dto);
        }
        return results;
    }

    public async Task DeleteAsync(int id)
    {
        var document = await _unitOfWork.Documents.GetByIdAsync(id);
        if (document == null) throw new KeyNotFoundException("Document not found.");

        var fullPath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        _unitOfWork.Documents.Remove(document);
        await _unitOfWork.SaveChangesAsync();
    }

    public (bool IsValid, string? Error) ValidateFile(IFormFile file, long maxSizeBytes, string[] allowedExtensions)
    {
        if (file.Length == 0)
            return (false, $"File '{file.FileName}' is empty.");

        if (file.Length > maxSizeBytes)
            return (false, $"File '{file.FileName}' exceeds the maximum allowed size of {maxSizeBytes / (1024 * 1024)} MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (allowedExtensions.Length > 0 && !allowedExtensions.Contains(extension))
            return (false, $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");

        return (true, null);
    }
}
