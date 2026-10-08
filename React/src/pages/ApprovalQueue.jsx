import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { getApprovalQueue, getPurchaseRequestApprovalQueue } from '../services/api';
import { AmountDisplay, ApprovalProgress, EmptyState, ErrorState, LoadingSpinner, StatusBadge } from '../components/Shared';

export default function ApprovalQueue() {
  const claims = useQuery({
    queryKey: ['approval-queue'],
    queryFn: async () => (await getApprovalQueue()).data,
  });
  const requests = useQuery({
    queryKey: ['purchase-request-queue'],
    queryFn: getPurchaseRequestApprovalQueue,
  });
  const claimRows = claims.data ?? [];
  const requestRows = requests.data ?? [];
  const loading = claims.isPending || requests.isPending;
  const error = claims.error || requests.error;
  const empty = !loading && !error && claimRows.length === 0 && requestRows.length === 0;

  return <div className="page-content">
    <div className="page-heading"><h2>My approval queue</h2>
      <button className="btn btn-ghost btn-sm" onClick={() => { claims.refetch(); requests.refetch(); }}
        disabled={claims.isFetching || requests.isFetching}>Refresh</button></div>
    {loading && <LoadingSpinner />}
    {error && <ErrorState error={error} onRetry={() => { claims.refetch(); requests.refetch(); }} />}
    {empty && <EmptyState message="Nothing awaits your approval." />}
    {requestRows.length > 0 && <div className="table-wrapper">
      <div className="table-header"><h3>Purchase requests</h3></div>
      <table>
        <thead><tr><th>Request</th><th>Employee</th><th>Amount</th><th>Stage</th><th>AI review</th><th>Action</th></tr></thead>
        <tbody>{requestRows.map(item => <tr key={`pr-${item.purchaseRequestId}`}>
          <td>#{item.purchaseRequestId} · {item.category || item.description}</td>
          <td>{item.fullName}<small className="employee-secondary">{item.departmentName}</small></td>
          <td><AmountDisplay amount={item.estimatedAmount} currency={item.currency} /></td>
          <td><ApprovalProgress compact steps={item.approvalSteps} currentRole={item.currentRequiredRole} /></td>
          <td>{item.review?.hasFlags ? <StatusBadge status="flagged" /> : <StatusBadge status="clear" />}</td>
          <td><Link className="btn btn-primary btn-sm" to={`/approvals/requests/${item.purchaseRequestId}`}>Review</Link></td>
        </tr>)}</tbody>
      </table>
    </div>}
    {claimRows.length > 0 && <div className="table-wrapper">
      <div className="table-header"><h3>Reimbursements</h3></div>
      <table>
        <thead><tr><th>Claim</th><th>Employee</th><th>Amount</th><th>Status</th><th>Action</th></tr></thead>
        <tbody>{claimRows.map(item => <tr key={`reim-${item.id}`}>
          <td>{item.expenseClaimId}</td><td>{item.employeeId}</td>
          <td><AmountDisplay amount={item.amount} currency={item.currency} /></td>
          <td><StatusBadge status={item.status} /></td>
          <td><Link className="btn btn-primary btn-sm" to={`/approvals/${item.id}`}>Review</Link></td>
        </tr>)}</tbody>
      </table>
    </div>}
  </div>;
}
