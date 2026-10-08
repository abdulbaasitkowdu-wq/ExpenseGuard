using System.Text.Json;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Services;

public interface IWorkflowLedger
{
    Task<WorkflowExecution> StartAsync(int claimId, string objective, string correlationId, CancellationToken ct);
    Task<WorkflowExecution> StartPurchaseRequestAsync(int purchaseRequestId, string objective, string correlationId, CancellationToken ct);
    Task CompleteStepAsync(Guid executionId, int sequence, string name, string type, string status, object? output, CancellationToken ct);
    Task SetStatusAsync(int claimId, string status, CancellationToken ct);
    Task SetPurchaseRequestStatusAsync(int purchaseRequestId, string status, CancellationToken ct);
    Task AuditAsync(int? employeeId, string action, string entityType, string entityId, object? data, string correlationId, CancellationToken ct);
}

public sealed class WorkflowLedger(AppDbContext db) : IWorkflowLedger
{
    public Task<WorkflowExecution> StartAsync(int claimId, string objective, string correlationId, CancellationToken ct) =>
        StartCoreAsync(claimId, null, WorkflowSubjects.Claim, objective, correlationId, ct);

    public Task<WorkflowExecution> StartPurchaseRequestAsync(int purchaseRequestId, string objective, string correlationId, CancellationToken ct) =>
        StartCoreAsync(null, purchaseRequestId, WorkflowSubjects.PurchaseRequest, objective, correlationId, ct);

    public async Task CompleteStepAsync(Guid executionId, int sequence, string name, string type, string status, object? output, CancellationToken ct)
    {
        db.WorkflowSteps.Add(new WorkflowStep
        {
            WorkflowExecutionId = executionId,
            Sequence = sequence,
            Name = name,
            Type = type,
            Status = status,
            OutputSnapshotJson = output is null ? null : JsonSerializer.Serialize(output)
        });
        await db.SaveChangesAsync(ct);
    }

    public Task SetStatusAsync(int claimId, string status, CancellationToken ct) =>
        SetStatusCoreAsync(claimId, null, status, ct);

    public Task SetPurchaseRequestStatusAsync(int purchaseRequestId, string status, CancellationToken ct) =>
        SetStatusCoreAsync(null, purchaseRequestId, status, ct);

    public async Task AuditAsync(int? employeeId, string action, string entityType, string entityId, object? data, string correlationId, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            EmployeeId = employeeId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DataJson = data is null ? null : JsonSerializer.Serialize(data),
            CorrelationId = correlationId
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<WorkflowExecution> StartCoreAsync(int? claimId, int? purchaseRequestId, string subjectType,
        string objective, string correlationId, CancellationToken ct)
    {
        var existing = await db.WorkflowExecutions
            .Include(w => w.Steps)
            .SingleOrDefaultAsync(w => claimId != null
                ? w.ExpenseClaimId == claimId
                : w.PurchaseRequestId == purchaseRequestId, ct);
        if (existing is not null)
        {
            if (existing.Steps.Count > 0)
            {
                db.WorkflowSteps.RemoveRange(existing.Steps);
                existing.Steps.Clear();
            }
            existing.Objective = objective;
            existing.Status = "CREATED";
            existing.CorrelationId = correlationId;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var execution = new WorkflowExecution
        {
            ExpenseClaimId = claimId,
            PurchaseRequestId = purchaseRequestId,
            SubjectType = subjectType,
            Objective = objective,
            Status = "CREATED",
            CorrelationId = correlationId
        };
        db.WorkflowExecutions.Add(execution);
        await db.SaveChangesAsync(ct);
        return execution;
    }

    private async Task SetStatusCoreAsync(int? claimId, int? purchaseRequestId, string status, CancellationToken ct)
    {
        var execution = await db.WorkflowExecutions.SingleOrDefaultAsync(w => claimId != null
            ? w.ExpenseClaimId == claimId
            : w.PurchaseRequestId == purchaseRequestId, ct);
        if (execution is null) return;
        execution.Status = status;
        execution.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
