export function StatusBadge({ status }) {
  const cls = status?.toLowerCase().replace(/_/g, '_') || 'pending';
  return <span className={`badge ${cls}`}>{status?.replace(/_/g, ' ')}</span>;
}

export function LoadingSpinner() {
  return <div className="loading-container" role="status" aria-label="Loading"><div className="loading-spinner"/></div>;
}

export function apiErrorMessage(error, fallback = 'Something went wrong.') {
  const status = error?.response?.status;
  const url = `${error?.config?.url || ''} ${error?.request?.responseURL || ''}`;
  if (status === 401) {
    return /\/auth\/login\b/.test(url)
      ? 'Username or password is incorrect.'
      : 'Your session expired. Sign in again.';
  }
  if (status === 403) return 'You do not have permission to perform this action.';
  if (status === 409) return error.response?.data?.detail || error.response?.data?.error || 'The record changed. Refresh and try again.';
  return error?.response?.data?.detail || error?.response?.data?.error || fallback;
}

export function ErrorState({ error, onRetry }) {
  return (
    <div className="alert alert-danger" role="alert">
      <span>{apiErrorMessage(error)}</span>
      {onRetry && <button className="btn btn-ghost btn-sm" onClick={onRetry}>Retry</button>}
    </div>
  );
}

export function EmptyState({ message = 'No records found.' }) {
  return <div className="card empty-state">{message}</div>;
}

export function formatDesignation(employee) {
  const title = employee?.designation || 'Not assigned';
  return employee?.departmentName ? `${title} · ${employee.departmentName}` : title;
}

export function stageLabel(role) {
  if (role === 'DepartmentHead') return 'Director';
  if (role === 'Admin') return 'C-Level';
  return role || 'Approver';
}

export function ApprovalProgress({ steps, currentRole, compact }) {
  if (!steps?.length) {
    return currentRole ? <span className="approval-now">Waiting for {stageLabel(currentRole)}</span> : null;
  }
  return (
    <ol className={`approval-progress ${compact ? 'compact' : ''}`} aria-label="Approval stage">
      {steps.map(step => {
        const approved = String(step.status).toUpperCase() === 'APPROVED';
        const rejected = ['REJECTED', 'REVISION_REQUIRED'].includes(String(step.status).toUpperCase());
        const current = !approved && !rejected && step.requiredRole === currentRole;
        return (
          <li key={`${step.sequence}-${step.requiredRole}`} className={approved ? 'done' : rejected ? 'blocked' : current ? 'current' : 'pending'}>
            <strong>{stageLabel(step.requiredRole)}</strong>
            <small>{approved ? 'Approved' : rejected ? String(step.status).replaceAll('_', ' ') : current ? 'In review' : 'Waiting'}</small>
          </li>
        );
      })}
    </ol>
  );
}

export function AmountDisplay({ amount, currency = 'LKR', large }) {
  const formatted = new Intl.NumberFormat('en-LK', {
    style: 'currency', currency, maximumFractionDigits: 2
  }).format(amount || 0);
  return <span className={`amount ${large ? 'large' : ''}`}>{formatted}</span>;
}

export function ProgressBar({ value, max }) {
  const pct = max > 0 ? Math.min((value / max) * 100, 100) : 0;
  const colorClass = pct >= 90 ? 'progress-high' : pct >= 70 ? 'progress-medium' : 'progress-low';
  return (
    <div>
      <div className="budget-meter-labels">
        <span>{pct.toFixed(1)}% utilised</span>
        <span>{pct < 100 ? `${(100 - pct).toFixed(1)}% remaining` : 'Over budget!'}</span>
      </div>
      <div className="progress-bar-track">
        <div className={`progress-bar-fill ${colorClass}`} style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

export function Pagination({ page, totalPages, onPageChange }) {
  const pages = Array.from({ length: totalPages }, (_, i) => i + 1);
  return (
    <div className="pagination">
      <button className="page-btn" onClick={() => onPageChange(page - 1)} disabled={page <= 1}>‹</button>
      {pages.slice(Math.max(0, page - 3), Math.min(totalPages, page + 2)).map(p => (
        <button
          key={p}
          id={`page-btn-${p}`}
          className={`page-btn ${p === page ? 'active' : ''}`}
          onClick={() => onPageChange(p)}
        >{p}</button>
      ))}
      <button className="page-btn" onClick={() => onPageChange(page + 1)} disabled={page >= totalPages}>›</button>
    </div>
  );
}
