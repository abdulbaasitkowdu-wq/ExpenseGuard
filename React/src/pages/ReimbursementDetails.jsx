import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useNavigate } from 'react-router-dom';
import { decideReimbursement, getReimbursement, processReimbursement, startApproval, submitPayment } from '../services/api';
import { ApprovalProgress, StatusBadge, LoadingSpinner, AmountDisplay, ErrorState, apiErrorMessage } from '../components/Shared';
import { useAuth } from '../auth/AuthContext';
import { FiArrowLeft, FiCreditCard, FiPlay } from 'react-icons/fi';

export default function ReimbursementDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const auth = useAuth();
  const queryClient = useQueryClient();
  const [actionMsg, setActionMsg] = useState(null);
  const [comment, setComment] = useState('');
  const [templateId, setTemplateId] = useState('');
  const reimbursement = useQuery({ queryKey: ['reimbursement', id], queryFn: async () => (await getReimbursement(id)).data });
  const action = useMutation({
    mutationFn: async ({ type }) => {
      if (type === 'process') return processReimbursement(id);
      if (type === 'pay') return submitPayment(id);
      if (type === 'start') return startApproval(id, Number(templateId));
      return decideReimbursement(id, type, comment);
    },
    onSuccess: (_, variables) => {
      setActionMsg({ type: 'success', text: `${variables.type.replaceAll('-', ' ')} completed.` });
      setComment('');
      queryClient.invalidateQueries({ queryKey: ['reimbursement', id] });
    },
    onError: error => setActionMsg({ type: 'danger', text: apiErrorMessage(error) }),
  });

  if (reimbursement.isPending && !reimbursement.data) return <div className="page-content"><LoadingSpinner /></div>;
  if (reimbursement.isError && !reimbursement.data) return <div className="page-content"><ErrorState error={reimbursement.error} onRetry={reimbursement.refetch} /></div>;
  const r = reimbursement.data;
  const canApprove = auth.hasRole('Manager', 'DepartmentHead', 'Finance', 'Admin');
  const canFinance = auth.hasRole('Finance', 'Admin');

  return (
    <div className="page-content">
      <button id="btn-back" className="btn btn-ghost btn-sm" onClick={() => navigate(-1)} style={{ marginBottom: 20 }}>
        <FiArrowLeft /> Back
      </button>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 24 }}>
        <div>
          <h2 style={{ fontSize: 22, fontWeight: 800 }}>Reimbursement Details</h2>
          <p style={{ color: 'var(--text-muted)', fontSize: 13, fontFamily: 'monospace' }}>{r.id}</p>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          {canFinance && r.status === 'APPROVED' && (
            <button id="btn-detail-process" className="btn btn-primary" disabled={action.isPending} onClick={() => action.mutate({ type: 'process' })}>
              <FiPlay /> Process
            </button>
          )}
          {canFinance && r.status === 'PROCESSING' && (
            <button id="btn-detail-pay" className="btn btn-success" disabled={action.isPending} onClick={() => action.mutate({ type: 'pay' })}>
              <FiCreditCard /> Submit Payment
            </button>
          )}
        </div>
      </div>

      {actionMsg && (
        <div className={`alert alert-${actionMsg.type}`} style={{ marginBottom: 16 }}>
          {actionMsg.text}
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 20 }}>
        {/* Reimbursement Info */}
        <div className="card">
          <h3 style={{ marginBottom: 16, fontSize: 15 }}>Reimbursement Information</h3>
          <DetailRow label="Employee ID" value={r.employeeId} />
          <DetailRow label="Department" value={r.departmentId} />
          <DetailRow label="Claim ID" value={<span style={{ fontFamily: 'monospace', fontSize: 12 }}>{r.expenseClaimId}</span>} />
          <DetailRow label="Amount" value={<AmountDisplay amount={r.amount} />} />
          <DetailRow label="Currency" value={r.currency} />
          <DetailRow label="Status" value={<StatusBadge status={r.status} />} />
          <div style={{ marginTop: 12 }}>
            <ApprovalProgress steps={r.approvalSteps} currentRole={r.currentRequiredRole} />
          </div>
          <DetailRow label="Payment ID" value={r.paymentId || '—'} />
        </div>

        {/* Payment Info */}
        <div className="card">
          <h3 style={{ marginBottom: 16, fontSize: 15 }}>Payment Information</h3>
          <DetailRow label="Payment Reference" value={r.paymentReference || '—'} />
          {r.status === 'PAID' && (
            <div className="alert alert-success" style={{ marginTop: 12 }}>
              ✅ Payment completed successfully
            </div>
          )}
          {r.status === 'BUDGET_REVIEW_REQUIRED' && (
            <div className="alert alert-warning" style={{ marginTop: 12 }}>
              ⚠️ Insufficient budget — requires Finance review
            </div>
          )}
        </div>
      </div>

      {canApprove && (
        <div className="card">
          <h3>Sequential approval</h3>
          {r.status !== 'PENDING_APPROVAL' && (
            <div className="form-group">
              <label className="form-label" htmlFor="template-id">Workflow template ID</label>
              <input id="template-id" inputMode="numeric" value={templateId} onChange={e => setTemplateId(e.target.value)} />
              <button className="btn btn-primary" disabled={action.isPending || !/^[1-9]\d*$/.test(templateId)}
                onClick={() => action.mutate({ type: 'start' })}>Start approval</button>
            </div>
          )}
          {r.status === 'PENDING_APPROVAL' && (
            <>
              <label className="form-label" htmlFor="decision-comment">Decision comment</label>
              <textarea id="decision-comment" value={comment} onChange={e => setComment(e.target.value)} rows={3} />
              <div className="decision-actions">
                <button className="btn btn-success" disabled={action.isPending} onClick={() => action.mutate({ type: 'approve' })}>Approve</button>
                <button className="btn btn-danger" disabled={action.isPending || !comment.trim()} onClick={() => action.mutate({ type: 'reject' })}>Reject</button>
                <button className="btn btn-ghost" disabled={action.isPending || !comment.trim()} onClick={() => action.mutate({ type: 'revise' })}>Request revision</button>
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
}

function DetailRow({ label, value }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid var(--border)', alignItems: 'center' }}>
      <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>{label}</span>
      <span style={{ fontSize: 14 }}>{value}</span>
    </div>
  );
}
