import { useState } from 'react';
import { AmountDisplay, LoadingSpinner, ProgressBar } from '../../../components/Shared';
import { useDepartments } from '../../department/hooks';
import { apiError, budgetApi } from '../api';
import {
  budgetKeys, useBudget, useBudgetAlerts, useBudgetMutation,
  useBudgetTransactions, useUtilization,
} from '../hooks';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import StatePanel from './StatePanel';

const today = new Date().toISOString().slice(0, 10);
const DEPARTMENT_BUDGET_CAP = 100_000_000;

function remainingCapacity(items, departmentId, currency) {
  if (!departmentId) return DEPARTMENT_BUDGET_CAP;
  const allocated = items
    .filter((item) => String(item.departmentId) === String(departmentId) && item.currency === currency)
    .reduce((sum, item) => sum + Number(item.allocatedAmount), 0);
  return Math.max(0, DEPARTMENT_BUDGET_CAP - allocated);
}

export default function BudgetWorkspace() {
  const [selectedId, setSelectedId] = useState(null);
  const [departmentId, setDepartmentId] = useState('');
  const report = useUtilization(departmentId);
  const departments = useDepartments(true);

  return (
    <div>
      <header className="feature-header">
        <div>
          <h2>Budget allocation & utilization</h2>
          <p>Each department’s active budgets are capped at LKR 100,000,000. The Budget for column shows what each allocation covers.</p>
        </div>
        <select className="form-control compact" aria-label="Filter by department" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
          <option value="">All departments</option>
          {departments.data?.items?.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
      </header>
      <AllocationForm departments={departments.data?.items ?? []} utilization={report.data?.items ?? []} />
      {report.isPending ? <LoadingSpinner /> : report.isError
        ? <StatePanel error={report.error} onRetry={report.refetch} />
        : !report.data?.items?.length
          ? <div className="empty-state card">No budgets match this filter.</div>
          : <UtilizationTable items={report.data.items} selectedId={selectedId} onSelect={setSelectedId} />}
      {selectedId && <BudgetDetail id={selectedId} onClose={() => setSelectedId(null)} />}
    </div>
  );
}

function AllocationForm({ departments, utilization }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState('');
  const [form, setForm] = useState({ departmentId: '', name: '', periodStart: today, periodEnd: today, currency: 'LKR', amount: '' });
  const currency = form.currency.toUpperCase();
  const remaining = remainingCapacity(utilization, form.departmentId, currency);
  const mutation = useMutation({
    mutationFn: budgetApi.allocate,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: budgetKeys.all });
      setOpen(false);
    },
  });
  const submit = (event) => {
    event.preventDefault();
    const amount = Number(form.amount);
    if (!form.departmentId || !form.name.trim() || amount <= 0 || form.periodEnd < form.periodStart) {
      setError('Choose a department, enter what the budget is for, a positive amount, and a valid period.');
      return;
    }
    if (amount > DEPARTMENT_BUDGET_CAP || amount > remaining) {
      setError(`Department total cannot exceed ${DEPARTMENT_BUDGET_CAP.toLocaleString()} ${currency}. Remaining capacity is ${remaining.toLocaleString()}.`);
      return;
    }
    setError('');
    mutation.mutate({
      ...form,
      departmentId: Number(form.departmentId),
      amount,
      currency,
      idempotencyKey: crypto.randomUUID(),
    });
  };
  return <div className="card allocation-card">
    <button className="btn btn-primary" onClick={() => setOpen(!open)}>{open ? 'Close allocation form' : 'Allocate budget'}</button>
    {open && <form className="form-grid" onSubmit={submit}>
      <label><span className="form-label">Department</span><select className="form-control" value={form.departmentId} onChange={(e) => setForm({ ...form, departmentId: e.target.value })}>
        <option value="">Select…</option>{departments.map((d) => <option value={d.id} key={d.id}>{d.name}</option>)}
      </select></label>
      <label><span className="form-label">Budget for</span><input className="form-control" maxLength="120" placeholder="e.g. Cloud infrastructure Q2 2026" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
      <label><span className="form-label">Start</span><input type="date" className="form-control" value={form.periodStart} onChange={(e) => setForm({ ...form, periodStart: e.target.value })} /></label>
      <label><span className="form-label">End</span><input type="date" className="form-control" value={form.periodEnd} onChange={(e) => setForm({ ...form, periodEnd: e.target.value })} /></label>
      <label><span className="form-label">Amount</span><input type="number" min="0.01" max={Math.min(DEPARTMENT_BUDGET_CAP, remaining) || 0.01} step="0.01" className="form-control" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} /></label>
      <label><span className="form-label">Currency</span><input className="form-control" maxLength="3" value={form.currency} onChange={(e) => setForm({ ...form, currency: e.target.value })} /></label>
      {form.departmentId && <p className="form-hint">Remaining department capacity: <AmountDisplay amount={remaining} currency={currency} /></p>}
      <button className="btn btn-primary" disabled={mutation.isPending || remaining <= 0}>{mutation.isPending ? 'Allocating…' : 'Confirm allocation'}</button>
      {error && <div className="alert alert-danger">{error}</div>}
      {mutation.error && <StatePanel error={mutation.error} />}
    </form>}
  </div>;
}

function UtilizationTable({ items, selectedId, onSelect }) {
  return <div className="table-wrapper">
    <div className="table-header"><h3>Utilization report</h3><span>{items.length} budgets</span></div>
    <table><thead><tr><th>Department</th><th>Budget for</th><th>Allocated</th><th>Reserved</th><th>Spent</th><th>Available</th><th>Utilization</th><th>Alerts</th></tr></thead>
      <tbody>{items.map((item) => <tr key={item.budgetId} className={selectedId === item.budgetId ? 'selected-row' : ''} onClick={() => onSelect(item.budgetId)}>
        <td>{item.departmentName}</td><td>{item.budgetName}</td><td><AmountDisplay amount={item.allocatedAmount} currency={item.currency} /></td>
        <td><AmountDisplay amount={item.reservedAmount} currency={item.currency} /></td><td><AmountDisplay amount={item.spentAmount} currency={item.currency} /></td>
        <td><AmountDisplay amount={item.availableAmount} currency={item.currency} /></td><td><ProgressBar value={item.utilizationPercent} max={100} /></td><td>{item.openAlerts}</td>
      </tr>)}</tbody>
    </table>
  </div>;
}

function BudgetDetail({ id, onClose }) {
  const budget = useBudget(id);
  const transactions = useBudgetTransactions(id);
  const [alertStatus, setAlertStatus] = useState('');
  const alerts = useBudgetAlerts(id, alertStatus);
  if (budget.isPending) return <LoadingSpinner />;
  if (budget.isError) return <StatePanel error={budget.error} onRetry={budget.refetch} />;
  return <section className="detail-panel card">
    <header className="feature-header"><div><h3>{budget.data.name}</h3><p>{budget.data.periodStart} – {budget.data.periodEnd}</p></div><button className="btn btn-ghost" onClick={onClose}>Close</button></header>
    <div className="stats-grid">
      <Balance label="Allocated" value={budget.data.allocatedAmount} currency={budget.data.currency} />
      <Balance label="Reserved" value={budget.data.reservedAmount} currency={budget.data.currency} />
      <Balance label="Spent" value={budget.data.spentAmount} currency={budget.data.currency} />
      <Balance label="Available" value={budget.data.availableAmount} currency={budget.data.currency} />
    </div>
    <BudgetActionForm budget={budget.data} />
    <div className="feature-grid">
      <div><h3>Transaction history</h3><RecordState query={transactions} empty="No reservation or spend transactions yet.">
        {(data) => <ul className="record-list">{data.items.map((t) => <li key={t.id}><strong>{t.type}</strong> <AmountDisplay amount={t.amount} currency={budget.data.currency} /><span>{t.reference || 'No reference'} · {new Date(t.createdAt).toLocaleString()}</span></li>)}</ul>}
      </RecordState></div>
      <div><div className="feature-header"><h3>Alert review</h3><select className="form-control compact" value={alertStatus} onChange={(e) => setAlertStatus(e.target.value)}>
        <option value="">All</option><option value="Open">Open</option><option value="Acknowledged">Acknowledged</option><option value="Resolved">Resolved</option>
      </select></div><RecordState query={alerts} empty="No alerts for this budget.">
        {(data) => <ul className="record-list">{data.map((a) => <li key={a.id}><strong>{a.severity} · {a.status}</strong><span>{a.message} ({a.utilizationPercent}%)</span></li>)}</ul>}
      </RecordState></div>
    </div>
  </section>;
}

function BudgetActionForm({ budget }) {
  const [action, setAction] = useState('reserve');
  const [form, setForm] = useState({ amount: '', reference: '', description: '', fromReservation: true });
  const mutation = useBudgetMutation(action, budget.id);
  const submit = (event) => {
    event.preventDefault();
    const amount = Number(form.amount);
    if (amount <= 0) return;
    mutation.mutate({ ...form, amount, version: budget.version, idempotencyKey: crypto.randomUUID() }, { onSuccess: () => setForm({ ...form, amount: '', reference: '', description: '' }) });
  };
  return <form className="card action-form" onSubmit={submit}>
    <select className="form-control compact" value={action} onChange={(e) => setAction(e.target.value)}><option value="reserve">Reserve</option><option value="release">Release</option><option value="spend">Spend</option></select>
    <input aria-label="Transaction amount" required type="number" min="0.01" step="0.01" className="form-control compact" placeholder="Amount" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} />
    <input aria-label="Reference" className="form-control compact" placeholder="Reference" value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} />
    <input aria-label="Description" className="form-control" placeholder="Description" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
    {action === 'spend' && <label className="check-row"><input type="checkbox" checked={form.fromReservation} onChange={(e) => setForm({ ...form, fromReservation: e.target.checked })} />Use reserved funds</label>}
    <button className="btn btn-primary" disabled={mutation.isPending}>{mutation.isPending ? 'Submitting…' : action}</button>
    {mutation.error && <span className={`inline-error ${apiError(mutation.error).kind}`}>{apiError(mutation.error).message}</span>}
  </form>;
}

function RecordState({ query, empty, children }) {
  if (query.isPending) return <LoadingSpinner />;
  if (query.isError) return <StatePanel error={query.error} onRetry={query.refetch} />;
  const emptyData = Array.isArray(query.data) ? !query.data.length : !query.data?.items?.length;
  return emptyData ? <div className="empty-state">{empty}</div> : children(query.data);
}

function Balance({ label, value, currency }) {
  return <div className="stat-card primary"><span className="stat-label">{label}</span><AmountDisplay amount={value} currency={currency} large /></div>;
}
