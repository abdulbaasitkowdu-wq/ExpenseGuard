import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { getWorkflow, getWorkflowByClaim, submitApproval } from '../services/api';
import { StatusBadge, LoadingSpinner } from '../components/Shared';
import { FiCheckCircle, FiXCircle, FiAlertCircle, FiClock, FiLoader } from 'react-icons/fi';

const STEP_ICONS = {
  COMPLETED: <FiCheckCircle style={{ color: 'var(--accent-success)' }} />,
  FAILED: <FiXCircle style={{ color: 'var(--accent-danger)' }} />,
  WAITING_FOR_HUMAN: <FiAlertCircle style={{ color: 'var(--accent-warning)' }} />,
  IN_PROGRESS: <FiLoader style={{ color: 'var(--accent-primary)', animation: 'spin 1s linear infinite' }} />,
  PENDING: <FiClock style={{ color: 'var(--text-muted)' }} />,
};

export default function WorkflowMonitor({ embedded, workflow: propWorkflow }) {
  const { id } = useParams() || {};
  const [workflow, setWorkflow] = useState(propWorkflow || null);
  const [loading, setLoading] = useState(!propWorkflow);
  const [approval, setApproval] = useState({ comment: '', decision: 'APPROVED' });
  const [approvalMsg, setApprovalMsg] = useState(null);

  useEffect(() => {
    if (propWorkflow) { setWorkflow(propWorkflow); return; }
    if (!id) return;
    setLoading(true);
    getWorkflow(id)
      .then(r => setWorkflow(r.data))
      .catch(() => setWorkflow(getMockWorkflow()))
      .finally(() => setLoading(false));
  }, [id, propWorkflow]);

  const handleApproval = async () => {
    try {
      const r = await submitApproval(workflow.id, {
        decision: approval.decision,
        comment: approval.comment,
        approverId: 'MGR-001' // In real app, from JWT
      });
      setWorkflow(r.data);
      setApprovalMsg({ type: 'success', text: `Decision recorded: ${approval.decision}` });
    } catch (e) {
      setApprovalMsg({ type: 'danger', text: e.response?.data?.error || 'Failed to submit decision' });
    }
  };

  if (loading) return <LoadingSpinner />;
  const wf = workflow || getMockWorkflow();

  return (
    <div className={embedded ? '' : 'page-content'}>
      {!embedded && (
        <div style={{ marginBottom: 24 }}>
          <h2 style={{ fontSize: 22, fontWeight: 800 }}>Workflow Monitor</h2>
          <p style={{ color: 'var(--text-muted)', fontSize: 14 }}>ID: {wf.workflowId}</p>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: embedded ? '1fr' : '380px 1fr', gap: 20 }}>
        {/* Step Timeline */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
            <h3 style={{ fontSize: 15 }}>Execution Steps</h3>
            <StatusBadge status={wf.status} />
          </div>

          <div style={{ display: 'flex', gap: 8, marginBottom: 16, fontSize: 12, color: 'var(--text-muted)' }}>
            <span>{wf.completedSteps}/{wf.totalSteps} steps complete</span>
            <span>•</span>
            <span>{wf.objective?.substring(0, 50)}…</span>
          </div>

          <div className="workflow-steps">
            {wf.steps?.map((step, i) => {
              const isLast = i === wf.steps.length - 1;
              const icon = STEP_ICONS[step.status] || STEP_ICONS.PENDING;
              return (
                <div key={step.id || i} className="workflow-step" id={`step-row-${step.stepNumber}`}>
                  {!isLast && (
                    <div className={`step-connector ${step.status === 'COMPLETED' ? 'completed' : step.status === 'IN_PROGRESS' ? 'in_progress' : ''}`} />
                  )}
                  <div className={`step-icon ${step.status?.toLowerCase()}`}>{icon}</div>
                  <div className="step-content">
                    <div className="step-name">{step.stepName?.replace(/_/g, ' ')}</div>
                    <div className="step-agent" style={{ fontSize: 11 }}>
                      {step.stepType === 'HUMAN_APPROVAL' ? '👤 Human Approval Required' : `🤖 ${step.agentName}`}
                    </div>
                    {step.startedAt && (
                      <div className="step-time">
                        {step.startedAt ? new Date(step.startedAt).toLocaleTimeString() : ''}
                        {step.completedAt ? ` → ${new Date(step.completedAt).toLocaleTimeString()}` : ''}
                      </div>
                    )}
                    {step.retryCount > 0 && (
                      <div style={{ fontSize: 11, color: 'var(--accent-warning)' }}>⟳ Retried {step.retryCount}x</div>
                    )}
                    {step.errorMessage && (
                      <div style={{ fontSize: 11, color: 'var(--accent-danger)', marginTop: 2 }}>✕ {step.errorMessage}</div>
                    )}
                    {step.validationResult && (
                      <div style={{ fontSize: 11, color: 'var(--accent-success)', marginTop: 2 }}>✓ {step.validationResult}</div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>

          {wf.finalOutcome && (
            <div className={`alert ${wf.status === 'COMPLETED' ? 'alert-success' : 'alert-danger'}`} style={{ marginTop: 16 }}>
              {wf.finalOutcome}
            </div>
          )}
        </div>

        {/* Right Panel */}
        {!embedded && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            {/* Workflow Summary */}
            <div className="card">
              <h3 style={{ fontSize: 15, marginBottom: 12 }}>Workflow Summary</h3>
              <WfRow label="Workflow ID" value={<code style={{ fontSize: 12 }}>{wf.workflowId}</code>} />
              <WfRow label="Claim ID" value={<code style={{ fontSize: 11 }}>{wf.expenseClaimId?.substring(0, 16)}…</code>} />
              <WfRow label="Status" value={<StatusBadge status={wf.status} />} />
              <WfRow label="Current Step" value={wf.currentStep?.replace(/_/g, ' ')} />
              <WfRow label="Approval" value={wf.approvalStatus || '—'} />
              <WfRow label="Created" value={new Date(wf.createdAt).toLocaleString()} />
              <WfRow label="Updated" value={new Date(wf.updatedAt).toLocaleString()} />
            </div>

            {/* Human Approval Panel */}
            {wf.status === 'WAITING_FOR_APPROVAL' && (
              <div className="card" style={{ borderColor: 'var(--accent-warning)' }}>
                <h3 style={{ fontSize: 15, marginBottom: 8, color: 'var(--accent-warning)' }}>⏸ Manager Approval Required</h3>
                <p style={{ fontSize: 13, color: 'var(--text-muted)', marginBottom: 16 }}>
                  The workflow is paused pending your decision. Review the claim and submit your approval.
                  Payment will NOT be processed until you approve.
                </p>

                {approvalMsg && (
                  <div className={`alert alert-${approvalMsg.type}`} style={{ marginBottom: 12 }}>
                    {approvalMsg.text}
                  </div>
                )}

                <div className="form-group">
                  <label className="form-label">Decision</label>
                  <select
                    id="approval-decision"
                    className="form-control"
                    value={approval.decision}
                    onChange={e => setApproval(a => ({ ...a, decision: e.target.value }))}
                  >
                    <option value="APPROVED">✅ Approve</option>
                    <option value="REJECTED">❌ Reject</option>
                    <option value="REVISION_REQUIRED">📝 Request Revision</option>
                  </select>
                </div>
                <div className="form-group">
                  <label className="form-label">Comment</label>
                  <textarea
                    id="approval-comment"
                    className="form-control"
                    rows={3}
                    placeholder="Add your comment..."
                    value={approval.comment}
                    onChange={e => setApproval(a => ({ ...a, comment: e.target.value }))}
                  />
                </div>
                <button id="btn-submit-approval" className="btn btn-primary" onClick={handleApproval}>
                  Submit Decision
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function WfRow({ label, value }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', padding: '7px 0', borderBottom: '1px solid var(--border)', alignItems: 'center' }}>
      <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>{label}</span>
      <span style={{ fontSize: 13 }}>{value}</span>
    </div>
  );
}

function getMockWorkflow() {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    workflowId: 'WF-10001',
    expenseClaimId: 'a1b2c3d4-1234-5678-abcd-ef1234567890',
    objective: 'Process expense reimbursement for Employee EMP-001 — Amount 25000 LKR',
    status: 'WAITING_FOR_APPROVAL',
    currentStep: 'HUMAN_APPROVAL',
    approvalStatus: 'PENDING',
    finalOutcome: null,
    totalSteps: 9, completedSteps: 3,
    createdAt: '2026-09-15T10:00:00Z',
    updatedAt: '2026-09-15T10:02:30Z',
    steps: [
      { id: '1', stepNumber: 1, stepName: 'INTAKE', agentName: 'ExpenseExtractionAgent', status: 'COMPLETED', stepType: 'AGENT', startedAt: '2026-09-15T10:00:10Z', completedAt: '2026-09-15T10:00:15Z', validationResult: 'VALID' },
      { id: '2', stepNumber: 2, stepName: 'POLICY_CHECK', agentName: 'PolicyComplianceAgent', status: 'COMPLETED', stepType: 'AGENT', startedAt: '2026-09-15T10:00:15Z', completedAt: '2026-09-15T10:00:22Z', validationResult: 'VALID' },
      { id: '3', stepNumber: 3, stepName: 'RISK_CHECK', agentName: 'FraudAnomalyRiskAgent', status: 'COMPLETED', stepType: 'AGENT', startedAt: '2026-09-15T10:00:22Z', completedAt: '2026-09-15T10:00:30Z', validationResult: 'VALID' },
      { id: '4', stepNumber: 4, stepName: 'HUMAN_APPROVAL', agentName: 'HUMAN_APPROVAL', status: 'WAITING_FOR_HUMAN', stepType: 'HUMAN_APPROVAL', startedAt: '2026-09-15T10:00:30Z' },
      { id: '5', stepNumber: 5, stepName: 'BUDGET_CHECK', agentName: 'BudgetAgent', status: 'PENDING', stepType: 'AGENT' },
      { id: '6', stepNumber: 6, stepName: 'FINANCE_PROCESSING', agentName: 'FinanceAgent', status: 'PENDING', stepType: 'AGENT' },
      { id: '7', stepNumber: 7, stepName: 'PAYMENT', agentName: 'PaymentSandbox', status: 'PENDING', stepType: 'TOOL' },
      { id: '8', stepNumber: 8, stepName: 'BUDGET_UPDATE', agentName: 'BudgetAgent', status: 'PENDING', stepType: 'AGENT' },
      { id: '9', stepNumber: 9, stepName: 'FINAL_RESULT', agentName: 'FinanceAgent', status: 'PENDING', stepType: 'AGENT' },
    ]
  };
}
