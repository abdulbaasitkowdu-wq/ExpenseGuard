using PolicyCompliance.Core.Entities;
using PolicyCompliance.Core.Enums;

namespace PolicyCompliance.Core.Validators;

public interface IPolicyValidationEngine
{
    PolicyValidationResult ValidateClaim(ExpenseClaim claim, IEnumerable<ExpensePolicy> activePolicies, decimal departmentMonthlySpentSoFar, decimal departmentBudgetLimit);
}

public class PolicyValidationResult
{
    public bool IsCompliant => Violations.Count == 0;
    public List<PolicyViolation> Violations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool RequiresManagerApproval { get; set; }
}

public class PolicyValidationEngine : IPolicyValidationEngine
{
    public PolicyValidationResult ValidateClaim(
        ExpenseClaim claim,
        IEnumerable<ExpensePolicy> activePolicies,
        decimal departmentMonthlySpentSoFar,
        decimal departmentBudgetLimit)
    {
        var result = new PolicyValidationResult();
        var policiesList = activePolicies.ToList();

        // 1. Find matching policy for claim category (department-specific policy takes precedence over global policy)
        var categoryPolicy = policiesList
            .Where(p => p.Active && p.Category.Equals(claim.Category, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.DepartmentId.HasValue && p.DepartmentId == claim.DepartmentId)
            .FirstOrDefault();

        // Rule 3: Expense category allowed
        if (categoryPolicy == null)
        {
            result.Violations.Add(new PolicyViolation
            {
                ExpenseClaimId = claim.Id,
                RuleCode = "RULE_CATEGORY_DISALLOWED",
                Severity = ViolationSeverity.HIGH,
                Message = $"Expense category '{claim.Category}' is not recognized under any active company policy.",
                ActualValue = claim.Category,
                AllowedValue = string.Join(", ", policiesList.Where(p => p.Active).Select(p => p.Category).Distinct()),
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            // Rule 1: Maximum amount limit
            if (claim.TotalAmount > categoryPolicy.MaximumAmount)
            {
                result.Violations.Add(new PolicyViolation
                {
                    ExpenseClaimId = claim.Id,
                    PolicyId = categoryPolicy.Id,
                    RuleCode = "RULE_MAX_AMOUNT_EXCEEDED",
                    Severity = ViolationSeverity.HIGH,
                    Message = $"Claim amount ({claim.TotalAmount:N2} {claim.Currency}) exceeds the policy maximum limit of {categoryPolicy.MaximumAmount:N2} {categoryPolicy.AllowedCurrency} for '{claim.Category}'.",
                    ActualValue = $"{claim.TotalAmount:N2} {claim.Currency}",
                    AllowedValue = $"{categoryPolicy.MaximumAmount:N2} {categoryPolicy.AllowedCurrency}",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Rule 4: Allowed currency
            if (!string.IsNullOrWhiteSpace(categoryPolicy.AllowedCurrency) &&
                !categoryPolicy.AllowedCurrency.Equals(claim.Currency, StringComparison.OrdinalIgnoreCase))
            {
                result.Violations.Add(new PolicyViolation
                {
                    ExpenseClaimId = claim.Id,
                    PolicyId = categoryPolicy.Id,
                    RuleCode = "RULE_CURRENCY_DISALLOWED",
                    Severity = ViolationSeverity.CRITICAL,
                    Message = $"Claim currency '{claim.Currency}' is not allowed for category '{claim.Category}'. Expected '{categoryPolicy.AllowedCurrency}'.",
                    ActualValue = claim.Currency,
                    AllowedValue = categoryPolicy.AllowedCurrency,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Rule 6: Mandatory manager approval requirement
            if (categoryPolicy.RequiresManagerApproval)
            {
                result.RequiresManagerApproval = true;
            }
        }

        // Rule 2: Receipt verification on claim & items
        bool requiresReceipt = categoryPolicy?.RequiresReceipt ?? true;
        if (requiresReceipt)
        {
            // Check if claim items exist and whether receipts are present
            bool hasMissingReceipt = false;
            if (claim.Items == null || !claim.Items.Any())
            {
                hasMissingReceipt = true;
            }
            else
            {
                hasMissingReceipt = claim.Items.Any(i => string.IsNullOrWhiteSpace(i.ReceiptUrl));
            }

            if (hasMissingReceipt)
            {
                result.Violations.Add(new PolicyViolation
                {
                    ExpenseClaimId = claim.Id,
                    PolicyId = categoryPolicy?.Id,
                    RuleCode = "RULE_MISSING_RECEIPT",
                    Severity = ViolationSeverity.HIGH,
                    Message = "Policy requires a valid receipt / supporting proof of spend for this claim.",
                    ActualValue = "No receipt uploaded or incomplete items",
                    AllowedValue = "Valid receipt URL / document required",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Rule 5: Department cumulative monthly budget limit
        if (departmentBudgetLimit > 0 && (departmentMonthlySpentSoFar + claim.TotalAmount) > departmentBudgetLimit)
        {
            result.Violations.Add(new PolicyViolation
            {
                ExpenseClaimId = claim.Id,
                RuleCode = "RULE_DEPARTMENT_BUDGET_EXCEEDED",
                Severity = ViolationSeverity.WARNING,
                Message = $"Claim pushes cumulative department monthly spend ({departmentMonthlySpentSoFar + claim.TotalAmount:N2}) above allocated monthly budget limit of {departmentBudgetLimit:N2}.",
                ActualValue = $"Total Projected: {departmentMonthlySpentSoFar + claim.TotalAmount:N2}",
                AllowedValue = $"Budget Limit: {departmentBudgetLimit:N2}",
                CreatedAt = DateTime.UtcNow
            });
        }

        return result;
    }
}
