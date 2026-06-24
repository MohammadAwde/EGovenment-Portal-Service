using SmartEGov.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace SmartEGov.Application.Services;

public interface IDocumentService
{
    Task<DocumentDto?> GetByIdAsync(int id);
    Task<IEnumerable<DocumentDto>> GetByServiceRequestIdAsync(int serviceRequestId);
    Task<DocumentDto> UploadAsync(int serviceRequestId, IFormFile file, string documentType = "");
    Task<IEnumerable<DocumentDto>> UploadMultipleAsync(int serviceRequestId, IEnumerable<IFormFile> files);
    Task DeleteAsync(int id);
    (bool IsValid, string? Error) ValidateFile(IFormFile file, long maxSizeBytes, string[] allowedExtensions);
}

public class DocumentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public int ServiceRequestId { get; set; }
}
