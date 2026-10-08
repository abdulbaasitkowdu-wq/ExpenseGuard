using Microsoft.EntityFrameworkCore;
using ExpenseGuard.Api.Models;

namespace ExpenseGuard.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<ExpenseClaim> ExpenseClaims => Set<ExpenseClaim>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyDesignation> PolicyDesignations => Set<PolicyDesignation>();
    public DbSet<PolicyEvaluation> PolicyEvaluations => Set<PolicyEvaluation>();
    public DbSet<PolicyViolation> PolicyViolations => Set<PolicyViolation>();
    public DbSet<FraudFlag> FraudFlags => Set<FraudFlag>();
    public DbSet<FraudEvaluation> FraudEvaluations => Set<FraudEvaluation>();
    public DbSet<Reimbursement> Reimbursements => Set<Reimbursement>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetTransaction> BudgetTransactions => Set<BudgetTransaction>();
    public DbSet<BudgetAlert> BudgetAlerts => Set<BudgetAlert>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ClaimStatusHistory> ClaimStatusHistories => Set<ClaimStatusHistory>();
    public DbSet<PurchaseRequestStatusHistory> PurchaseRequestStatusHistories => Set<PurchaseRequestStatusHistory>();
    public DbSet<ApprovalWorkflowTemplate> ApprovalWorkflowTemplates => Set<ApprovalWorkflowTemplate>();
    public DbSet<ApprovalStageDefinition> ApprovalStageDefinitions => Set<ApprovalStageDefinition>();
    public DbSet<ApprovalProcess> ApprovalProcesses => Set<ApprovalProcess>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<WorkflowExecution> WorkflowExecutions => Set<WorkflowExecution>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<ToolExecution> ToolExecutions => Set<ToolExecution>();
    public DbSet<ValidationResult> ValidationResults => Set<ValidationResult>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>()
            .HasIndex(e => e.Username)
            .IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(e => e.NormalizedUsername).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(e => e.Email).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(r => r.RoleName).IsUnique();

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Manager)
            .WithMany(e => e.DirectReports)
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Role)
            .WithMany(r => r.Employees)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Designation)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DesignationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ExpenseClaim>()
            .HasOne(c => c.Employee)
            .WithMany(e => e.ExpenseClaims)
            .HasForeignKey(c => c.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseRequest>()
            .HasOne(p => p.Employee)
            .WithMany(e => e.PurchaseRequests)
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExpenseClaim>()
            .HasOne(c => c.PurchaseRequest)
            .WithMany(p => p.Claims)
            .HasForeignKey(c => c.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.ExpenseClaim)
            .WithMany(c => c.Receipts)
            .HasForeignKey(r => r.ExpenseClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClaimStatusHistory>()
            .HasOne(h => h.ExpenseClaim)
            .WithMany(c => c.StatusHistory)
            .HasForeignKey(h => h.ExpenseClaimId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PurchaseRequestStatusHistory>()
            .HasOne(h => h.PurchaseRequest)
            .WithMany(p => p.StatusHistory)
            .HasForeignKey(h => h.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PurchaseRequestStatusHistory>().HasIndex(h => new { h.PurchaseRequestId, h.ChangedAt });
        modelBuilder.Entity<PurchaseRequestStatusHistory>().Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<PurchaseRequestStatusHistory>().Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(30);

        modelBuilder.Entity<Policy>()
            .HasOne(p => p.Department)
            .WithMany(d => d.Policies)
            .HasForeignKey(p => p.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Policy>()
            .HasIndex(p => new { p.PolicyCode, p.Version })
            .IsUnique();
        modelBuilder.Entity<Policy>()
            .HasIndex(p => new { p.IsActive, p.Category, p.Currency, p.DepartmentId, p.EffectiveFrom, p.EffectiveTo });
        modelBuilder.Entity<PolicyDesignation>()
            .HasIndex(x => new { x.PolicyId, x.Designation })
            .IsUnique();
        modelBuilder.Entity<PolicyEvaluation>()
            .HasIndex(x => new { x.ExpenseClaimId, x.InputFingerprint })
            .IsUnique();
        modelBuilder.Entity<PolicyEvaluation>()
            .HasOne(x => x.ExpenseClaim).WithMany(x => x.PolicyEvaluations)
            .HasForeignKey(x => x.ExpenseClaimId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PolicyEvaluation>()
            .HasOne(x => x.Policy).WithMany(x => x.Evaluations)
            .HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PolicyViolation>()
            .HasOne(x => x.PolicyEvaluation).WithMany(x => x.Violations)
            .HasForeignKey(x => x.PolicyEvaluationId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Budget>()
            .HasOne(b => b.Department)
            .WithMany(d => d.Budgets)
            .HasForeignKey(b => b.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BudgetTransaction>()
            .HasOne(t => t.Budget)
            .WithMany(b => b.Transactions)
            .HasForeignKey(t => t.BudgetId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<BudgetAlert>()
            .HasOne(a => a.Budget)
            .WithMany(b => b.Alerts)
            .HasForeignKey(a => a.BudgetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FraudFlag>()
            .HasOne(f => f.ExpenseClaim)
            .WithMany(c => c.FraudFlags)
            .HasForeignKey(f => f.ExpenseClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<FraudFlag>()
            .HasOne(x => x.FraudEvaluation).WithMany(x => x.Flags)
            .HasForeignKey(x => x.FraudEvaluationId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<FraudEvaluation>()
            .HasOne(x => x.ExpenseClaim).WithMany(x => x.FraudEvaluations)
            .HasForeignKey(x => x.ExpenseClaimId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<FraudEvaluation>()
            .HasIndex(x => new { x.ExpenseClaimId, x.InputFingerprint }).IsUnique();
        modelBuilder.Entity<FraudEvaluation>()
            .HasIndex(x => x.NormalizedInvoiceNumber);
        modelBuilder.Entity<FraudFlag>()
            .HasIndex(x => new { x.Status, x.Severity, x.CreatedAt });

        modelBuilder.Entity<Reimbursement>()
            .HasOne(r => r.ExpenseClaim)
            .WithOne(c => c.Reimbursement)
            .HasForeignKey<Reimbursement>(r => r.ExpenseClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Reimbursement>().HasIndex(r => r.IdempotencyKey).IsUnique();
        modelBuilder.Entity<PaymentTransaction>().HasIndex(p => p.IdempotencyKey).IsUnique();
        modelBuilder.Entity<ApprovalStageDefinition>()
            .HasIndex(s => new { s.ApprovalWorkflowTemplateId, s.Sequence }).IsUnique();
        modelBuilder.Entity<ApprovalStep>()
            .HasIndex(s => new { s.ApprovalProcessId, s.Sequence }).IsUnique();

        modelBuilder.Entity<ApprovalProcess>()
            .HasOne(p => p.Reimbursement).WithMany(r => r.ApprovalProcesses)
            .HasForeignKey(p => p.ReimbursementId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ApprovalStep>()
            .HasOne(s => s.DecidedByEmployee).WithMany()
            .HasForeignKey(s => s.DecidedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<WorkflowExecution>()
            .HasOne(w => w.ExpenseClaim).WithMany()
            .HasForeignKey(w => w.ExpenseClaimId).OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
        modelBuilder.Entity<WorkflowExecution>()
            .HasOne(w => w.PurchaseRequest).WithMany()
            .HasForeignKey(w => w.PurchaseRequestId).OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
        modelBuilder.Entity<WorkflowExecution>().Property(w => w.SubjectType).HasMaxLength(40);
        modelBuilder.Entity<WorkflowExecution>().HasIndex(w => w.PurchaseRequestId);
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.Employee).WithMany()
            .HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Department>().HasIndex(d => d.Code).IsUnique();
        modelBuilder.Entity<Budget>()
            .HasIndex(b => new { b.DepartmentId, b.PeriodStart, b.PeriodEnd, b.Currency })
            .IsUnique();
        modelBuilder.Entity<BudgetTransaction>()
            .HasIndex(t => new { t.BudgetId, t.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        modelBuilder.Entity<BudgetTransaction>().HasIndex(t => new { t.BudgetId, t.CreatedAt });
        modelBuilder.Entity<BudgetAlert>()
            .HasIndex(a => new { a.BudgetId, a.ThresholdPercent, a.Status });

        modelBuilder.Entity<ExpenseClaim>()
            .HasIndex(c => c.Status);

        modelBuilder.Entity<Employee>().Property(e => e.Username).HasMaxLength(100);
        modelBuilder.Entity<Role>().Property(r => r.RoleName).HasMaxLength(50);
        modelBuilder.Entity<ExpenseClaim>().Property(c => c.Category).HasMaxLength(100);
        modelBuilder.Entity<ExpenseClaim>().Property(c => c.Status).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<ExpenseClaim>().Property(c => c.Flow).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<ExpenseClaim>().Property(c => c.PurchaseNo).HasMaxLength(100);
        modelBuilder.Entity<ExpenseClaim>().Property(c => c.Currency).HasMaxLength(3);
        modelBuilder.Entity<PurchaseRequest>().Property(p => p.Status).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<PurchaseRequest>().Property(p => p.Currency).HasMaxLength(3);
        modelBuilder.Entity<PurchaseRequest>().Property(p => p.Category).HasMaxLength(100);
        modelBuilder.Entity<PurchaseRequest>().Property(p => p.CurrentRequiredRole).HasMaxLength(50);
        modelBuilder.Entity<PurchaseRequest>().Property(p => p.ReviewJson).HasColumnType("jsonb");
        modelBuilder.Entity<PurchaseRequest>().Property(p => p.ApprovalJson).HasColumnType("jsonb");
        modelBuilder.Entity<PurchaseRequest>().HasIndex(p => new { p.Status, p.CurrentRequiredRole });
        modelBuilder.Entity<Receipt>().Property(r => r.ProcessingStatus).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<Receipt>().HasIndex(r => new { r.ExpenseClaimId, r.Sha256 }).IsUnique();
        modelBuilder.Entity<Designation>().HasIndex(d => d.Name).IsUnique();
        modelBuilder.Entity<Employee>().Property(e => e.FullName).HasMaxLength(200);
        modelBuilder.Entity<Employee>().Property(e => e.Email).HasMaxLength(320);
        modelBuilder.Entity<Employee>().Property(e => e.NormalizedUsername).HasMaxLength(100);
        modelBuilder.Entity<Policy>().Property(p => p.Category).HasMaxLength(100);
        modelBuilder.Entity<Policy>().Property(p => p.PolicyCode).HasMaxLength(50);
        modelBuilder.Entity<Policy>().Property(p => p.Currency).HasMaxLength(3);
        modelBuilder.Entity<PolicyDesignation>().Property(p => p.Designation).HasMaxLength(100);
        modelBuilder.Entity<PolicyEvaluation>().Property(p => p.InputFingerprint).HasMaxLength(64);
        modelBuilder.Entity<PolicyEvaluation>().Property(p => p.Outcome).HasMaxLength(30);
        modelBuilder.Entity<PolicyViolation>().Property(p => p.RuleCode).HasMaxLength(60);
        modelBuilder.Entity<PolicyViolation>().Property(p => p.Severity).HasMaxLength(20);
        modelBuilder.Entity<FraudEvaluation>().Property(p => p.InputFingerprint).HasMaxLength(64);
        modelBuilder.Entity<FraudEvaluation>().Property(p => p.NormalizedInvoiceNumber).HasMaxLength(100);
        modelBuilder.Entity<FraudEvaluation>().Property(p => p.RiskLevel).HasMaxLength(20);
        modelBuilder.Entity<FraudFlag>().Property(p => p.RuleCode).HasMaxLength(60);
        modelBuilder.Entity<FraudFlag>().Property(p => p.Severity).HasMaxLength(20);
        modelBuilder.Entity<FraudFlag>().Property(p => p.Source).HasMaxLength(30);
        modelBuilder.Entity<FraudFlag>().Property(p => p.Status).HasMaxLength(30);
        modelBuilder.Entity<FraudFlag>().Property(p => p.EvidenceJson).HasColumnType("jsonb");
        modelBuilder.Entity<Reimbursement>().Property(r => r.Status).HasMaxLength(30);
        modelBuilder.Entity<Department>().Property(d => d.Code).HasMaxLength(20);
        modelBuilder.Entity<Department>().Property(d => d.DepartmentName).HasMaxLength(120);
        modelBuilder.Entity<Department>().Property(d => d.Version).IsConcurrencyToken();
        modelBuilder.Entity<Budget>().Property(b => b.Name).HasMaxLength(120);
        modelBuilder.Entity<Budget>().Property(b => b.Currency).HasMaxLength(3).IsFixedLength();
        modelBuilder.Entity<Budget>().Property(b => b.Version).IsConcurrencyToken();
        modelBuilder.Entity<BudgetTransaction>().Property(t => t.Reference).HasMaxLength(100);
        modelBuilder.Entity<BudgetTransaction>().Property(t => t.Description).HasMaxLength(500);
        modelBuilder.Entity<BudgetTransaction>().Property(t => t.IdempotencyKey).HasMaxLength(100);
        modelBuilder.Entity<BudgetAlert>().Property(a => a.Message).HasMaxLength(500);
        modelBuilder.Entity<BudgetTransaction>().Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<BudgetAlert>().Property(a => a.Severity).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<BudgetAlert>().Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        modelBuilder.Entity<Role>().Property(r => r.ApprovalLimit).HasPrecision(18, 2);
        modelBuilder.Entity<ExpenseClaim>().Property(c => c.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<ExpenseClaim>().Property(c => c.Version).IsConcurrencyToken();
        modelBuilder.Entity<PurchaseRequest>().Property(p => p.EstimatedAmount).HasPrecision(18, 2);
        modelBuilder.Entity<PurchaseRequest>().Property(p => p.Version).IsConcurrencyToken();
        modelBuilder.Entity<Receipt>().Property(r => r.ExtractedAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Receipt>().Property(r => r.Confidence).HasPrecision(5, 4);
        modelBuilder.Entity<Policy>().Property(p => p.MinAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Policy>().Property(p => p.MaxAmount).HasPrecision(18, 2);
        modelBuilder.Entity<PolicyViolation>().Property(p => p.ExpectedAmount).HasPrecision(18, 2);
        modelBuilder.Entity<PolicyViolation>().Property(p => p.ActualAmount).HasPrecision(18, 2);
        modelBuilder.Entity<FraudFlag>().Property(f => f.RiskScore).HasPrecision(5, 2);
        modelBuilder.Entity<FraudEvaluation>().Property(f => f.RiskScore).HasPrecision(5, 2);
        modelBuilder.Entity<FraudEvaluation>().Property(f => f.ClaimAmount).HasPrecision(18, 2);
        modelBuilder.Entity<FraudEvaluation>().Property(f => f.ReceiptAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Reimbursement>().Property(r => r.Total).HasPrecision(18, 2);
        modelBuilder.Entity<PaymentTransaction>().Property(r => r.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<ApprovalStageDefinition>().Property(r => r.MinimumAmount).HasPrecision(18, 2);
        modelBuilder.Entity<ApprovalStageDefinition>().Property(r => r.MaximumAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Budget>().Property(b => b.AllocatedAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Budget>().Property(b => b.ReservedAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Budget>().Property(b => b.SpentAmount).HasPrecision(18, 2);
        modelBuilder.Entity<BudgetTransaction>().Property(t => t.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<BudgetTransaction>().Property(t => t.AllocatedBalance).HasPrecision(18, 2);
        modelBuilder.Entity<BudgetTransaction>().Property(t => t.ReservedBalance).HasPrecision(18, 2);
        modelBuilder.Entity<BudgetTransaction>().Property(t => t.SpentBalance).HasPrecision(18, 2);
        modelBuilder.Entity<BudgetAlert>().Property(a => a.ThresholdPercent).HasPrecision(5, 2);
        modelBuilder.Entity<BudgetAlert>().Property(a => a.UtilizationPercent).HasPrecision(7, 2);

        modelBuilder.Entity<Budget>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Budgets_Dates", "\"PeriodEnd\" >= \"PeriodStart\"");
            t.HasCheckConstraint("CK_Budgets_Balances", "\"AllocatedAmount\" >= 0 AND \"ReservedAmount\" >= 0 AND \"SpentAmount\" >= 0 AND \"ReservedAmount\" + \"SpentAmount\" <= \"AllocatedAmount\"");
        });
        modelBuilder.Entity<BudgetTransaction>().ToTable(t =>
            t.HasCheckConstraint("CK_BudgetTransactions_Amount", "\"Amount\" > 0"));

        modelBuilder.Entity<Role>().HasData(
            new Role { RoleId = 1, RoleName = RoleNames.Employee, PermissionsJson = "[\"reimbursement:self\"]" },
            new Role { RoleId = 2, RoleName = RoleNames.Manager, CanApprove = true, ApprovalLimit = 100000, PermissionsJson = "[\"reimbursement:approve\"]" },
            new Role { RoleId = 3, RoleName = RoleNames.DepartmentHead, CanApprove = true, ApprovalLimit = 1000000, PermissionsJson = "[\"reimbursement:approve\"]" },
            new Role { RoleId = 4, RoleName = RoleNames.Finance, CanProcessPayments = true, PermissionsJson = "[\"payment:process\"]" },
            new Role { RoleId = 5, RoleName = RoleNames.Admin, CanApprove = true, CanProcessPayments = true, CanManageRoles = true, ApprovalLimit = 9999999999999999.99m, PermissionsJson = "[\"*\"]" });
    }
}
