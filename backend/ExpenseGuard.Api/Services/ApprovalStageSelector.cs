using ExpenseGuard.Api.Models;

namespace ExpenseGuard.Api.Services;

public static class ApprovalStageSelector
{
    public static List<ApprovalStageDefinition> ForAmount(IEnumerable<ApprovalStageDefinition> stages, decimal amount)
    {
        var list = stages.ToList();
        var selected = list
            .Where(s => !s.RequiredRole.Equals(RoleNames.Finance, StringComparison.OrdinalIgnoreCase)
                && (!s.MinimumAmount.HasValue || amount >= s.MinimumAmount)
                && (!s.MaximumAmount.HasValue || amount <= s.MaximumAmount))
            .OrderBy(s => s.Sequence)
            .ToList();
        var finance = list
            .Where(s => s.RequiredRole.Equals(RoleNames.Finance, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Sequence)
            .LastOrDefault();
        selected.Add(finance ?? new ApprovalStageDefinition
        {
            Sequence = (selected.LastOrDefault()?.Sequence ?? 0) + 1,
            RequiredRole = RoleNames.Finance
        });
        return selected;
    }
}
