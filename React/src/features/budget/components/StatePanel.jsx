import { apiError } from '../api';

export default function StatePanel({ error, message, onRetry }) {
  const detail = error ? apiError(error) : { kind: 'empty', message };
  return (
    <div className={`alert alert-${detail.kind === 'forbidden' || detail.kind === 'conflict' ? 'warning' : 'danger'}`} role="alert">
      <span>{detail.message}</span>
      {onRetry && <button className="btn btn-ghost btn-sm" onClick={onRetry}>Retry</button>}
    </div>
  );
}
