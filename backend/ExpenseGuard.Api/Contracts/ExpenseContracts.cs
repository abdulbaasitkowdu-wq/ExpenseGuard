using System.ComponentModel.DataAnnotations;
using ExpenseGuard.Api.Models;
using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace ExpenseGuard.Api.Contracts;

public sealed record EmployeeProfileDto(
    int EmployeeId, string FullName, string Email, string Username, bool IsActive, bool IsLocked,
    int RoleId, int DepartmentId, string? DepartmentName, int? ManagerId, int? DesignationId, string? Designation);

public sealed class PurchaseRequestWriteDto
{
    [Required, StringLength(2000)] public string Description { get; init; } = string.Empty;
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal EstimatedAmount { get; init; }
    [Required, RegularExpression("^[A-Za-z]{3}$")] public string Currency { get; init; } = "LKR";
    [StringLength(200)] public string? Vendor { get; init; }
    [StringLength(100)] public string? Category { get; init; }
    [Range(0, long.MaxValue)] public long Version { get; init; }
}

public sealed record ReviewFlagDto(string Code, string Severity, string Message);
public sealed record ReviewSectionDto(
    string Outcome, string Summary, IReadOnlyList<ReviewFlagDto> Flags,
    decimal? Requested = null, decimal? Available = null, decimal? Allocated = null);
public sealed record PurchaseRequestReviewDto(
    string Category, string? PolicyCode, int DepartmentId, string? DepartmentName,
    string EmployeeName, string? Designation, bool HasFlags, string Summary,
    ReviewSectionDto Policy, ReviewSectionDto Fraud, ReviewSectionDto Budget);
public sealed record PurchaseRequestApprovalStepDto(
    int Sequence, string RequiredRole, string Status, string? Comment, int? DecidedByEmployeeId, DateTime? DecidedAt);

public sealed record PurchaseRequestDto(
    int PurchaseRequestId, int EmployeeId, string FullName, string? DepartmentName, string? Designation,
    string Description, decimal EstimatedAmount, string Currency, string? Vendor, string? Category,
    PurchaseRequestStatus Status, DateTime? SubmittedAt, long Version, string? CurrentRequiredRole,
    PurchaseRequestReviewDto? Review, IReadOnlyList<PurchaseRequestApprovalStepDto> ApprovalSteps);

public sealed class ClaimWriteDto : IValidatableObject
{
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal Amount { get; init; }
    [Required, StringLength(100)] public string Category { get; init; } = string.Empty;
    [Required, StringLength(2000)] public string Description { get; init; } = string.Empty;
    [Required, RegularExpression("^[A-Za-z]{3}$")] public string Currency { get; init; } = "LKR";
    [StringLength(200)] public string? Vendor { get; init; }
    public DateTime? PurchaseDate { get; init; }
    public int? PurchaseRequestId { get; init; }
    public ClaimFlow Flow { get; init; } = ClaimFlow.OutOfPocket;
    [Range(0, long.MaxValue)] public long Version { get; init; }

    public IEnumerable<DataAnnotationsValidationResult> Validate(ValidationContext validationContext)
    {
        if (Flow == ClaimFlow.PrePurchase && PurchaseRequestId is null)
            yield return new DataAnnotationsValidationResult("Pre-purchase claims require a purchase request.", [nameof(PurchaseRequestId)]);
        if (Flow == ClaimFlow.OutOfPocket && PurchaseRequestId is not null)
            yield return new DataAnnotationsValidationResult("Out-of-pocket claims cannot link a purchase request.", [nameof(PurchaseRequestId)]);
    }
}

public sealed record ClaimDto(
    int ExpenseClaimId, int EmployeeId, int? PurchaseRequestId, decimal Amount, string Category,
    string Description, string Currency, string? Vendor, DateTime? PurchaseDate, ClaimFlow Flow,
    ClaimStatus Status, long Version, string? CurrentRequiredRole = null,
    IReadOnlyList<PurchaseRequestApprovalStepDto>? ApprovalSteps = null);

public sealed class ReceiptCorrectionDto
{
    [StringLength(200)] public string? Vendor { get; init; }
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal? Amount { get; init; }
    public DateTime? PurchaseDate { get; init; }
    [RegularExpression("^[A-Za-z]{3}$")] public string? Currency { get; init; }
}

public sealed record ReceiptDto(
    int ReceiptId, int ExpenseClaimId, string StorageUrl, string FileName, string ContentType,
    long SizeBytes, string Sha256, ReceiptProcessingStatus ProcessingStatus, string? ExtractedVendor,
    decimal? ExtractedAmount, DateTime? ExtractedDate, string? ExtractedCurrency, string? ExtractedText,
    decimal? Confidence, bool RequiresManualReview, DateTime? CorrectedAt);

public sealed record ClaimHistoryDto(
    int ClaimStatusHistoryId, ClaimStatus FromStatus, ClaimStatus ToStatus,
    int ChangedByEmployeeId, string? Reason, DateTime ChangedAt);

public sealed record PurchaseRequestHistoryDto(
    int PurchaseRequestStatusHistoryId, PurchaseRequestStatus FromStatus, PurchaseRequestStatus ToStatus,
    int ChangedByEmployeeId, string? Reason, DateTime ChangedAt);

public sealed record RequestHistoryDto(
    string Kind, int RequestId, int EmployeeId, string EmployeeName, string? DepartmentName,
    string? Vendor, string? Category, decimal Amount, string Currency,
    string FromStatus, string ToStatus, int ChangedByEmployeeId, string? Reason, DateTime ChangedAt);

public sealed class RequestHistoryQuery
{
    [StringLength(40)] public string? Kind { get; init; }
    public int? EmployeeId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    [Range(1, 200)] public int Limit { get; init; } = 100;
}

public sealed class ClaimSearchQuery
{
    public ClaimStatus? Status { get; init; }
    [StringLength(100)] public string? Category { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    [Range(1, 100)] public int Limit { get; init; } = 50;
}
