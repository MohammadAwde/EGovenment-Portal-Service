using SmartEGov.Application.DTOs;
using SmartEGov.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace SmartEGov.Application.Services;

public interface IServiceRequestService
{
    Task<ServiceRequestDto?> GetByIdAsync(int id);
    Task<IEnumerable<ServiceRequestDto>> GetAllAsync();
    Task<IEnumerable<ServiceRequestDto>> GetByCitizenIdAsync(int citizenId);
    Task<IEnumerable<ServiceRequestDto>> GetPendingRequestsAsync();
    Task<IEnumerable<ServiceRequestDto>> GetPendingRequestsByOfficerAsync(string officerId);
    Task<ServiceRequestDto> CreateAsync(ServiceRequestDto dto);
    Task<ServiceRequestDto> SaveDraftAsync(ServiceRequestDto dto);
    Task<ServiceRequestDto> UpdateAsync(ServiceRequestDto dto);
    Task UpdateStatusAsync(int id, string status, string? comments, string? completionFileName = null, string? completionFilePath = null);
    Task CancelAsync(int id, string userId);
    Task DeleteAsync(int id);
    Task<IEnumerable<DocumentDto>> UploadDocumentsAsync(int serviceRequestId, IEnumerable<Microsoft.AspNetCore.Http.IFormFile> files);
}
