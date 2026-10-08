using PolicyCompliance.Core.Agents.Contracts;
using PolicyCompliance.Core.Entities;

namespace PolicyCompliance.Core.Tools;

public interface IComplianceAgentTools
{
    Task<List<AgentClaimHistoryItem>> GetEmployeeExpenseHistoryAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<AgentDepartmentCategoryStats> GetDepartmentSpendingStatisticsAsync(Guid departmentId, string category, CancellationToken cancellationToken = default);
    Task<List<AgentDuplicateCandidate>> FindPossibleDuplicateClaimsAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<ExpenseClaim?> GetClaimDetailsAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<RiskAssessment> CreateRiskAssessmentAsync(Guid claimId, AgentRiskAssessmentOutput assessment, CancellationToken cancellationToken = default);
    Task<AuditLog> CreateRiskLogAsync(Guid claimId, string reason, string severity, CancellationToken cancellationToken = default);
}
