import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  correctReceipt, getClaim, getClaimHistory, resubmitClaim, updateClaim, uploadReceipt,
} from '../services/api';
import { ApprovalProgress } from '../components/Shared';

const claimStatuses = ['Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'NeedsCorrection', 'Cancelled'];
const receiptStatuses = ['Pending', 'Processed', 'NeedsReview', 'Failed'];
const statusName = value => typeof value === 'number' ? claimStatuses[value] : value;
const receiptStatusName = value => typeof value === 'number' ? receiptStatuses[value] : value;

export default function EmployeeClaimDetail() {
  const { id } = useParams();
  const client = useQueryClient();
  const [receipt, setReceipt] = useState(null);
  const [reason, setReason] = useState('');
  const claim = useQuery({ queryKey: ['claim', id], queryFn: () => getClaim(id) });
  const history = useQuery({ queryKey: ['claim-history', id], queryFn: () => getClaimHistory(id) });
  const refresh = () => {
    client.invalidateQueries({ queryKey: ['claim', id] });
    client.invalidateQueries({ queryKey: ['claim-history', id] });
    client.invalidateQueries({ queryKey: ['claims'] });
  };
  const upload = useMutation({ mutationFn: file => uploadReceipt(id, file), onSuccess: setReceipt });
  const correct = useMutation({
    mutationFn: body => correctReceipt(id, receipt.receiptId, body),
    onSuccess: setReceipt,
  });
  const update = useMutation({ mutationFn: body => updateClaim(id, body), onSuccess: refresh });
  const resubmit = useMutation({ mutationFn: () => resubmitClaim(id, reason || null), onSuccess: refresh });

  if (claim.isPending) return <div className="loading-spinner" />;
  if (claim.isError) return <div className="page-content"><div className="alert alert-danger">{claim.error.message}</div></div>;
  const value = claim.data;
  const currentStatus = statusName(value.status);
  const save = event => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    update.mutate({
      amount: Number(form.get('amount')), category: form.get('category'),
      description: form.get('description'), currency: form.get('currency').toUpperCase(),
      vendor: form.get('vendor') || null, purchaseDate: form.get('purchaseDate') || null,
      purchaseRequestId: value.purchaseRequestId, flow: value.flow, version: value.version,
    });
  };
  const saveCorrection = event => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    correct.mutate({
      vendor: form.get('vendor') || null,
      amount: form.get('amount') ? Number(form.get('amount')) : null,
      purchaseDate: form.get('purchaseDate') || null,
      currency: form.get('currency')?.toUpperCase() || null,
    });
  };
  return <div className="page-content">
    <Link to="/employee" className="employee-back">← Back to employee workspace</Link>
    <div className="employee-heading"><div><h2>Claim #{value.expenseClaimId}</h2><p>{currentStatus} · version {value.version}</p>
      <ApprovalProgress steps={value.approvalSteps} currentRole={value.currentRequiredRole} /></div></div>
    <div className="employee-grid">
      <form className="card" onSubmit={save}>
        <h3>Claim details</h3>
        <label className="form-group"><span className="form-label">Category</span><input className="form-control" name="category" defaultValue={value.category} disabled={currentStatus !== 'Draft' && currentStatus !== 'NeedsCorrection'} required /></label>
        <label className="form-group"><span className="form-label">Description</span><textarea className="form-control" name="description" defaultValue={value.description} disabled={currentStatus !== 'Draft' && currentStatus !== 'NeedsCorrection'} required /></label>
        <label className="form-group"><span className="form-label">Vendor</span><input className="form-control" name="vendor" defaultValue={value.vendor || ''} disabled={currentStatus !== 'Draft' && currentStatus !== 'NeedsCorrection'} /></label>
        <div className="employee-form-row">
          <label className="form-group"><span className="form-label">Amount</span><input className="form-control" name="amount" type="number" step="0.01" defaultValue={value.amount} disabled={currentStatus !== 'Draft' && currentStatus !== 'NeedsCorrection'} required /></label>
          <label className="form-group"><span className="form-label">Currency</span><input className="form-control" name="currency" defaultValue={value.currency} disabled={currentStatus !== 'Draft' && currentStatus !== 'NeedsCorrection'} required /></label>
        </div>
        <label className="form-group"><span className="form-label">Purchase date</span><input className="form-control" name="purchaseDate" type="date" defaultValue={value.purchaseDate?.slice(0, 10) || ''} disabled={currentStatus !== 'Draft' && currentStatus !== 'NeedsCorrection'} /></label>
        {(currentStatus === 'Draft' || currentStatus === 'NeedsCorrection') && <button className="btn btn-primary">Save changes</button>}
        {update.isError && <div className="alert alert-danger">{update.error.message}</div>}
      </form>
      <div>
        <section className="card employee-section">
          <h3>Receipt and OCR</h3>
          <p className="employee-secondary">Upload an image or PDF. The API performs storage and OCR processing.</p>
          <input aria-label="Receipt file" type="file" accept="image/*,.pdf,application/pdf" onChange={e => e.target.files[0] && upload.mutate(e.target.files[0])} />
          {upload.isPending && <p>Uploading and processing…</p>}
          {upload.isError && <div className="alert alert-danger">{upload.error.message}</div>}
          {receipt && <form onSubmit={saveCorrection} className="employee-ocr">
            <span className={`badge ${receipt.requiresManualReview ? 'pending' : 'approved'}`}>{receipt.requiresManualReview ? 'Manual review required' : receiptStatusName(receipt.processingStatus)}</span>
            <label className="form-group"><span className="form-label">Extracted vendor</span><input className="form-control" name="vendor" defaultValue={receipt.extractedVendor || ''} /></label>
            <label className="form-group"><span className="form-label">Extracted amount</span><input className="form-control" name="amount" type="number" step="0.01" defaultValue={receipt.extractedAmount || ''} /></label>
            <label className="form-group"><span className="form-label">Extracted date</span><input className="form-control" name="purchaseDate" type="date" defaultValue={receipt.extractedDate?.slice(0, 10) || ''} /></label>
            <label className="form-group"><span className="form-label">Currency</span><input className="form-control" name="currency" defaultValue={receipt.extractedCurrency || value.currency} /></label>
            {receipt.extractedText && <label className="form-group"><span className="form-label">OCR text</span><textarea className="form-control" readOnly rows={6} value={receipt.extractedText} /></label>}
            <button className="btn btn-ghost">Confirm OCR corrections</button>
          </form>}
        </section>
        {currentStatus === 'NeedsCorrection' && <section className="card employee-section">
          <h3>Resubmit claim</h3>
          <textarea className="form-control" aria-label="Resubmission reason" placeholder="Describe your corrections" value={reason} onChange={e => setReason(e.target.value)} />
          <button className="btn btn-primary" onClick={() => resubmit.mutate()} disabled={resubmit.isPending}>Resubmit</button>
        </section>}
      </div>
    </div>
    <section className="card employee-section">
      <h3>Status history</h3>
      {history.isPending && <p>Loading history…</p>}
      {history.isError && <div className="alert alert-danger">{history.error.message}</div>}
      {!history.isPending && !history.data?.length && <div className="employee-empty">No status changes yet.</div>}
      <div className="employee-timeline">{history.data?.map(item => <div key={item.claimStatusHistoryId}>
        <strong>{statusName(item.fromStatus)} → {statusName(item.toStatus)}</strong>
        <span>{new Date(item.changedAt).toLocaleString()}{item.reason ? ` · ${item.reason}` : ''}</span>
      </div>)}</div>
    </section>
  </div>;
}
