using Microsoft.EntityFrameworkCore;
using PolicyCompliance.Core.DTOs;
using PolicyCompliance.Core.Entities;
using PolicyCompliance.Core.Services;
using PolicyCompliance.Infrastructure.Data;

namespace PolicyCompliance.Infrastructure.Services;

public class PolicyManagementService : IPolicyManagementService
{
    private readonly AppDbContext _context;

    public PolicyManagementService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ExpensePolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var policies = await _context.ExpensePolicies
            .AsNoTracking()
            .Include(p => p.Department)
            .OrderBy(p => p.Category)
            .ToListAsync(cancellationToken);

        return policies.Select(MapToDto).ToList();
    }

    public async Task<ExpensePolicyDto?> GetPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var policy = await _context.ExpensePolicies
            .AsNoTracking()
            .Include(p => p.Department)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return policy == null ? null : MapToDto(policy);
    }

    public async Task<ExpensePolicyDto> CreatePolicyAsync(CreateExpensePolicyDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new ExpensePolicy
        {
            PolicyName = dto.PolicyName,
            DepartmentId = dto.DepartmentId,
            IsGlobalPolicy = dto.IsGlobalPolicy,
            Category = dto.Category,
            MaximumAmount = dto.MaximumAmount,
            RequiresReceipt = dto.RequiresReceipt,
            RequiresManagerApproval = dto.RequiresManagerApproval,
            AllowedCurrency = dto.AllowedCurrency,
            Active = dto.Active,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            CreatedAt = DateTime.UtcNow
        };

        _context.ExpensePolicies.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return (await GetPolicyByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<ExpensePolicyDto?> UpdatePolicyAsync(Guid id, CreateExpensePolicyDto dto, CancellationToken cancellationToken = default)
    {
        var policy = await _context.ExpensePolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy == null) return null;

        policy.PolicyName = dto.PolicyName;
        policy.DepartmentId = dto.DepartmentId;
        policy.IsGlobalPolicy = dto.IsGlobalPolicy;
        policy.Category = dto.Category;
        policy.MaximumAmount = dto.MaximumAmount;
        policy.RequiresReceipt = dto.RequiresReceipt;
        policy.RequiresManagerApproval = dto.RequiresManagerApproval;
        policy.AllowedCurrency = dto.AllowedCurrency;
        policy.Active = dto.Active;
        policy.EffectiveFrom = dto.EffectiveFrom;
        policy.EffectiveTo = dto.EffectiveTo;

        await _context.SaveChangesAsync(cancellationToken);
        return (await GetPolicyByIdAsync(id, cancellationToken))!;
    }

    private static ExpensePolicyDto MapToDto(ExpensePolicy p) => new()
    {
        Id = p.Id,
        PolicyName = p.PolicyName,
        DepartmentId = p.DepartmentId,
        DepartmentName = p.Department?.Name,
        IsGlobalPolicy = p.IsGlobalPolicy,
        Category = p.Category,
        MaximumAmount = p.MaximumAmount,
        RequiresReceipt = p.RequiresReceipt,
        RequiresManagerApproval = p.RequiresManagerApproval,
        AllowedCurrency = p.AllowedCurrency,
        Active = p.Active,
        EffectiveFrom = p.EffectiveFrom,
        EffectiveTo = p.EffectiveTo
    };
}
