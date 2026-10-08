import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createPolicy, createPolicyVersion, getPolicies } from '../services/api';
import { useAuth } from '../auth/AuthContext';
import { AccessGate, MutationError, QueryState } from '../components/RequestState';
import { Pagination, StatusBadge } from '../components/Shared';

const blank = {
  policyCode: '', category: '', minAmount: '', maxAmount: '', currency: 'LKR',
  receiptRequired: false, priority: 0, effectiveFrom: '', effectiveTo: '',
  departmentId: '', designations: '', activate: true,
};

export default function PolicyManagement() {
  const auth = useAuth();
  const canEdit = auth.hasAnyRole('Admin', 'PolicyManager');
  const [filters, setFilters] = useState({ category: '', active: '', page: 1, pageSize: 10 });
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(blank);
  const [errors, setErrors] = useState({});
  const client = useQueryClient();
  const query = useQuery({
    queryKey: ['policies', filters],
    queryFn: () => getPolicies({
      ...filters,
      category: filters.category || undefined,
      active: filters.active === '' ? undefined : filters.active === 'true',
    }).then(response => response.data),
  });
  const mutation = useMutation({
    mutationFn: payload => editing
      ? createPolicyVersion(editing.policyId, payload)
      : createPolicy(payload),
    onSuccess: () => {
      setEditing(null);
      setForm(blank);
      client.invalidateQueries({ queryKey: ['policies'] });
    },
  });

  function startVersion(policy) {
    setEditing(policy);
    setForm({
      ...blank,
      policyCode: policy.policyCode,
      category: policy.category,
      minAmount: policy.minAmount ?? '',
      maxAmount: policy.maxAmount ?? '',
      currency: policy.currency,
      receiptRequired: policy.receiptRequired,
      priority: policy.priority,
      effectiveFrom: '',
      departmentId: policy.departmentId ?? '',
      designations: policy.designations.join(', '),
    });
  }

  function submit(event) {
    event.preventDefault();
    const next = validate(form);
    setErrors(next);
    if (Object.keys(next).length) return;
    mutation.mutate({
      ...form,
      minAmount: numberOrNull(form.minAmount),
      maxAmount: numberOrNull(form.maxAmount),
      priority: Number(form.priority),
      departmentId: form.departmentId === '' ? null : Number(form.departmentId),
      effectiveFrom: new Date(form.effectiveFrom).toISOString(),
      effectiveTo: form.effectiveTo ? new Date(form.effectiveTo).toISOString() : null,
      designations: form.designations.split(',').map(value => value.trim()).filter(Boolean),
      ...(editing ? {} : { activate: undefined }),
    });
  }

  return (
    <AccessGate roles={['Admin', 'PolicyManager', 'Finance', 'Manager', 'Auditor']}>
      <div className="page-content feature-page">
        <header className="page-heading">
          <div><h2>Policy management</h2><p>Create immutable versions and control effective periods.</p></div>
        </header>
        {canEdit && (
          <form className="card management-form" onSubmit={submit} noValidate>
            <h3>{editing ? `New version of ${editing.policyCode} v${editing.version}` : 'Create policy'}</h3>
            <div className="form-grid">
              <Field label="Policy code" error={errors.policyCode}><input className="form-control" disabled={Boolean(editing)} value={form.policyCode} onChange={e => setForm({ ...form, policyCode: e.target.value.toUpperCase() })} /></Field>
              <Field label="Category" error={errors.category}><input className="form-control" value={form.category} onChange={e => setForm({ ...form, category: e.target.value })} /></Field>
              <Field label="Currency" error={errors.currency}><input className="form-control" maxLength="3" value={form.currency} onChange={e => setForm({ ...form, currency: e.target.value.toUpperCase() })} /></Field>
              <Field label="Priority"><input className="form-control" type="number" value={form.priority} onChange={e => setForm({ ...form, priority: e.target.value })} /></Field>
              <Field label="Minimum amount" error={errors.amount}><input className="form-control" type="number" min="0" value={form.minAmount} onChange={e => setForm({ ...form, minAmount: e.target.value })} /></Field>
              <Field label="Maximum amount" error={errors.amount}><input className="form-control" type="number" min="0" value={form.maxAmount} onChange={e => setForm({ ...form, maxAmount: e.target.value })} /></Field>
              <Field label="Effective from" error={errors.effectiveFrom}><input className="form-control" type="datetime-local" value={form.effectiveFrom} onChange={e => setForm({ ...form, effectiveFrom: e.target.value })} /></Field>
              <Field label="Effective to" error={errors.effectiveTo}><input className="form-control" type="datetime-local" value={form.effectiveTo} onChange={e => setForm({ ...form, effectiveTo: e.target.value })} /></Field>
              <Field label="Department ID"><input className="form-control" type="number" min="1" value={form.departmentId} onChange={e => setForm({ ...form, departmentId: e.target.value })} /></Field>
              <Field label="Designations (comma separated)"><input className="form-control" value={form.designations} onChange={e => setForm({ ...form, designations: e.target.value })} /></Field>
            </div>
            <label className="check-label"><input type="checkbox" checked={form.receiptRequired} onChange={e => setForm({ ...form, receiptRequired: e.target.checked })} /> Receipt required</label>
            {editing && <label className="check-label"><input type="checkbox" checked={form.activate} onChange={e => setForm({ ...form, activate: e.target.checked })} /> Activate this version</label>}
            <MutationError error={mutation.error} />
            <div className="button-row">
              <button className="btn btn-primary" disabled={mutation.isPending}>{mutation.isPending ? 'Saving…' : editing ? 'Create version' : 'Create policy'}</button>
              {editing && <button type="button" className="btn btn-ghost" onClick={() => { setEditing(null); setForm(blank); }}>Cancel</button>}
            </div>
          </form>
        )}
        <div className="table-wrapper">
          <div className="filter-bar">
            <input aria-label="Category filter" placeholder="Filter category" value={filters.category} onChange={e => setFilters({ ...filters, category: e.target.value, page: 1 })} />
            <select aria-label="Active filter" value={filters.active} onChange={e => setFilters({ ...filters, active: e.target.value, page: 1 })}>
              <option value="">All versions</option><option value="true">Active</option><option value="false">Inactive</option>
            </select>
          </div>
          <QueryState query={query} empty={query.data?.items?.length === 0}>
            <table><thead><tr><th>Policy</th><th>Category</th><th>Limits</th><th>Effective</th><th>Status</th>{canEdit && <th>Action</th>}</tr></thead>
              <tbody>{query.data?.items?.map(policy => (
                <tr key={policy.policyId}>
                  <td>{policy.policyCode} <small>v{policy.version}</small></td>
                  <td>{policy.category}</td>
                  <td>{policy.minAmount ?? '—'} – {policy.maxAmount ?? '—'} {policy.currency}</td>
                  <td>{formatDate(policy.effectiveFrom)} → {formatDate(policy.effectiveTo)}</td>
                  <td><StatusBadge status={policy.isActive ? 'approved' : 'pending'} /></td>
                  {canEdit && <td><button className="btn btn-ghost btn-sm" onClick={() => startVersion(policy)}>New version</button></td>}
                </tr>
              ))}</tbody>
            </table>
            {(query.data?.total ?? 0) > filters.pageSize && <Pagination page={filters.page} totalPages={Math.ceil(query.data.total / filters.pageSize)} onPageChange={page => setFilters({ ...filters, page })} />}
          </QueryState>
        </div>
      </div>
    </AccessGate>
  );
}

function Field({ label, error, children }) {
  return <label className="form-group"><span className="form-label">{label}</span>{children}{error && <small className="field-error">{error}</small>}</label>;
}

function validate(form) {
  const errors = {};
  if (!form.policyCode.trim()) errors.policyCode = 'Policy code is required.';
  if (!form.category.trim()) errors.category = 'Category is required.';
  if (!/^[A-Z]{3}$/.test(form.currency)) errors.currency = 'Use a three-letter uppercase currency.';
  if (!form.effectiveFrom) errors.effectiveFrom = 'Start date is required.';
  if (form.minAmount !== '' && form.maxAmount !== '' && Number(form.minAmount) > Number(form.maxAmount)) errors.amount = 'Minimum cannot exceed maximum.';
  if (form.effectiveTo && form.effectiveFrom && new Date(form.effectiveTo) <= new Date(form.effectiveFrom)) errors.effectiveTo = 'End must be after start.';
  return errors;
}
const numberOrNull = value => value === '' ? null : Number(value);
const formatDate = value => value ? new Date(value).toLocaleDateString() : 'open';

