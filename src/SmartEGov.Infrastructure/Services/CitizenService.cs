using SmartEGov.Application.DTOs;
using AutoMapper;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Infrastructure.Services;

public class CitizenService : ICitizenService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CitizenService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<CitizenDto?> GetByIdAsync(int id)
    {
        var citizen = await _unitOfWork.Citizens.GetByIdAsync(id);
        return citizen == null ? null : _mapper.Map<CitizenDto>(citizen);
    }

    public async Task<CitizenDto?> GetByUserIdAsync(string userId)
    {
        var citizen = await _unitOfWork.Citizens.GetByUserIdAsync(userId);
        return citizen == null ? null : _mapper.Map<CitizenDto>(citizen);
    }

    public async Task<IEnumerable<CitizenDto>> GetAllAsync()
    {
        var citizens = await _unitOfWork.Citizens.GetAllAsync();
        return _mapper.Map<IEnumerable<CitizenDto>>(citizens);
    }

    public async Task<CitizenDto> CreateAsync(CitizenDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.NationalId))
        {
            var existing = await _unitOfWork.Citizens.GetByNationalIdAsync(dto.NationalId);
            if (existing != null)
                throw new InvalidOperationException("This National ID is already registered to another profile.");
        }

        var citizen = _mapper.Map<Citizen>(dto);
        citizen.UserId = dto.UserId!;

        await _unitOfWork.Citizens.AddAsync(citizen);
        await _unitOfWork.SaveChangesAsync();

        dto.Id = citizen.Id;
        return dto;
    }

    public async Task UpdateAsync(CitizenDto dto)
    {
        var citizen = await _unitOfWork.Citizens.GetByIdAsync(dto.Id);
        if (citizen == null) throw new KeyNotFoundException("Citizen not found.");

        citizen.NationalId = dto.NationalId;
        citizen.FirstName = dto.FirstName;
        citizen.LastName = dto.LastName;
        citizen.DateOfBirth = dto.DateOfBirth;
        citizen.Address = dto.Address;
        citizen.PhoneNumber = dto.PhoneNumber;

        _unitOfWork.Citizens.Update(citizen);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var citizen = await _unitOfWork.Citizens.GetByIdAsync(id);
        if (citizen == null) throw new KeyNotFoundException("Citizen not found.");

        _unitOfWork.Citizens.Remove(citizen);
        await _unitOfWork.SaveChangesAsync();
    }
}
