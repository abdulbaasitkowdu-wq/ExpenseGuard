import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { decidePurchaseRequest, getPurchaseRequest } from '../services/api';
import { AmountDisplay, ApprovalProgress, ErrorState, LoadingSpinner, StatusBadge, apiErrorMessage, formatDesignation, stageLabel } from '../components/Shared';

export default function PurchaseRequestReview() {
  const { id } = useParams();
  const navigate = useNavigate();
  const client = useQueryClient();
  const [comment, setComment] = useState('');
  const [message, setMessage] = useState(null);
  const request = useQuery({ queryKey: ['purchase-request', id], queryFn: () => getPurchaseRequest(id) });
  const action = useMutation({
    mutationFn: ({ type }) => decidePurchaseRequest(id, type, comment),
    onSuccess: (_, variables) => {
      setMessage({ type: 'success', text: `${variables.type} recorded.` });
      setComment('');
      client.invalidateQueries({ queryKey: ['purchase-request', id] });
      client.invalidateQueries({ queryKey: ['purchase-request-queue'] });
    },
    onError: error => setMessage({ type: 'danger', text: apiErrorMessage(error) }),
  });

  if (request.isPending) return <div className="page-content"><LoadingSpinner /></div>;
  if (request.isError) return <div className="page-content"><ErrorState error={request.error} onRetry={request.refetch} /></div>;
  const value = request.data;
  const review = value.review;
  const pending = value.status === 'Submitted' || value.status === 1;

  return (
    <div className="page-content">
      <button className="btn btn-ghost btn-sm" type="button" onClick={() => navigate(-1)}>← Back</button>
      <div className="employee-heading">
        <div>
          <h2>Purchase request #{value.purchaseRequestId}</h2>
          <p>{value.fullName} · {formatDesignation(value)}</p>
          <ApprovalProgress steps={value.approvalSteps} currentRole={value.currentRequiredRole} />
        </div>
        <StatusBadge status={enumLabel(value.status)} />
      </div>
      {message && <div className={`alert alert-${message.type}`}>{message.text}</div>}
      <div className="review-grid">
        <article className="card">
          <h3>Request</h3>
          <Detail label="Department" value={value.departmentName || '—'} />
          <Detail label="Vendor" value={value.vendor || 'Not provided'} />
          <Detail label="Category" value={value.category || review?.category || 'Unknown'} />
          <Detail label="Amount" value={<AmountDisplay amount={value.estimatedAmount} currency={value.currency} />} />
          <p className="employee-secondary">{value.description}</p>
        </article>
        <article className={`card review-summary ${review?.hasFlags ? 'flagged' : ''}`}>
          <h3>AI review summary</h3>
          <p>{review?.summary || 'Review findings are not available yet.'}</p>
        </article>
      </div>
      <div className="review-grid">
        <Section title="Policy" section={review?.policy} />
        <Section title="Fraud risk" section={review?.fraud} />
        <Section title="Budget" section={review?.budget} currency={value.currency} />
      </div>
      {pending && (
        <div className="card">
          <h3>Decision</h3>
          <p className="employee-secondary">Current stage: {stageLabel(value.currentRequiredRole)}</p>
          <label className="form-label" htmlFor="pr-comment">Comment</label>
          <textarea id="pr-comment" className="form-control" rows={3} value={comment} onChange={e => setComment(e.target.value)} />
          <div className="decision-actions">
            <button className="btn btn-success" disabled={action.isPending} onClick={() => action.mutate({ type: 'approve' })}>Approve</button>
            <button className="btn btn-danger" disabled={action.isPending || !comment.trim()} onClick={() => action.mutate({ type: 'reject' })}>Reject</button>
            <button className="btn btn-ghost" disabled={action.isPending || !comment.trim()} onClick={() => action.mutate({ type: 'revise' })}>Request revision</button>
          </div>
        </div>
      )}
      <p><Link to="/approvals">Return to approval queue</Link></p>
    </div>
  );
}

function Section({ title, section, currency }) {
  if (!section) return <article className="card"><h3>{title}</h3><p className="employee-secondary">No finding.</p></article>;
  return (
    <article className={`card review-section ${section.outcome}`}>
      <h3>{title}</h3>
      <StatusBadge status={section.outcome} />
      <p>{section.summary}</p>
      {section.requested != null && (
        <p className="employee-secondary">Requested {section.requested.toLocaleString()} {currency} · remaining {section.available?.toLocaleString() ?? '—'}</p>
      )}
      {section.flags?.length > 0 && (
        <ul className="review-flags">{section.flags.map(flag => (
          <li key={flag.code}><strong>{flag.severity}</strong> {flag.message}</li>
        ))}</ul>
      )}
    </article>
  );
}

function Detail({ label, value }) {
  return <div className="review-detail"><span>{label}</span><strong>{value}</strong></div>;
}

function enumLabel(status) {
  const names = ['Draft', 'Submitted', 'Approved', 'Rejected', 'Cancelled'];
  return typeof status === 'number' ? names[status] : status;
}
