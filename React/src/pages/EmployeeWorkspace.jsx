import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FiClipboard, FiFileText, FiUser } from 'react-icons/fi';
import {
  createClaim, createPurchaseRequest, deleteClaim, deletePurchaseRequest,
  getMyProfile, getPurchaseRequests, searchClaims, submitClaim, submitPurchaseRequest,
  updatePurchaseRequest,
} from '../services/api';
import { ApprovalProgress, formatDesignation } from '../components/Shared';

const emptyClaim = {
  amount: '', category: '', description: '', currency: 'LKR', vendor: '',
  purchaseDate: '', flow: 'OutOfPocket', purchaseRequestId: '',
};
const emptyRequest = { description: '', estimatedAmount: '', currency: 'LKR', vendor: '', category: '' };
const requestForm = request => ({
  description: request.description || '',
  estimatedAmount: String(request.estimatedAmount ?? ''),
  currency: request.currency || 'LKR',
  vendor: request.vendor || '',
  category: request.category || '',
});
const revisionNote = request => request.approvalSteps?.find(step =>
  String(step.status).toUpperCase() === 'REVISION_REQUIRED')?.comment || null;
const statusClass = status => `badge ${String(status).toLowerCase()}`;
const claimStatuses = ['Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'NeedsCorrection', 'Cancelled'];
const requestStatuses = ['Draft', 'Submitted', 'Approved', 'Rejected', 'Cancelled'];
const enumLabel = (value, names) => typeof value === 'number' ? names[value] : value;

function QueryState({ query, empty, children }) {
  if (query.isPending) return <div className="loading-spinner" aria-label="Loading" />;
  if (query.isError) return <div className="alert alert-danger">{query.error.message}</div>;
  if (!query.data?.length) return <div className="employee-empty">{empty}</div>;
  return children(query.data);
}

function ProfilePanel() {
  const profile = useQuery({ queryKey: ['employee-profile'], queryFn: getMyProfile });
  if (profile.isPending) return <div className="loading-spinner" />;
  if (profile.isError) return <div className="alert alert-danger">{profile.error.message}</div>;
  const value = profile.data;
  return (
    <div className="card employee-profile">
      <div><span>Name</span><strong>{value.fullName}</strong></div>
      <div><span>Email</span><strong>{value.email}</strong></div>
      <div><span>Username</span><strong>{value.username}</strong></div>
      <div><span>Designation</span><strong>{formatDesignation(value)}</strong></div>
      <div><span>Account</span><strong>{value.isActive && !value.isLocked ? 'Active' : 'Restricted'}</strong></div>
    </div>
  );
}

function PurchaseRequestsPanel() {
  const client = useQueryClient();
  const [form, setForm] = useState(emptyRequest);
  const [editing, setEditing] = useState(null);
  const requests = useQuery({ queryKey: ['purchase-requests'], queryFn: getPurchaseRequests });
  const refresh = () => client.invalidateQueries({ queryKey: ['purchase-requests'] });
  const resetForm = () => { setEditing(null); setForm(emptyRequest); };
  const save = useMutation({
    mutationFn: payload => editing
      ? updatePurchaseRequest(editing.id, payload)
      : createPurchaseRequest(payload),
    onSuccess: resetForm,
    onSettled: refresh,
  });
  const submit = useMutation({ mutationFn: submitPurchaseRequest, onSuccess: refresh });
  const remove = useMutation({
    mutationFn: deletePurchaseRequest,
    onSuccess: (_result, id) => { if (editing?.id === id) resetForm(); },
    onSettled: refresh,
  });
  const beginEdit = request => {
    setEditing({ id: request.purchaseRequestId, version: request.version, note: revisionNote(request) });
    setForm(requestForm(request));
  };
  const onSubmit = event => {
    event.preventDefault();
    save.mutate({
      ...form, estimatedAmount: Number(form.estimatedAmount), version: editing?.version ?? 0,
    });
  };
  return <div className="employee-grid">
    <form className="card" onSubmit={onSubmit}>
      <h3>{editing ? `Edit purchase request #${editing.id}` : 'New purchase request'}</h3>
      {editing?.note && <div className="alert alert-warning">Revision requested: {editing.note}</div>}
      <label className="form-group"><span className="form-label">Description</span>
        <textarea className="form-control" required value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
      <label className="form-group"><span className="form-label">Estimated amount</span>
        <input className="form-control" type="number" min="0.01" step="0.01" required value={form.estimatedAmount} onChange={e => setForm({ ...form, estimatedAmount: e.target.value })} /></label>
      <label className="form-group"><span className="form-label">Currency</span>
        <input className="form-control" pattern="[A-Za-z]{3}" required value={form.currency} onChange={e => setForm({ ...form, currency: e.target.value.toUpperCase() })} /></label>
      <label className="form-group"><span className="form-label">Vendor</span>
        <input className="form-control" value={form.vendor} onChange={e => setForm({ ...form, vendor: e.target.value })} /></label>
      <label className="form-group"><span className="form-label">Category (optional)</span>
        <input className="form-control" value={form.category} onChange={e => setForm({ ...form, category: e.target.value })} placeholder="Inferred from description if blank" /></label>
      {save.isError && <div className="alert alert-danger">{save.error.message}</div>}
      <div className="decision-actions">
        <button className="btn btn-primary" disabled={save.isPending}>{editing ? 'Save changes' : 'Save draft'}</button>
        {editing && <button className="btn btn-ghost" type="button" onClick={resetForm}>Cancel</button>}
      </div>
    </form>
    <div className="table-wrapper">
      <div className="table-header"><h3>My purchase requests</h3></div>
      <QueryState query={requests} empty="No purchase requests yet.">
        {rows => <table><thead><tr><th>Description</th><th>Amount</th><th>Status</th><th>Approval</th><th>Actions</th></tr></thead>
          <tbody>{rows.map(request => <tr key={request.purchaseRequestId}>
            <td>{request.description}<small className="employee-secondary">{[request.vendor || 'No vendor', request.category, request.review?.hasFlags ? 'Flagged for review' : null].filter(Boolean).join(' · ')}</small></td>
            <td>{request.currency} {request.estimatedAmount.toLocaleString()}</td>
            <td><span className={statusClass(enumLabel(request.status, requestStatuses))}>{enumLabel(request.status, requestStatuses)}</span></td>
            <td><ApprovalProgress compact steps={request.approvalSteps} currentRole={request.currentRequiredRole} /></td>
            <td className="employee-actions">{enumLabel(request.status, requestStatuses) === 'Draft' && <>
              <button className="btn btn-ghost btn-sm" type="button" onClick={() => beginEdit(request)}>Edit</button>
              <button className="btn btn-primary btn-sm" type="button" onClick={() => submit.mutate(request.purchaseRequestId)}>Submit</button>
              <button className="btn btn-danger btn-sm" type="button" onClick={() => remove.mutate(request.purchaseRequestId)}>Delete</button>
            </>}</td>
          </tr>)}</tbody></table>}
      </QueryState>
    </div>
  </div>;
}

function ClaimsPanel() {
  const client = useQueryClient();
  const [filters, setFilters] = useState({ status: '', category: '', from: '', to: '' });
  const [form, setForm] = useState(emptyClaim);
  const params = Object.fromEntries(Object.entries({
    ...filters,
    status: filters.status ? claimStatuses.indexOf(filters.status) : '',
  }).filter(([, value]) => value !== ''));
  const claims = useQuery({ queryKey: ['claims', params], queryFn: () => searchClaims(params) });
  const requests = useQuery({ queryKey: ['purchase-requests'], queryFn: getPurchaseRequests });
  const refresh = () => client.invalidateQueries({ queryKey: ['claims'] });
  const create = useMutation({ mutationFn: createClaim, onSuccess: () => { setForm(emptyClaim); refresh(); } });
  const submit = useMutation({ mutationFn: submitClaim, onSuccess: refresh });
  const remove = useMutation({ mutationFn: deleteClaim, onSuccess: refresh });
  const save = event => {
    event.preventDefault();
    create.mutate({
      ...form, amount: Number(form.amount), purchaseDate: form.purchaseDate || null,
      vendor: form.vendor || null,
      purchaseRequestId: form.flow === 'PrePurchase' ? Number(form.purchaseRequestId) : null,
      flow: form.flow === 'PrePurchase' ? 1 : 0, version: 0,
    });
  };
  return <>
    <div className="employee-grid">
      <form className="card" onSubmit={save}>
        <h3>New claim draft</h3>
        {['category', 'description', 'vendor'].map(field => <label className="form-group" key={field}>
          <span className="form-label">{field}</span>
          <input className="form-control" required={field !== 'vendor'} value={form[field]} onChange={e => setForm({ ...form, [field]: e.target.value })} />
        </label>)}
        <div className="employee-form-row">
          <label className="form-group"><span className="form-label">Amount</span><input className="form-control" type="number" min="0.01" step="0.01" required value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} /></label>
          <label className="form-group"><span className="form-label">Currency</span><input className="form-control" pattern="[A-Za-z]{3}" required value={form.currency} onChange={e => setForm({ ...form, currency: e.target.value.toUpperCase() })} /></label>
        </div>
        <label className="form-group"><span className="form-label">Purchase date</span><input className="form-control" type="date" value={form.purchaseDate} onChange={e => setForm({ ...form, purchaseDate: e.target.value })} /></label>
        <label className="form-group"><span className="form-label">Flow</span><select className="form-control" value={form.flow} onChange={e => setForm({ ...form, flow: e.target.value, purchaseRequestId: '' })}><option>OutOfPocket</option><option>PrePurchase</option></select></label>
        {form.flow === 'PrePurchase' && <label className="form-group"><span className="form-label">Approved request</span>
          <select className="form-control" required value={form.purchaseRequestId} onChange={e => setForm({ ...form, purchaseRequestId: e.target.value })}>
            <option value="">Select request</option>{requests.data?.filter(x => enumLabel(x.status, requestStatuses) === 'Approved').map(x => <option key={x.purchaseRequestId} value={x.purchaseRequestId}>{x.description}</option>)}
          </select></label>}
        {create.isError && <div className="alert alert-danger">{create.error.message}</div>}
        <button className="btn btn-primary" disabled={create.isPending}>Save draft</button>
      </form>
      <div className="table-wrapper">
        <div className="table-header"><h3>Claims</h3></div>
        <div className="filter-bar">
          <input aria-label="Category search" placeholder="Category" value={filters.category} onChange={e => setFilters({ ...filters, category: e.target.value })} />
          <select aria-label="Status" value={filters.status} onChange={e => setFilters({ ...filters, status: e.target.value })}><option value="">All statuses</option>{['Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'NeedsCorrection'].map(x => <option key={x}>{x}</option>)}</select>
          <input aria-label="From date" type="date" value={filters.from} onChange={e => setFilters({ ...filters, from: e.target.value })} />
          <input aria-label="To date" type="date" value={filters.to} onChange={e => setFilters({ ...filters, to: e.target.value })} />
        </div>
        <QueryState query={claims} empty="No claims match these filters.">
          {rows => <table><thead><tr><th>Claim</th><th>Amount</th><th>Status</th><th>Approval</th><th>Actions</th></tr></thead>
            <tbody>{rows.map(claim => <tr key={claim.expenseClaimId}>
              <td><Link to={`/employee/claims/${claim.expenseClaimId}`}>#{claim.expenseClaimId} · {claim.category}</Link><small className="employee-secondary">{claim.description}</small></td>
              <td>{claim.currency} {claim.amount.toLocaleString()}</td>
              <td><span className={statusClass(enumLabel(claim.status, claimStatuses))}>{enumLabel(claim.status, claimStatuses)}</span></td>
              <td><ApprovalProgress compact steps={claim.approvalSteps} currentRole={claim.currentRequiredRole} /></td>
              <td className="employee-actions">{enumLabel(claim.status, claimStatuses) === 'Draft' && <>
                <button className="btn btn-primary btn-sm" onClick={() => submit.mutate(claim.expenseClaimId)}>Submit</button>
                <button className="btn btn-danger btn-sm" onClick={() => remove.mutate(claim.expenseClaimId)}>Delete</button>
              </>}</td>
            </tr>)}</tbody></table>}
        </QueryState>
      </div>
    </div>
  </>;
}

const workspaceTabs = [
  { id: 'claims', label: 'Claims', description: 'Draft, submit, and track reimbursements.', icon: <FiFileText /> },
  { id: 'requests', label: 'Purchase requests', description: 'Request spend before a purchase is made.', icon: <FiClipboard /> },
  { id: 'profile', label: 'Profile', description: 'Review your employee account details.', icon: <FiUser /> },
];

export default function EmployeeWorkspace() {
  const [tab, setTab] = useState('claims');
  return <div className="page-content">
    <div className="employee-heading"><div><h2>Employee expense workspace</h2><p>Draft, submit, and track your employee expenses.</p></div></div>
    <div className="workspace-tiles" aria-label="Expense workspace">
      {workspaceTabs.map(item => (
        <button key={item.id} type="button" aria-label={item.label} aria-pressed={tab === item.id}
          className={`workspace-tile ${tab === item.id ? 'active' : ''}`} onClick={() => setTab(item.id)}>
          <span className="workspace-tile-icon" aria-hidden="true">{item.icon}</span>
          <span className="workspace-tile-copy">
            <strong>{item.label}</strong>
            <small>{item.description}</small>
          </span>
        </button>
      ))}
    </div>
    {tab === 'claims' && <ClaimsPanel />}
    {tab === 'requests' && <PurchaseRequestsPanel />}
    {tab === 'profile' && <ProfilePanel />}
  </div>;
}
