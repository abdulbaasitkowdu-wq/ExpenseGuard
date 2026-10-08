using System.ComponentModel.DataAnnotations;

namespace ExpenseGuard.Api.Contracts;

public class CreatePolicyRequest : IValidatableObject
{
    [Required, StringLength(50)] public string PolicyCode { get; init; } = "";
    [Required, StringLength(100)] public string Category { get; init; } = "";
    [Range(0, double.MaxValue)] public decimal? MinAmount { get; init; }
    [Range(0, double.MaxValue)] public decimal? MaxAmount { get; init; }
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; init; } = "LKR";
    public bool ReceiptRequired { get; init; }
    [Range(-10000, 10000)] public int Priority { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public int? DepartmentId { get; init; }
    [MaxLength(50)] public IReadOnlyCollection<string> Designations { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveFrom == default)
            yield return new("EffectiveFrom is required.", [nameof(EffectiveFrom)]);
        if (MinAmount is not null && MaxAmount is not null && MinAmount > MaxAmount)
            yield return new("MinAmount cannot exceed MaxAmount.", [nameof(MinAmount), nameof(MaxAmount)]);
        if (EffectiveTo is not null && EffectiveTo <= EffectiveFrom)
            yield return new("EffectiveTo must be later than EffectiveFrom.", [nameof(EffectiveTo)]);
    }
}

public sealed class CreatePolicyVersionRequest : CreatePolicyRequest
{
    public bool Activate { get; init; } = true;
}

public sealed record PolicyDto(
    int PolicyId, string PolicyCode, int Version, string Category, decimal? MinAmount,
    decimal? MaxAmount, string Currency, bool ReceiptRequired, bool IsActive, int Priority,
    DateTime EffectiveFrom, DateTime? EffectiveTo, int? DepartmentId, IReadOnlyCollection<string> Designations);

public sealed class PolicyEvaluateRequest
{
    [Range(1, int.MaxValue)] public int ExpenseClaimId { get; init; }
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; init; } = "LKR";
    [StringLength(100)] public string? Designation { get; init; }
    [Range(0, double.MaxValue)] public decimal? ReceiptAmount { get; init; }
    public DateTime? At { get; init; }
}

public sealed record ViolationDto(string RuleCode, string Message, string Severity, decimal? ExpectedAmount, decimal? ActualAmount);
public sealed record PolicyEvaluationDto(int EvaluationId, int ExpenseClaimId, int? PolicyId, string Outcome, DateTime EvaluatedAt, IReadOnlyCollection<ViolationDto> Violations);

public sealed class FraudEvaluateRequest
{
    [Range(1, int.MaxValue)] public int ExpenseClaimId { get; init; }
    [StringLength(100)] public string? InvoiceNumber { get; init; }
    [Range(0, double.MaxValue)] public decimal? ReceiptAmount { get; init; }
}

public sealed class ReviewFraudFlagRequest
{
    [Required, RegularExpression("^(under_review|dismissed|confirmed)$")] public string Status { get; init; } = "";
    [StringLength(1000)] public string? Note { get; init; }
}

public sealed class ResolveFraudFlagRequest
{
    [Required, StringLength(1000, MinimumLength = 3)] public string ResolutionNote { get; init; } = "";
}

public sealed record FraudFlagDto(int FraudFlagId, int ExpenseClaimId, string RuleCode, string Severity,
    string Source, string Reason, string EvidenceJson, decimal RiskScore, string Status,
    string? ResolutionNote, string? ResolvedBy, DateTime? ResolvedAt, DateTime CreatedAt);
public sealed record FraudEvaluationDto(int EvaluationId, int ExpenseClaimId, decimal RiskScore, string RiskLevel, DateTime EvaluatedAt, IReadOnlyCollection<FraudFlagDto> Flags);
public sealed record PageResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int Total);
