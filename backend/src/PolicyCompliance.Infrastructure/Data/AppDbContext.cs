using Microsoft.EntityFrameworkCore;
using PolicyCompliance.Core.Entities;

namespace PolicyCompliance.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ExpenseClaim> ExpenseClaims => Set<ExpenseClaim>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<ExpensePolicy> ExpensePolicies => Set<ExpensePolicy>();
    public DbSet<PolicyViolation> PolicyViolations => Set<PolicyViolation>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<DuplicateMatch> DuplicateMatches => Set<DuplicateMatch>();
    public DbSet<ManagerReview> ManagerReviews => Set<ManagerReview>();
    public DbSet<ComplianceCheck> ComplianceChecks => Set<ComplianceCheck>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AgentExecution> AgentExecutions => Set<AgentExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Department
        modelBuilder.Entity<Department>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.Name).IsRequired().HasMaxLength(150);
            b.Property(d => d.Code).IsRequired().HasMaxLength(50);
            b.Property(d => d.MonthlyBudgetLimit).HasPrecision(18, 2);
        });

        // User
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Username).IsRequired().HasMaxLength(100);
            b.HasIndex(u => u.Username).IsUnique();
            b.Property(u => u.Email).IsRequired().HasMaxLength(200);
            b.Property(u => u.FullName).IsRequired().HasMaxLength(150);
            b.Property(u => u.Role).HasConversion<string>();

            b.HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ExpenseClaim
        modelBuilder.Entity<ExpenseClaim>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.ClaimNumber).IsRequired().HasMaxLength(50);
            b.HasIndex(c => c.ClaimNumber).IsUnique();
            b.Property(c => c.TotalAmount).HasPrecision(18, 2);
            b.Property(c => c.Currency).IsRequired().HasMaxLength(10);
            b.Property(c => c.MerchantName).HasMaxLength(200);
            b.Property(c => c.Category).IsRequired().HasMaxLength(100);
            b.Property(c => c.Status).HasConversion<string>();
            b.Property(c => c.PolicyStatus).HasConversion<string>();
            b.Property(c => c.RiskStatus).HasConversion<string>();

            b.HasIndex(c => c.Status);
            b.HasIndex(c => c.PolicyStatus);
            b.HasIndex(c => c.RiskStatus);
            b.HasIndex(c => c.ClaimDate);

            b.HasOne(c => c.Employee)
                .WithMany(u => u.ExpenseClaims)
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(c => c.Department)
                .WithMany(d => d.ExpenseClaims)
                .HasForeignKey(c => c.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ExpenseItem
        modelBuilder.Entity<ExpenseItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.Amount).HasPrecision(18, 2);
            b.Property(i => i.Currency).IsRequired().HasMaxLength(10);
            b.Property(i => i.Category).IsRequired().HasMaxLength(100);
            b.Property(i => i.Merchant).HasMaxLength(200);
            b.Property(i => i.ReceiptUrl).HasMaxLength(500);

            b.HasOne(i => i.ExpenseClaim)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.ExpenseClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ExpensePolicy
        modelBuilder.Entity<ExpensePolicy>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.PolicyName).IsRequired().HasMaxLength(150);
            b.Property(p => p.Category).IsRequired().HasMaxLength(100);
            b.Property(p => p.MaximumAmount).HasPrecision(18, 2);
            b.Property(p => p.AllowedCurrency).IsRequired().HasMaxLength(10);

            b.HasOne(p => p.Department)
                .WithMany(d => d.Policies)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PolicyViolation
        modelBuilder.Entity<PolicyViolation>(b =>
        {
            b.HasKey(v => v.Id);
            b.Property(v => v.RuleCode).IsRequired().HasMaxLength(100);
            b.Property(v => v.Severity).HasConversion<string>();

            b.HasOne(v => v.ExpenseClaim)
                .WithMany(c => c.Violations)
                .HasForeignKey(v => v.ExpenseClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(v => v.Policy)
                .WithMany()
                .HasForeignKey(v => v.PolicyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // RiskAssessment
        modelBuilder.Entity<RiskAssessment>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.RiskLevel).HasConversion<string>();
            b.Property(r => r.AgentVersion).HasMaxLength(50);

            b.HasOne(r => r.ExpenseClaim)
                .WithMany(c => c.RiskAssessments)
                .HasForeignKey(r => r.ExpenseClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DuplicateMatch
        modelBuilder.Entity<DuplicateMatch>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.SimilarityScore).HasPrecision(5, 2);
            b.Property(m => m.MatchType).HasConversion<string>();

            b.HasOne(m => m.ExpenseClaim)
                .WithMany(c => c.DuplicateMatches)
                .HasForeignKey(m => m.ExpenseClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(m => m.MatchingClaim)
                .WithMany()
                .HasForeignKey(m => m.MatchingClaimId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ManagerReview
        modelBuilder.Entity<ManagerReview>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.Decision).HasConversion<string>();

            b.HasOne(r => r.ExpenseClaim)
                .WithMany(c => c.Reviews)
                .HasForeignKey(r => r.ExpenseClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(r => r.Manager)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ComplianceCheck
        modelBuilder.Entity<ComplianceCheck>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.CheckType).HasConversion<string>();
            b.Property(c => c.Result).HasConversion<string>();

            b.HasOne(c => c.ExpenseClaim)
                .WithMany(cl => cl.ComplianceChecks)
                .HasForeignKey(c => c.ExpenseClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
            b.Property(a => a.Action).IsRequired().HasMaxLength(100);
            b.Property(a => a.ActorRole).HasMaxLength(50);
            b.Property(a => a.CorrelationId).HasMaxLength(100);
            b.HasIndex(a => a.EntityId);
            b.HasIndex(a => a.Timestamp);

            b.HasOne(a => a.ExpenseClaim)
                .WithMany(c => c.AuditLogs)
                .HasForeignKey(a => a.EntityId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);
        });

        // AgentExecution
        modelBuilder.Entity<AgentExecution>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.WorkflowId).IsRequired().HasMaxLength(100);
            b.Property(e => e.AgentName).IsRequired().HasMaxLength(100);
            b.Property(e => e.Status).HasConversion<string>();
            b.HasIndex(e => e.WorkflowId);
        });
    }
}
