using SmartEGov.Application.DTOs;

namespace SmartEGov.Application.Services;

public interface ICitizenService
{
    Task<CitizenDto?> GetByIdAsync(int id);
    Task<CitizenDto?> GetByUserIdAsync(string userId);
    Task<IEnumerable<CitizenDto>> GetAllAsync();
    Task<CitizenDto> CreateAsync(CitizenDto dto);
    Task UpdateAsync(CitizenDto dto);
    Task DeleteAsync(int id);
}
