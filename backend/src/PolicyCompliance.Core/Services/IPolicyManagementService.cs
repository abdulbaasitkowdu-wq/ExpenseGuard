using PolicyCompliance.Core.DTOs;

namespace PolicyCompliance.Core.Services;

public interface IPolicyManagementService
{
    Task<List<ExpensePolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken = default);
    Task<ExpensePolicyDto?> GetPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ExpensePolicyDto> CreatePolicyAsync(CreateExpensePolicyDto dto, CancellationToken cancellationToken = default);
    Task<ExpensePolicyDto?> UpdatePolicyAsync(Guid id, CreateExpensePolicyDto dto, CancellationToken cancellationToken = default);
}
