using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SmartEGov.Infrastructure.Services;

public interface IFileValidationService
{
    Task<(bool IsValid, string ErrorMessage)> ValidateFileAsync(IFormFile file);
    Task<(bool IsValid, string ErrorMessage)> ValidateFilesAsync(IEnumerable<IFormFile> files);
}

public class FileValidationService : IFileValidationService
{
    private readonly IConfiguration _configuration;
    private readonly long _maxFileSizeInBytes;
    private readonly HashSet<string> _allowedExtensions;
    private readonly HashSet<string> _allowedMimeTypes;

    public FileValidationService(IConfiguration configuration)
    {
        _configuration = configuration;

        var maxFileSizeMB = _configuration.GetValue<int>("FileUpload:MaxFileSizeInMB", 10);
        _maxFileSizeInBytes = maxFileSizeMB * 1024 * 1024;

        var extensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>() 
            ?? new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".xlsx", ".xls" };
        _allowedExtensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);

        _allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "image/jpeg",
            "image/png",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    public async Task<(bool IsValid, string ErrorMessage)> ValidateFileAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return (false, "File is empty or not provided.");
        }

        if (file.Length > _maxFileSizeInBytes)
        {
            return (false, $"File size exceeds maximum allowed size of {_maxFileSizeInBytes / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
        {
            return (false, $"File extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", _allowedExtensions)}");
        }

        if (!_allowedMimeTypes.Contains(file.ContentType))
        {
            return (false, $"File MIME type '{file.ContentType}' is not allowed.");
        }

        var isValidContent = await ValidateFileContentAsync(file);
        if (!isValidContent)
        {
            return (false, "File content validation failed. File may be corrupted or malicious.");
        }

        var fileName = Path.GetFileName(file.FileName);
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return (false, "File name contains invalid characters.");
        }

        return (true, string.Empty);
    }

    public async Task<(bool IsValid, string ErrorMessage)> ValidateFilesAsync(IEnumerable<IFormFile> files)
    {
        foreach (var file in files)
        {
            var result = await ValidateFileAsync(file);
            if (!result.IsValid)
            {
                return result;
            }
        }
        return (true, string.Empty);
    }

    private async Task<bool> ValidateFileContentAsync(IFormFile file)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var buffer = new byte[Math.Min(memoryStream.Length, 512)];
            await memoryStream.ReadAsync(buffer, 0, buffer.Length);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => buffer.Length >= 5 && buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46,
                ".jpg" or ".jpeg" => buffer.Length >= 3 && buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF,
                ".png" => buffer.Length >= 8 && buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47,
                ".doc" => buffer.Length >= 8 && buffer[0] == 0xD0 && buffer[1] == 0xCF && buffer[2] == 0x11 && buffer[3] == 0xE0,
                ".docx" or ".xlsx" => buffer.Length >= 4 && buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04,
                ".xls" => buffer.Length >= 8 && buffer[0] == 0xD0 && buffer[1] == 0xCF && buffer[2] == 0x11 && buffer[3] == 0xE0,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}
