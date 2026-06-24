using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Interfaces;

public interface IApprovalWorkflowRepository : IRepository<ApprovalWorkflow>
{
    Task<IEnumerable<ApprovalWorkflow>> GetActiveWorkflowsAsync();
}
