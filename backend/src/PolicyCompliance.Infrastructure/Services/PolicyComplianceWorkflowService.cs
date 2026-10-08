using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolicyCompliance.Core.Agents.Contracts;
using PolicyCompliance.Core.Agents.FraudAnomalyRiskAgent;
using PolicyCompliance.Core.DTOs;
using PolicyCompliance.Core.Entities;
using PolicyCompliance.Core.Enums;
using PolicyCompliance.Core.Services;
using PolicyCompliance.Core.Validators;
using PolicyCompliance.Infrastructure.Data;

namespace PolicyCompliance.Infrastructure.Services;

public class PolicyComplianceWorkflowService : IPolicyComplianceWorkflowService
{
    private readonly AppDbContext _context;
    private readonly IPolicyValidationEngine _policyEngine;
    private readonly IFraudAnomalyRiskAgent _riskAgent;

    public PolicyComplianceWorkflowService(
        AppDbContext context,
        IPolicyValidationEngine policyEngine,
        IFraudAnomalyRiskAgent riskAgent)
    {
        _context = context;
        _policyEngine = policyEngine;
        _riskAgent = riskAgent;
    }

    public async Task<ExpenseClaimDetailDto> SubmitClaimAsync(Guid employeeId, CreateExpenseClaimDto dto, CancellationToken cancellationToken = default)
    {
        var employee = await _context.Users
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == employeeId, cancellationToken);

        if (employee == null)
            throw new InvalidOperationException($"Employee with ID {employeeId} not found.");

        string correlationId = Guid.NewGuid().ToString();

        // 1. Create claim
        int existingCount = await _context.ExpenseClaims.CountAsync(cancellationToken);
        string claimNumber = $"CLM-2026-{1000 + existingCount + 1}";

        var claim = new ExpenseClaim
        {
            EmployeeId = employeeId,
            DepartmentId = employee.DepartmentId,
            ClaimNumber = claimNumber,
            ClaimDate = dto.ClaimDate,
            SubmittedAt = DateTime.UtcNow,
            TotalAmount = dto.TotalAmount,
            Currency = dto.Currency,
            MerchantName = dto.MerchantName,
            Category = dto.Category,
            Description = dto.Description,
            Status = ClaimStatus.SUBMITTED,
            PolicyStatus = PolicyStatus.PENDING,
            RiskStatus = RiskStatus.NOT_ASSESSED,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var item in dto.Items)
        {
            claim.Items.Add(new ExpenseItem
            {
                ExpenseDate = item.ExpenseDate,
                Category = item.Category,
                Merchant = item.Merchant,
                Amount = item.Amount,
                Currency = item.Currency,
                Description = item.Description,
                ReceiptUrl = item.ReceiptUrl
            });
        }

        _context.ExpenseClaims.Add(claim);
        await _context.SaveChangesAsync(cancellationToken);

        // Audit: CLAIM_SUBMITTED
        await AddAuditLogAsync(claim.Id, "CLAIM_SUBMITTED", employeeId, employee.Role.ToString(),
            "", ClaimStatus.SUBMITTED.ToString(), $"Claim {claimNumber} submitted for {claim.TotalAmount:N2} {claim.Currency}.", correlationId, cancellationToken);

        // 2. Execute Validation & Agent Workflow
        await RunValidationAndRiskAssessmentPipelineAsync(claim.Id, correlationId, cancellationToken);

        return (await GetClaimDetailsAsync(claim.Id, cancellationToken))!;
    }

    public async Task<ExpenseClaimDetailDto> ResubmitClaimAsync(Guid claimId, Guid employeeId, ResubmitExpenseClaimDto dto, CancellationToken cancellationToken = default)
    {
        var claim = await _context.ExpenseClaims
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);

        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        if (claim.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("Only the claim owner can resubmit.");

        if (claim.Status != ClaimStatus.REVISION_REQUIRED)
            throw new InvalidOperationException($"Claim cannot be resubmitted when in status {claim.Status}. Only REVISION_REQUIRED claims can be resubmitted.");

        string oldStatus = claim.Status.ToString();
        string correlationId = Guid.NewGuid().ToString();

        claim.TotalAmount = dto.TotalAmount;
        claim.MerchantName = dto.MerchantName;
        claim.Category = dto.Category;
        claim.Description = dto.Description;
        claim.Status = ClaimStatus.SUBMITTED;
        claim.UpdatedAt = DateTime.UtcNow;

        // Clear existing items and replace with updated ones
        _context.ExpenseItems.RemoveRange(claim.Items);
        claim.Items.Clear();

        foreach (var item in dto.Items)
        {
            claim.Items.Add(new ExpenseItem
            {
                ExpenseDate = item.ExpenseDate,
                Category = item.Category,
                Merchant = item.Merchant,
                Amount = item.Amount,
                Currency = item.Currency,
                Description = item.Description,
                ReceiptUrl = item.ReceiptUrl
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(claim.Id, "RESUBMITTED", employeeId, "Employee",
            oldStatus, ClaimStatus.SUBMITTED.ToString(), $"Claim resubmitted with updates. Notes: {dto.ResubmissionNotes}", correlationId, cancellationToken);

        // Re-execute pipeline
        await RunValidationAndRiskAssessmentPipelineAsync(claim.Id, correlationId, cancellationToken);

        return (await GetClaimDetailsAsync(claim.Id, cancellationToken))!;
    }

    public async Task<ExpenseClaimDetailDto> ApproveClaimAsync(Guid claimId, Guid managerId, string comment, CancellationToken cancellationToken = default)
    {
        var claim = await _context.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        var manager = await _context.Users.FirstOrDefaultAsync(u => u.Id == managerId, cancellationToken);
        if (manager == null || manager.Role != UserRole.Manager && manager.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("Only authorized managers can approve claims.");

        // Rule: Claim must be waiting for manager approval or submitted
        if (claim.Status != ClaimStatus.WAITING_FOR_MANAGER_APPROVAL && claim.Status != ClaimStatus.SUBMITTED)
            throw new InvalidOperationException($"Claim {claim.ClaimNumber} is in status {claim.Status} and cannot be approved.");

        string oldStatus = claim.Status.ToString();
        string correlationId = Guid.NewGuid().ToString();

        claim.Status = ClaimStatus.APPROVED;
        claim.UpdatedAt = DateTime.UtcNow;

        _context.ManagerReviews.Add(new ManagerReview
        {
            ExpenseClaimId = claimId,
            ManagerId = managerId,
            Decision = ReviewDecision.APPROVE,
            Comment = string.IsNullOrWhiteSpace(comment) ? "Approved by department manager." : comment,
            ReviewedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(claim.Id, "APPROVED", managerId, manager.Role.ToString(),
            oldStatus, ClaimStatus.APPROVED.ToString(), $"Claim approved. Note: {comment}", correlationId, cancellationToken);

        return (await GetClaimDetailsAsync(claim.Id, cancellationToken))!;
    }

    public async Task<ExpenseClaimDetailDto> RejectClaimAsync(Guid claimId, Guid managerId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A specific rejection reason must be provided by the manager.");

        var claim = await _context.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        var manager = await _context.Users.FirstOrDefaultAsync(u => u.Id == managerId, cancellationToken);
        if (manager == null || manager.Role != UserRole.Manager && manager.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("Only authorized managers can reject claims.");

        string oldStatus = claim.Status.ToString();
        string correlationId = Guid.NewGuid().ToString();

        claim.Status = ClaimStatus.REJECTED;
        claim.LatestRejectionReason = reason;
        claim.UpdatedAt = DateTime.UtcNow;

        _context.ManagerReviews.Add(new ManagerReview
        {
            ExpenseClaimId = claimId,
            ManagerId = managerId,
            Decision = ReviewDecision.REJECT,
            Comment = reason,
            ReviewedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(claim.Id, "REJECTED", managerId, manager.Role.ToString(),
            oldStatus, ClaimStatus.REJECTED.ToString(), $"Claim rejected. Reason: {reason}", correlationId, cancellationToken);

        return (await GetClaimDetailsAsync(claim.Id, cancellationToken))!;
    }

    public async Task<ExpenseClaimDetailDto> RequestRevisionAsync(Guid claimId, Guid managerId, string comment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("A detailed revision request comment must be provided by the manager.");

        var claim = await _context.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        var manager = await _context.Users.FirstOrDefaultAsync(u => u.Id == managerId, cancellationToken);
        if (manager == null || manager.Role != UserRole.Manager && manager.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("Only authorized managers can request revisions.");

        string oldStatus = claim.Status.ToString();
        string correlationId = Guid.NewGuid().ToString();

        claim.Status = ClaimStatus.REVISION_REQUIRED;
        claim.LatestRevisionComment = comment;
        claim.UpdatedAt = DateTime.UtcNow;

        _context.ManagerReviews.Add(new ManagerReview
        {
            ExpenseClaimId = claimId,
            ManagerId = managerId,
            Decision = ReviewDecision.REQUEST_REVISION,
            Comment = comment,
            ReviewedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(claim.Id, "REVISION_REQUESTED", managerId, manager.Role.ToString(),
            oldStatus, ClaimStatus.REVISION_REQUIRED.ToString(), $"Revision requested. Feedback: {comment}", correlationId, cancellationToken);

        return (await GetClaimDetailsAsync(claim.Id, cancellationToken))!;
    }

    public async Task<ExpenseClaimDetailDto> ReimburseClaimAsync(Guid claimId, Guid financeUserId, string notes, CancellationToken cancellationToken = default)
    {
        var claim = await _context.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        var financeUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == financeUserId, cancellationToken);
        if (financeUser == null || (financeUser.Role != UserRole.Finance && financeUser.Role != UserRole.Admin))
            throw new UnauthorizedAccessException("Only finance personnel can record reimbursements.");

        if (claim.Status != ClaimStatus.APPROVED)
            throw new InvalidOperationException($"Claim must be in APPROVED state prior to reimbursement. Current: {claim.Status}");

        string oldStatus = claim.Status.ToString();
        string correlationId = Guid.NewGuid().ToString();

        claim.Status = ClaimStatus.REIMBURSED;
        claim.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(claim.Id, "REIMBURSED", financeUserId, financeUser.Role.ToString(),
            oldStatus, ClaimStatus.REIMBURSED.ToString(), $"Reimbursement processed by Finance. Notes: {notes}", correlationId, cancellationToken);

        return (await GetClaimDetailsAsync(claim.Id, cancellationToken))!;
    }

    private async Task RunValidationAndRiskAssessmentPipelineAsync(Guid claimId, string correlationId, CancellationToken cancellationToken)
    {
        var claim = await _context.ExpenseClaims
            .Include(c => c.Items)
            .Include(c => c.Department)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);

        if (claim == null) return;

        // --- STEP 1: DETERMINISTIC POLICY VALIDATION ---
        claim.Status = ClaimStatus.POLICY_VALIDATING;
        await _context.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(claim.Id, "POLICY_CHECK_STARTED", null, "System",
            ClaimStatus.SUBMITTED.ToString(), ClaimStatus.POLICY_VALIDATING.ToString(), "Deterministic policy validation engine started.", correlationId, cancellationToken);

        // Fetch active policies
        var activePolicies = await _context.ExpensePolicies
            .AsNoTracking()
            .Where(p => p.Active)
            .ToListAsync(cancellationToken);

        // Calculate department monthly spend so far
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        decimal deptSpendThisMonth = await _context.ExpenseClaims
            .Where(c => c.DepartmentId == claim.DepartmentId && c.Id != claim.Id && c.ClaimDate >= startOfMonth && c.Status != ClaimStatus.REJECTED)
            .SumAsync(c => c.TotalAmount, cancellationToken);

        decimal deptBudget = claim.Department?.MonthlyBudgetLimit ?? 0m;

        // Clear previous violations if re-validating
        var existingViolations = await _context.PolicyViolations.Where(v => v.ExpenseClaimId == claimId).ToListAsync(cancellationToken);
        if (existingViolations.Any())
        {
            _context.PolicyViolations.RemoveRange(existingViolations);
        }

        var validationResult = _policyEngine.ValidateClaim(claim, activePolicies, deptSpendThisMonth, deptBudget);

        if (validationResult.Violations.Any())
        {
            claim.PolicyStatus = PolicyStatus.VIOLATIONS_FOUND;
            foreach (var v in validationResult.Violations)
            {
                _context.PolicyViolations.Add(v);
            }
            await _context.SaveChangesAsync(cancellationToken);

            await AddAuditLogAsync(claim.Id, "POLICY_VIOLATION_FOUND", null, "System",
                "", "", $"Detected {validationResult.Violations.Count} policy violation(s): {string.Join("; ", validationResult.Violations.Select(v => v.RuleCode))}.", correlationId, cancellationToken);
        }
        else
        {
            claim.PolicyStatus = PolicyStatus.COMPLIANT;
            await _context.SaveChangesAsync(cancellationToken);
        }

        // --- STEP 2: FRAUD/ANOMALY-RISK AGENT SUBSYSTEM ---
        claim.Status = ClaimStatus.RISK_ASSESSING;
        await _context.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(claim.Id, "RISK_CHECK_STARTED", null, "FraudAnomalyRiskAgent",
            ClaimStatus.POLICY_VALIDATING.ToString(), ClaimStatus.RISK_ASSESSING.ToString(), "Multi-agent anomaly, duplicate, and spending velocity analysis started.", correlationId, cancellationToken);

        var agentExecution = new AgentExecution
        {
            WorkflowId = correlationId,
            AgentName = "FraudAnomalyRiskAgent",
            InputReference = $"ClaimId: {claim.Id}, Amount: {claim.TotalAmount}, Merchant: {claim.MerchantName}",
            Status = AgentExecutionStatus.STARTED,
            StartedAt = DateTime.UtcNow
        };
        _context.AgentExecutions.Add(agentExecution);
        await _context.SaveChangesAsync(cancellationToken);

        AgentRiskAssessmentOutput riskAssessmentOutput;
        try
        {
            riskAssessmentOutput = await _riskAgent.AnalyzeClaimAsync(claim.Id, cancellationToken);

            agentExecution.Status = riskAssessmentOutput.Signals.Any(s => s.Type == "SAFE_FALLBACK")
                ? AgentExecutionStatus.SAFE_FALLBACK
                : AgentExecutionStatus.SUCCESS;
            agentExecution.OutputReference = $"Score: {riskAssessmentOutput.RiskScore}, Level: {riskAssessmentOutput.RiskLevel}, Signals: {riskAssessmentOutput.Signals.Count}";
            agentExecution.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            agentExecution.Status = AgentExecutionStatus.FAILED;
            agentExecution.ErrorMessage = ex.Message;
            agentExecution.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            // Trigger fallback
            riskAssessmentOutput = new AgentRiskAssessmentOutput
            {
                RiskLevel = "REVIEW_REQUIRED",
                RiskScore = 90,
                ReasonSummary = "Workflow engine caught unhandled agent exception; safe-failure engaged.",
                Signals = new List<AgentRiskSignal>
                {
                    new() { Type = "SAFE_FALLBACK", Severity = "CRITICAL", Evidence = ex.Message }
                }
            };
        }

        await AddAuditLogAsync(claim.Id, "RISK_ASSESSMENT_CREATED", null, "FraudAnomalyRiskAgent",
            "", "", $"Risk Assessment completed: {riskAssessmentOutput.RiskLevel} (Score: {riskAssessmentOutput.RiskScore}/100). Summary: {riskAssessmentOutput.ReasonSummary}", correlationId, cancellationToken);

        // --- STEP 3: HUMAN APPROVAL PAUSE (Section 12) ---
        // Workflow halts at WAITING_FOR_MANAGER_APPROVAL if:
        // - Policy violations found
        // - High risk or Review required
        // - Policy specifies manager approval required
        bool pauseForManager = claim.PolicyStatus == PolicyStatus.VIOLATIONS_FOUND ||
                               riskAssessmentOutput.RiskLevel == "HIGH_RISK" ||
                               riskAssessmentOutput.RiskLevel == "REVIEW_REQUIRED" ||
                               validationResult.RequiresManagerApproval ||
                               claim.TotalAmount > 5000m; // standard threshold

        if (pauseForManager)
        {
            claim.Status = ClaimStatus.WAITING_FOR_MANAGER_APPROVAL;
            claim.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await AddAuditLogAsync(claim.Id, "APPROVAL_REQUESTED", null, "WorkflowEngine",
                ClaimStatus.RISK_ASSESSING.ToString(), ClaimStatus.WAITING_FOR_MANAGER_APPROVAL.ToString(),
                "Human approval pause triggered. Placed into Manager Review Queue awaiting managerial decision.", correlationId, cancellationToken);
        }
        else
        {
            // Low risk, fully compliant claim can automatically advance to APPROVED if company rules allow
            claim.Status = ClaimStatus.APPROVED;
            claim.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await AddAuditLogAsync(claim.Id, "APPROVED", null, "WorkflowEngine",
                ClaimStatus.RISK_ASSESSING.ToString(), ClaimStatus.APPROVED.ToString(),
                "Auto-approved compliant low-risk claim under policy threshold.", correlationId, cancellationToken);
        }
    }

    private async Task AddAuditLogAsync(Guid claimId, string action, Guid? actorId, string actorRole,
        string oldStatus, string newStatus, string details, string correlationId, CancellationToken cancellationToken)
    {
        var log = new AuditLog
        {
            EntityType = "ExpenseClaim",
            EntityId = claimId,
            Action = action,
            ActorUserId = actorId,
            ActorRole = actorRole,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Details = details,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        };
        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ExpenseClaimListDto>> GetReviewQueueAsync(ReviewQueueFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _context.ExpenseClaims
            .AsNoTracking()
            .Include(c => c.Employee)
            .Include(c => c.Department)
            .Include(c => c.Violations)
            .Include(c => c.DuplicateMatches)
            .Include(c => c.RiskAssessments)
            .AsQueryable();

        // Default: Show pending approval or revision claims if not filtered
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            if (Enum.TryParse<ClaimStatus>(filter.Status, true, out var cStatus))
                query = query.Where(c => c.Status == cStatus);
        }
        else
        {
            query = query.Where(c => c.Status == ClaimStatus.WAITING_FOR_MANAGER_APPROVAL || c.Status == ClaimStatus.REVISION_REQUIRED);
        }

        if (!string.IsNullOrWhiteSpace(filter.RiskLevel))
        {
            if (Enum.TryParse<RiskStatus>(filter.RiskLevel, true, out var rStatus))
                query = query.Where(c => c.RiskStatus == rStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.PolicyStatus))
        {
            if (Enum.TryParse<PolicyStatus>(filter.PolicyStatus, true, out var pStatus))
                query = query.Where(c => c.PolicyStatus == pStatus);
        }

        if (filter.DepartmentId.HasValue && filter.DepartmentId.Value != Guid.Empty)
        {
            query = query.Where(c => c.DepartmentId == filter.DepartmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(c => c.Category.ToLower() == filter.Category.ToLower());
        }

        if (filter.MinAmount.HasValue)
        {
            query = query.Where(c => c.TotalAmount >= filter.MinAmount.Value);
        }

        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(c => c.TotalAmount <= filter.MaxAmount.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.ToLower();
            query = query.Where(c => c.ClaimNumber.ToLower().Contains(search) ||
                                     c.MerchantName.ToLower().Contains(search) ||
                                     (c.Employee != null && c.Employee.FullName.ToLower().Contains(search)));
        }

        var paged = await query
            .OrderByDescending(c => c.ClaimDate)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return paged.Select(c => new ExpenseClaimListDto
        {
            Id = c.Id,
            ClaimNumber = c.ClaimNumber,
            EmployeeId = c.EmployeeId,
            EmployeeName = c.Employee?.FullName ?? "Unknown",
            DepartmentId = c.DepartmentId,
            DepartmentName = c.Department?.Name ?? "General",
            ClaimDate = c.ClaimDate,
            SubmittedAt = c.SubmittedAt,
            TotalAmount = c.TotalAmount,
            Currency = c.Currency,
            MerchantName = c.MerchantName,
            Category = c.Category,
            Status = c.Status.ToString(),
            PolicyStatus = c.PolicyStatus.ToString(),
            RiskStatus = c.RiskStatus.ToString(),
            RiskScore = c.RiskAssessments.OrderByDescending(r => r.CreatedAt).FirstOrDefault()?.RiskScore,
            ViolationCount = c.Violations.Count,
            DuplicateCount = c.DuplicateMatches.Count
        }).ToList();
    }

    public async Task<ExpenseClaimDetailDto?> GetClaimDetailsAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var claim = await _context.ExpenseClaims
            .AsNoTracking()
            .Include(c => c.Employee)
            .Include(c => c.Department)
            .Include(c => c.Items)
            .Include(c => c.Violations)
            .Include(c => c.RiskAssessments)
            .Include(c => c.DuplicateMatches).ThenInclude(d => d.MatchingClaim)
            .Include(c => c.Reviews).ThenInclude(r => r.Manager)
            .Include(c => c.ComplianceChecks)
            .Include(c => c.AuditLogs)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);

        if (claim == null) return null;

        var latestRisk = claim.RiskAssessments.OrderByDescending(r => r.CreatedAt).FirstOrDefault();
        List<RiskSignalDto> signals = new();
        if (latestRisk != null && !string.IsNullOrWhiteSpace(latestRisk.SignalsJson))
        {
            try
            {
                signals = JsonSerializer.Deserialize<List<RiskSignalDto>>(latestRisk.SignalsJson) ?? new();
            }
            catch { }
        }

        var usersDict = await _context.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return new ExpenseClaimDetailDto
        {
            Id = claim.Id,
            ClaimNumber = claim.ClaimNumber,
            EmployeeId = claim.EmployeeId,
            EmployeeName = claim.Employee?.FullName ?? "Unknown",
            DepartmentId = claim.DepartmentId,
            DepartmentName = claim.Department?.Name ?? "General",
            ClaimDate = claim.ClaimDate,
            SubmittedAt = claim.SubmittedAt,
            TotalAmount = claim.TotalAmount,
            Currency = claim.Currency,
            MerchantName = claim.MerchantName,
            Category = claim.Category,
            Description = claim.Description,
            Status = claim.Status.ToString(),
            PolicyStatus = claim.PolicyStatus.ToString(),
            RiskStatus = claim.RiskStatus.ToString(),
            LatestRevisionComment = claim.LatestRevisionComment,
            LatestRejectionReason = claim.LatestRejectionReason,
            RiskScore = latestRisk?.RiskScore,
            ViolationCount = claim.Violations.Count,
            DuplicateCount = claim.DuplicateMatches.Count,

            Items = claim.Items.Select(i => new ExpenseItemDto
            {
                Id = i.Id,
                ExpenseDate = i.ExpenseDate,
                Category = i.Category,
                Merchant = i.Merchant,
                Amount = i.Amount,
                Currency = i.Currency,
                Description = i.Description,
                ReceiptUrl = i.ReceiptUrl
            }).ToList(),

            Violations = claim.Violations.Select(v => new PolicyViolationDto
            {
                Id = v.Id,
                RuleCode = v.RuleCode,
                Severity = v.Severity.ToString(),
                Message = v.Message,
                ActualValue = v.ActualValue,
                AllowedValue = v.AllowedValue,
                CreatedAt = v.CreatedAt
            }).ToList(),

            RiskAssessment = latestRisk == null ? null : new RiskAssessmentDto
            {
                Id = latestRisk.Id,
                RiskScore = latestRisk.RiskScore,
                RiskLevel = latestRisk.RiskLevel.ToString(),
                DuplicateDetected = latestRisk.DuplicateDetected,
                UnusualAmountDetected = latestRisk.UnusualAmountDetected,
                SuspiciousPatternDetected = latestRisk.SuspiciousPatternDetected,
                ReasonSummary = latestRisk.ReasonSummary,
                AgentVersion = latestRisk.AgentVersion,
                CreatedAt = latestRisk.CreatedAt,
                Signals = signals
            },

            DuplicateMatches = claim.DuplicateMatches.Select(d => new DuplicateMatchDto
            {
                Id = d.Id,
                MatchingClaimId = d.MatchingClaimId,
                MatchingClaimNumber = d.MatchingClaim?.ClaimNumber ?? "Prior Claim",
                SimilarityScore = d.SimilarityScore,
                MatchType = d.MatchType.ToString(),
                Evidence = d.Evidence,
                MatchingAmount = d.MatchingClaim?.TotalAmount ?? 0m,
                MatchingClaimDate = d.MatchingClaim?.ClaimDate ?? DateTime.UtcNow,
                MatchingMerchant = d.MatchingClaim?.MerchantName ?? ""
            }).ToList(),

            Reviews = claim.Reviews.OrderByDescending(r => r.ReviewedAt).Select(r => new ManagerReviewDto
            {
                Id = r.Id,
                ManagerId = r.ManagerId,
                ManagerName = r.Manager?.FullName ?? "Manager",
                Decision = r.Decision.ToString(),
                Comment = r.Comment,
                ReviewedAt = r.ReviewedAt
            }).ToList(),

            ComplianceChecks = claim.ComplianceChecks.OrderByDescending(c => c.CheckedAt).Select(c => new ComplianceCheckDto
            {
                Id = c.Id,
                CheckType = c.CheckType.ToString(),
                Result = c.Result.ToString(),
                Details = c.Details,
                CheckedAt = c.CheckedAt
            }).ToList(),

            AuditLogs = claim.AuditLogs.OrderByDescending(a => a.Timestamp).Select(a => new AuditLogDto
            {
                Id = a.Id,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Action = a.Action,
                ActorUserId = a.ActorUserId,
                ActorName = a.ActorUserId.HasValue && usersDict.TryGetValue(a.ActorUserId.Value, out var name) ? name : (a.ActorRole ?? "System"),
                ActorRole = a.ActorRole,
                OldStatus = a.OldStatus,
                NewStatus = a.NewStatus,
                Details = a.Details,
                CorrelationId = a.CorrelationId,
                Timestamp = a.Timestamp
            }).ToList()
        };
    }

    public async Task<List<ExpenseClaimListDto>> GetEmployeeClaimsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var claims = await _context.ExpenseClaims
            .AsNoTracking()
            .Include(c => c.Department)
            .Include(c => c.Violations)
            .Include(c => c.DuplicateMatches)
            .Include(c => c.RiskAssessments)
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.ClaimDate)
            .ToListAsync(cancellationToken);

        return claims.Select(c => new ExpenseClaimListDto
        {
            Id = c.Id,
            ClaimNumber = c.ClaimNumber,
            EmployeeId = c.EmployeeId,
            EmployeeName = "You",
            DepartmentId = c.DepartmentId,
            DepartmentName = c.Department?.Name ?? "General",
            ClaimDate = c.ClaimDate,
            SubmittedAt = c.SubmittedAt,
            TotalAmount = c.TotalAmount,
            Currency = c.Currency,
            MerchantName = c.MerchantName,
            Category = c.Category,
            Status = c.Status.ToString(),
            PolicyStatus = c.PolicyStatus.ToString(),
            RiskStatus = c.RiskStatus.ToString(),
            RiskScore = c.RiskAssessments.OrderByDescending(r => r.CreatedAt).FirstOrDefault()?.RiskScore,
            ViolationCount = c.Violations.Count,
            DuplicateCount = c.DuplicateMatches.Count
        }).ToList();
    }

    public async Task<ComplianceDashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        int pending = await _context.ExpenseClaims.CountAsync(c => c.Status == ClaimStatus.WAITING_FOR_MANAGER_APPROVAL, cancellationToken);
        int highRisk = await _context.ExpenseClaims.CountAsync(c => c.RiskStatus == RiskStatus.HIGH_RISK || c.RiskStatus == RiskStatus.REVIEW_REQUIRED, cancellationToken);
        int violations = await _context.ExpenseClaims.CountAsync(c => c.PolicyStatus == PolicyStatus.VIOLATIONS_FOUND, cancellationToken);
        int awaitingRevision = await _context.ExpenseClaims.CountAsync(c => c.Status == ClaimStatus.REVISION_REQUIRED, cancellationToken);
        int approved = await _context.ExpenseClaims.CountAsync(c => c.Status == ClaimStatus.APPROVED || c.Status == ClaimStatus.REIMBURSED, cancellationToken);
        int rejected = await _context.ExpenseClaims.CountAsync(c => c.Status == ClaimStatus.REJECTED, cancellationToken);

        decimal pendingAmount = await _context.ExpenseClaims
            .Where(c => c.Status == ClaimStatus.WAITING_FOR_MANAGER_APPROVAL)
            .SumAsync(c => c.TotalAmount, cancellationToken);

        var recentHighRisk = await GetReviewQueueAsync(new ReviewQueueFilterDto { RiskLevel = "HIGH_RISK", PageSize = 5 }, cancellationToken);

        var recentActivities = await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Action = a.Action,
                ActorUserId = a.ActorUserId,
                ActorRole = a.ActorRole,
                OldStatus = a.OldStatus,
                NewStatus = a.NewStatus,
                Details = a.Details,
                CorrelationId = a.CorrelationId,
                Timestamp = a.Timestamp
            })
            .ToListAsync(cancellationToken);

        return new ComplianceDashboardStatsDto
        {
            TotalPendingReviews = pending,
            HighRiskClaims = highRisk,
            PolicyViolationsCount = violations,
            AwaitingRevisionCount = awaitingRevision,
            TotalApproved = approved,
            TotalRejected = rejected,
            TotalPendingAmount = pendingAmount,
            RecentHighRiskClaims = recentHighRisk,
            RecentActivities = recentActivities
        };
    }
}
