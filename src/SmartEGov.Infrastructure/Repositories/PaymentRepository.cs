using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.Interfaces;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Data;
using SmartEGov.Domain.Enums;

namespace SmartEGov.Infrastructure.Repositories;

public class PaymentRepository : Repository<Payment>, IPaymentRepository
{
    public PaymentRepository(ApplicationDbContext context) : base(context)
    {
    }


    public async Task<IEnumerable<Payment>> GetByServiceRequestIdAsync(int serviceRequestId)
    {
        return await _dbSet
            .Include(p => p.ServiceRequest).ThenInclude(sr => sr.GovernmentService)
            .Where(p => p.ServiceRequestId == serviceRequestId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Payment>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .Include(p => p.ServiceRequest).ThenInclude(sr => sr.GovernmentService)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Payment?> GetByTransactionNumberAsync(string transactionNumber)
    {
        return await _dbSet
            .Include(p => p.ServiceRequest).ThenInclude(sr => sr.GovernmentService)
            .FirstOrDefaultAsync(p => p.TransactionNumber == transactionNumber);
    }

    public async Task<Payment?> GetByStripeSessionIdAsync(string sessionId)
    {
        return await _dbSet
            .Include(p => p.ServiceRequest).ThenInclude(sr => sr.GovernmentService)
            .FirstOrDefaultAsync(p => p.StripeSessionId == sessionId);
    }

    public async Task<Payment?> GetByPaymentIntentIdAsync(string paymentIntentId)
    {
        return await _dbSet
            .Include(p => p.ServiceRequest).ThenInclude(sr => sr.GovernmentService)
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);
    }

    public async Task<IEnumerable<Payment>> GetCompletedPaymentsAsync()
    {
        return await _dbSet
            .Include(p => p.ServiceRequest).ThenInclude(sr => sr.GovernmentService)
            .Include(p => p.User)
            .Where(p => p.Status == PaymentStatus.Completed)
            .OrderByDescending(p => p.CompletedAt)
            .ToListAsync();
    }
}
