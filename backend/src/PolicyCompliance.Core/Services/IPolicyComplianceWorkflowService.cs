using PolicyCompliance.Core.DTOs;
using PolicyCompliance.Core.Entities;

namespace PolicyCompliance.Core.Services;

public interface IPolicyComplianceWorkflowService
{
    Task<ExpenseClaimDetailDto> SubmitClaimAsync(Guid employeeId, CreateExpenseClaimDto dto, CancellationToken cancellationToken = default);
    Task<ExpenseClaimDetailDto> ResubmitClaimAsync(Guid claimId, Guid employeeId, ResubmitExpenseClaimDto dto, CancellationToken cancellationToken = default);
    Task<ExpenseClaimDetailDto> ApproveClaimAsync(Guid claimId, Guid managerId, string comment, CancellationToken cancellationToken = default);
    Task<ExpenseClaimDetailDto> RejectClaimAsync(Guid claimId, Guid managerId, string reason, CancellationToken cancellationToken = default);
    Task<ExpenseClaimDetailDto> RequestRevisionAsync(Guid claimId, Guid managerId, string comment, CancellationToken cancellationToken = default);
    Task<ExpenseClaimDetailDto> ReimburseClaimAsync(Guid claimId, Guid financeUserId, string notes, CancellationToken cancellationToken = default);

    Task<List<ExpenseClaimListDto>> GetReviewQueueAsync(ReviewQueueFilterDto filter, CancellationToken cancellationToken = default);
    Task<ExpenseClaimDetailDto?> GetClaimDetailsAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<List<ExpenseClaimListDto>> GetEmployeeClaimsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<ComplianceDashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
}
