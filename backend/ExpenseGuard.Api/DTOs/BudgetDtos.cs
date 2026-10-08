using System.ComponentModel.DataAnnotations;
using ExpenseGuard.Api.Models;
using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace ExpenseGuard.Api.DTOs;

public sealed record DepartmentDto(int Id, string Code, string Name, bool IsActive, Guid Version);
public sealed record BudgetDto(int Id, int DepartmentId, string Name, DateOnly PeriodStart, DateOnly PeriodEnd,
    string Currency, decimal AllocatedAmount, decimal ReservedAmount, decimal SpentAmount,
    decimal AvailableAmount, bool IsActive, Guid Version);
public sealed record BudgetTransactionDto(long Id, BudgetTransactionType Type, decimal Amount,
    decimal AllocatedBalance, decimal ReservedBalance, decimal SpentBalance, string? Reference,
    string? Description, DateTime CreatedAt);
public sealed record BudgetAlertDto(long Id, decimal ThresholdPercent, decimal UtilizationPercent,
    BudgetAlertSeverity Severity, BudgetAlertStatus Status, string Message, DateTime CreatedAt);
public sealed record AvailabilityDto(int BudgetId, decimal RequestedAmount, decimal AvailableAmount, bool IsAvailable);
public sealed record UtilizationDto(int BudgetId, int DepartmentId, string DepartmentName, string BudgetName, string Currency,
    decimal AllocatedAmount, decimal ReservedAmount, decimal SpentAmount, decimal AvailableAmount,
    decimal UtilizationPercent, int OpenAlerts);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed class CreateDepartmentRequest
{
    [Required, RegularExpression("^[A-Za-z0-9_-]{2,20}$")]
    public string Code { get; init; } = string.Empty;
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateDepartmentRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public Guid Version { get; init; }
}

public sealed class AllocateBudgetRequest : IValidatableObject
{
    [Range(1, int.MaxValue)] public int DepartmentId { get; init; }
    [Required, StringLength(120)] public string Name { get; init; } = string.Empty;
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; init; } = "LKR";
    [Range(typeof(decimal), "0.01", "100000000")] public decimal Amount { get; init; }
    [StringLength(100)] public string? IdempotencyKey { get; init; }

    public IEnumerable<DataAnnotationsValidationResult> Validate(ValidationContext validationContext)
    {
        if (PeriodEnd < PeriodStart)
            yield return new DataAnnotationsValidationResult("PeriodEnd must be on or after PeriodStart.", [nameof(PeriodEnd)]);
    }
}

public class BudgetAmountRequest
{
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal Amount { get; init; }
    [Required, StringLength(100)] public string IdempotencyKey { get; init; } = string.Empty;
    [StringLength(100)] public string? Reference { get; init; }
    [StringLength(500)] public string? Description { get; init; }
    public Guid Version { get; init; }
}

public sealed class SpendBudgetRequest : BudgetAmountRequest
{
    public bool FromReservation { get; init; } = true;
}
