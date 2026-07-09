using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Repositories;

public class PublicHolidayRepository : IPublicHolidayRepository
{
    private readonly ApplicationDbContext _ctx;
    public PublicHolidayRepository(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<PublicHoliday>> GetAllAsync()
        => await _ctx.PublicHolidays.OrderBy(h => h.Month).ThenBy(h => h.Day).ToListAsync();

    public async Task<PublicHoliday?> GetByIdAsync(int id)
        => await _ctx.PublicHolidays.FindAsync(id);

    public async Task AddAsync(PublicHoliday holiday)
        => await _ctx.PublicHolidays.AddAsync(holiday);

    public void Update(PublicHoliday holiday)
        => _ctx.PublicHolidays.Update(holiday);

    public void Remove(PublicHoliday holiday)
        => _ctx.PublicHolidays.Remove(holiday);

    public async Task<bool> IsHolidayAsync(DateTime date)
        => await _ctx.PublicHolidays.AnyAsync(h =>
            h.IsActive &&
            h.Day == date.Day &&
            h.Month == date.Month &&
            (h.Year == null || h.Year == date.Year));
}
