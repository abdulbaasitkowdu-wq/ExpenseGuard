import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { getFinanceQueue, processReimbursement, submitPayment } from '../services/api';
import { StatusBadge, LoadingSpinner, AmountDisplay, EmptyState, ErrorState, apiErrorMessage } from '../components/Shared';
import { FiRefreshCw, FiSearch, FiEye, FiPlay, FiCreditCard } from 'react-icons/fi';

const STATUS_OPTIONS = ['', 'APPROVED', 'READY_FOR_FINANCE', 'PROCESSING', 'PAYMENT_PENDING', 'PAID', 'PAYMENT_FAILED', 'ON_HOLD', 'BUDGET_REVIEW_REQUIRED'];

export default function FinanceQueue() {
  const navigate = useNavigate();
  const [status, setStatus] = useState('');
  const [toast, setToast] = useState(null);
  const queryClient = useQueryClient();
  const queue = useQuery({
    queryKey: ['finance-queue', status],
    queryFn: async () => (await getFinanceQueue(status ? { status } : undefined)).data,
  });
  const action = useMutation({
    mutationFn: ({ id, type }) => type === 'pay' ? submitPayment(id) : processReimbursement(id),
    onSuccess: (_, variables) => {
      setToast({ type: 'success', msg: variables.type === 'pay' ? 'Payment submitted.' : 'Reimbursement moved to processing.' });
      queryClient.invalidateQueries({ queryKey: ['finance-queue'] });
    },
    onError: error => setToast({ type: 'danger', msg: apiErrorMessage(error) }),
  });
  const items = queue.data ?? [];

  return (
    <div className="page-content">
      {toast && (
        <div className={`alert alert-${toast.type}`} style={{ position: 'fixed', top: 80, right: 24, zIndex: 1000, minWidth: 300 }}>
          {toast.msg}
        </div>
      )}

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <div>
          <h2 style={{ fontSize: 22, fontWeight: 800 }}>Finance Processing Queue</h2>
          <p style={{ color: 'var(--text-muted)', fontSize: 14 }}>{items.length} reimbursements</p>
        </div>
        <button id="btn-refresh-queue" className="btn btn-ghost btn-sm" onClick={() => queue.refetch()} disabled={queue.isFetching}>
          <FiRefreshCw /> Refresh
        </button>
      </div>

      <div className="table-wrapper">
        {/* Filter Bar */}
        <div className="filter-bar">
          <FiSearch aria-hidden="true" />
          <label htmlFor="status-filter">Status</label>
          <select
            id="status-filter"
            value={status}
            onChange={e => setStatus(e.target.value)}
          >
            {STATUS_OPTIONS.map(s => <option key={s} value={s}>{s || 'All Statuses'}</option>)}
          </select>
        </div>

        {/* Table */}
        {queue.isPending && <LoadingSpinner />}
        {queue.isError && <ErrorState error={queue.error} onRetry={queue.refetch} />}
        {!queue.isPending && !queue.isError && items.length === 0 && <EmptyState message="The queue is empty." />}
        {items.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Employee</th>
                <th>Department</th>
                <th>Claim ID</th>
                <th>Amount</th>
                <th>Status</th>
                <th>Requested</th>
                <th>Payment Ref</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map(item => (
                <tr key={item.id} id={`queue-row-${item.id}`}>
                  <td>{item.employeeId}</td>
                  <td>{item.departmentId}</td>
                  <td style={{ fontFamily: 'monospace', fontSize: 12, color: 'var(--text-muted)' }}>
                    {item.expenseClaimId?.substring(0, 8)}…
                  </td>
                  <td><AmountDisplay amount={item.amount} /></td>
                  <td><StatusBadge status={item.status} /></td>
                  <td style={{ color: 'var(--text-muted)', fontSize: 12 }}>
                    {item.requestedAt ? new Date(item.requestedAt).toLocaleDateString() : '—'}
                  </td>
                  <td style={{ fontFamily: 'monospace', fontSize: 12 }}>
                    {item.paymentReference || '—'}
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: 6 }}>
                      <button
                        id={`btn-view-${item.id}`}
                        className="btn btn-ghost btn-sm"
                        onClick={() => navigate(`/reimbursements/${item.id}`)}
                      ><FiEye /></button>
                      {(item.status === 'APPROVED' || item.status === 'READY_FOR_FINANCE') && (
                        <button
                          id={`btn-process-${item.id}`}
                          className="btn btn-primary btn-sm"
                          disabled={action.isPending}
                          onClick={() => action.mutate({ id: item.id, type: 'process' })}
                        ><FiPlay /> Process</button>
                      )}
                      {item.status === 'PROCESSING' && (
                        <button
                          id={`btn-pay-${item.id}`}
                          className="btn btn-success btn-sm"
                          disabled={action.isPending}
                          onClick={() => action.mutate({ id: item.id, type: 'pay' })}
                        ><FiCreditCard /> Pay</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
