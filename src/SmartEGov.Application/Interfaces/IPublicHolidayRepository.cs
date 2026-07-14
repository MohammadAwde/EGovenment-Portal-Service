using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IPublicHolidayRepository
{
    Task<IEnumerable<PublicHoliday>> GetAllAsync();
    Task<PublicHoliday?> GetByIdAsync(int id);
    Task AddAsync(PublicHoliday holiday);
    void Update(PublicHoliday holiday);
    void Remove(PublicHoliday holiday);
    Task<bool> IsHolidayAsync(DateTime date);
}
