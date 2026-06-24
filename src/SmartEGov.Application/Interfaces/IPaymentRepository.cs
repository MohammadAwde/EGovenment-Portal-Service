using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<IEnumerable<Payment>> GetByServiceRequestIdAsync(int serviceRequestId);
    Task<IEnumerable<Payment>> GetByUserIdAsync(string userId);
    Task<Payment?> GetByTransactionNumberAsync(string transactionNumber);
    Task<Payment?> GetByStripeSessionIdAsync(string sessionId);
    Task<Payment?> GetByPaymentIntentIdAsync(string paymentIntentId);
    Task<IEnumerable<Payment>> GetCompletedPaymentsAsync();
}
