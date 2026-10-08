namespace ExpenseGuard.Api.Models;

public class ApprovalWorkflowTemplate
{
    public int ApprovalWorkflowTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Department? Department { get; set; }
    public ICollection<ApprovalStageDefinition> Stages { get; set; } = [];
}

public class ApprovalStageDefinition
{
    public int ApprovalStageDefinitionId { get; set; }
    public int ApprovalWorkflowTemplateId { get; set; }
    public int Sequence { get; set; }
    public string RequiredRole { get; set; } = RoleNames.Manager;
    public decimal? MinimumAmount { get; set; }
    public decimal? MaximumAmount { get; set; }
    public ApprovalWorkflowTemplate Template { get; set; } = null!;
}

public class ApprovalProcess
{
    public Guid ApprovalProcessId { get; set; } = Guid.NewGuid();
    public int ReimbursementId { get; set; }
    public int ApprovalWorkflowTemplateId { get; set; }
    public string Status { get; set; } = ApprovalStatuses.Pending;
    public int CurrentSequence { get; set; } = 1;
    public string TemplateSnapshotJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Reimbursement Reimbursement { get; set; } = null!;
    public ApprovalWorkflowTemplate Template { get; set; } = null!;
    public ICollection<ApprovalStep> Steps { get; set; } = [];
}

public class ApprovalStep
{
    public Guid ApprovalStepId { get; set; } = Guid.NewGuid();
    public Guid ApprovalProcessId { get; set; }
    public int Sequence { get; set; }
    public string RequiredRole { get; set; } = string.Empty;
    public string Status { get; set; } = ApprovalStatuses.Pending;
    public int? DecidedByEmployeeId { get; set; }
    public string? Comment { get; set; }
    public string StageSnapshotJson { get; set; } = string.Empty;
    public DateTime? DecidedAt { get; set; }
    public ApprovalProcess ApprovalProcess { get; set; } = null!;
    public Employee? DecidedByEmployee { get; set; }
}

public static class ApprovalStatuses
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string RevisionRequired = "REVISION_REQUIRED";
}

public static class WorkflowSubjects
{
    public const string Claim = "claim";
    public const string PurchaseRequest = "purchase_request";
}

public class WorkflowExecution
{
    public Guid WorkflowExecutionId { get; set; } = Guid.NewGuid();
    public int? ExpenseClaimId { get; set; }
    public int? PurchaseRequestId { get; set; }
    public string SubjectType { get; set; } = WorkflowSubjects.Claim;
    public string Objective { get; set; } = string.Empty;
    public string Status { get; set; } = "CREATED";
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public string? StateJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ExpenseClaim? ExpenseClaim { get; set; }
    public PurchaseRequest? PurchaseRequest { get; set; }
    public ICollection<WorkflowStep> Steps { get; set; } = [];
}

public class WorkflowStep
{
    public Guid WorkflowStepId { get; set; } = Guid.NewGuid();
    public Guid WorkflowExecutionId { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "AGENT";
    public string Status { get; set; } = "PENDING";
    public string? InputSnapshotJson { get; set; }
    public string? OutputSnapshotJson { get; set; }
    public string? Error { get; set; }
    public WorkflowExecution WorkflowExecution { get; set; } = null!;
    public ICollection<ToolExecution> ToolExecutions { get; set; } = [];
    public ICollection<ValidationResult> ValidationResults { get; set; } = [];
}

public class ToolExecution
{
    public Guid ToolExecutionId { get; set; } = Guid.NewGuid();
    public Guid WorkflowStepId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public WorkflowStep WorkflowStep { get; set; } = null!;
}

public class ValidationResult
{
    public Guid ValidationResultId { get; set; } = Guid.NewGuid();
    public Guid WorkflowStepId { get; set; }
    public string Validator { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public WorkflowStep WorkflowStep { get; set; } = null!;
}

public class PaymentTransaction
{
    public Guid PaymentTransactionId { get; set; } = Guid.NewGuid();
    public int ReimbursementId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ExternalTransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    public string Status { get; set; } = "PENDING";
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Reimbursement Reimbursement { get; set; } = null!;
}

public class AuditLog
{
    public Guid AuditLogId { get; set; } = Guid.NewGuid();
    public int? EmployeeId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Employee? Employee { get; set; }
}
