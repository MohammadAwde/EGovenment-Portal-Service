using SmartEGov.Application.DTOs;

namespace SmartEGov.Application.Services;

public interface IWorkflowService
{
    Task<IEnumerable<ApprovalStepDto>> GetStepsByRequestIdAsync(int serviceRequestId);
    Task<ApprovalStepDto?> GetCurrentStepAsync(int serviceRequestId);
    Task ApproveStepAsync(int stepId, string officerId, string? comments);
    Task RejectStepAsync(int stepId, string officerId, string? comments);
    Task InitializeWorkflowAsync(int serviceRequestId, int workflowId);
    bool IsValidTransition(string currentStatus, string newStatus);
    IEnumerable<string> GetAllowedTransitions(string currentStatus);
}
