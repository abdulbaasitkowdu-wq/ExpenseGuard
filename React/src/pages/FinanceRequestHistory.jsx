import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getRequestHistory } from '../services/api';
import { LoadingSpinner } from '../components/Shared';

const kinds = [
  { value: '', label: 'All requests' },
  { value: 'purchase_request', label: 'Purchase requests' },
  { value: 'claim', label: 'Reimbursement claims' },
];

export default function FinanceRequestHistory() {
  const [kind, setKind] = useState('');
  const [employeeId, setEmployeeId] = useState('');
  const params = Object.fromEntries(Object.entries({
    kind: kind || undefined,
    employeeId: employeeId || undefined,
    limit: 100,
  }).filter(([, value]) => value !== undefined));
  const history = useQuery({ queryKey: ['request-history', params], queryFn: () => getRequestHistory(params) });

  return (
    <div className="page-content">
      <header className="feature-header">
        <div>
          <h2>Request history</h2>
          <p>Company-wide purchase and reimbursement history is stored in the database. Only Finance can look it up. Employee accounts only see their own requests.</p>
        </div>
      </header>
      <div className="filter-bar">
        <label className="form-group">
          <span className="form-label">Type</span>
          <select className="form-control compact" value={kind} onChange={e => setKind(e.target.value)}>
            {kinds.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
        </label>
        <label className="form-group">
          <span className="form-label">Employee ID</span>
          <input className="form-control compact" inputMode="numeric" placeholder="Optional" value={employeeId}
            onChange={e => setEmployeeId(e.target.value.replace(/\D/g, ''))} />
        </label>
      </div>
      {history.isPending && <LoadingSpinner />}
      {history.isError && <div className="alert alert-danger">{history.error.message}</div>}
      {!history.isPending && !history.isError && !history.data?.length && (
        <div className="empty-state card">No request history matches these filters.</div>
      )}
      {!!history.data?.length && (
        <div className="table-wrapper">
          <div className="table-header"><h3>Stored history</h3><span>{history.data.length} events</span></div>
          <table>
            <thead>
              <tr>
                <th>When</th><th>Type</th><th>Request</th><th>Employee</th><th>Department</th>
                <th>Vendor / category</th><th>Amount</th><th>Change</th><th>Note</th>
              </tr>
            </thead>
            <tbody>
              {history.data.map((row, index) => (
                <tr key={`${row.kind}-${row.requestId}-${row.changedAt}-${index}`}>
                  <td>{new Date(row.changedAt).toLocaleString()}</td>
                  <td>{row.kind === 'purchase_request' ? 'Purchase request' : 'Claim'}</td>
                  <td>#{row.requestId}</td>
                  <td>{row.employeeName} <small className="employee-secondary">#{row.employeeId}</small></td>
                  <td>{row.departmentName || '—'}</td>
                  <td>{[row.vendor, row.category].filter(Boolean).join(' · ') || '—'}</td>
                  <td>{row.currency} {Number(row.amount).toLocaleString()}</td>
                  <td>{row.fromStatus} → {row.toStatus}</td>
                  <td>{row.reason || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
