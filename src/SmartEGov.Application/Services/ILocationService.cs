using SmartEGov.Application.DTOs;

namespace SmartEGov.Application.Services;

public interface ILocationService
{
    Task<NearestLocationDto?> GetNearestLocationAsync(int departmentId, string citizenAddress);
}
