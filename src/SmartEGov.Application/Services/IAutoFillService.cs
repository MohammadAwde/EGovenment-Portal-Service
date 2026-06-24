using Microsoft.AspNetCore.Http;
using SmartEGov.Application.DTOs;
namespace SmartEGov.Application.Services;

public interface IAutoFillService
{
    Task<AutoFillResultDto?> GetAutoFillDataAsync(string userId, string documentType = "NationalID");
    Task<IEnumerable<DocumentProfileDto>> GetAllProfilesAsync(string userId);
    Task<DocumentProfileDto> SaveProfileAsync(DocumentProfileDto dto, string userId);
    Task<AutoFillResultDto> ExtractFromOcrAsync(IFormFile idImage, string documentType, string userId);
    Task DeleteProfileAsync(int profileId, string userId);
}
