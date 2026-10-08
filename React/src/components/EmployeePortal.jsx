import React, { useState, useEffect } from 'react';
import { PlusCircle, Send, CheckCircle, Clock, AlertTriangle, RefreshCw, Eye, Edit3 } from 'lucide-react';
import { api } from '../services/api';

export default function EmployeePortal({ currentUser, onSelectClaim }) {
  const [myClaims, setMyClaims] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showSubmitModal, setShowSubmitModal] = useState(false);
  const [resubmitClaimItem, setResubmitClaimItem] = useState(null);

  // New claim form state
  const [formData, setFormData] = useState({
    category: 'Meals',
    merchantName: '',
    amount: '',
    description: '',
    claimDate: new Date().toISOString().split('T')[0],
    receiptUrl: 'https://storage.enterprise.internal/receipts/sample.pdf',
  });

  const [resubmitData, setResubmitData] = useState({
    amount: '',
    merchantName: '',
    category: '',
    description: '',
    resubmissionNotes: '',
    receiptUrl: '',
  });

  const [errorMsg, setErrorMsg] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  const loadMyClaims = async () => {
    try {
      setLoading(true);
      const data = await api.getMyClaims();
      setMyClaims(data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadMyClaims();
  }, [currentUser]);

  const handleSubmitClaim = async (e) => {
    e.preventDefault();
    if (!formData.merchantName || !formData.amount || !formData.category) {
      setErrorMsg('Merchant name, category, and total amount are required.');
      return;
    }

    try {
      setErrorMsg('');
      setSuccessMsg('');
      const payload = {
        claimDate: new Date(formData.claimDate).toISOString(),
        merchantName: formData.merchantName,
        category: formData.category,
        totalAmount: parseFloat(formData.amount),
        currency: 'LKR',
        description: formData.description,
        items: [
          {
            expenseDate: new Date(formData.claimDate).toISOString(),
            category: formData.category,
            merchant: formData.merchantName,
            amount: parseFloat(formData.amount),
            currency: 'LKR',
            description: formData.description,
            receiptUrl: formData.receiptUrl || null,
          },
        ],
      };

      const result = await api.submitClaim(payload);
      setSuccessMsg(`Claim #${result.claimNumber} submitted successfully! Policy Engine & Fraud/Anomaly Risk Agent evaluated the claim.`);
      setShowSubmitModal(false);
      setFormData({
        category: 'Meals',
        merchantName: '',
        amount: '',
        description: '',
        claimDate: new Date().toISOString().split('T')[0],
        receiptUrl: 'https://storage.enterprise.internal/receipts/sample.pdf',
      });
      await loadMyClaims();
    } catch (err) {
      setErrorMsg(err.message || 'Failed to submit claim.');
    }
  };

  const handleOpenResubmit = (claim) => {
    setResubmitClaimItem(claim);
    setResubmitData({
      amount: claim.totalAmount,
      merchantName: claim.merchantName,
      category: claim.category,
      description: claim.description || '',
      resubmissionNotes: '',
      receiptUrl: 'https://storage.enterprise.internal/receipts/updated-receipt.pdf',
    });
  };

  const handleResubmitClaim = async (e) => {
    e.preventDefault();
    try {
      setErrorMsg('');
      const payload = {
        totalAmount: parseFloat(resubmitData.amount),
        merchantName: resubmitData.merchantName,
        category: resubmitData.category,
        description: resubmitData.description,
        resubmissionNotes: resubmitData.resubmissionNotes,
        items: [
          {
            expenseDate: new Date().toISOString(),
            category: resubmitData.category,
            merchant: resubmitData.merchantName,
            amount: parseFloat(resubmitData.amount),
            currency: 'LKR',
            description: resubmitData.description,
            receiptUrl: resubmitData.receiptUrl,
          },
        ],
      };

      await api.resubmitClaim(resubmitClaimItem.id, payload);
      setSuccessMsg(`Claim #${resubmitClaimItem.claimNumber} resubmitted! Re-entering validation workflow.`);
      setResubmitClaimItem(null);
      await loadMyClaims();
    } catch (err) {
      setErrorMsg(err.message || 'Failed to resubmit claim.');
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
        <div>
          <h1 style={{ fontSize: '1.75rem', fontWeight: 800, letterSpacing: '-0.02em' }}>
            Employee Spend & Reimbursement Portal
          </h1>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>
            Logged in as <strong>{currentUser?.fullName}</strong> ({currentUser?.role}) — Submit new claims and track policy validation.
          </p>
        </div>

        <button className="btn btn-primary" onClick={() => setShowSubmitModal(true)}>
          <PlusCircle size={16} /> Submit New Expense Claim
        </button>
      </div>

      {successMsg && (
        <div style={{ padding: '1rem', background: 'var(--success-bg)', border: '1px solid var(--success-border)', color: '#34d399', borderRadius: 'var(--radius-sm)', marginBottom: '1.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <CheckCircle size={18} /> {successMsg}
        </div>
      )}

      {errorMsg && (
        <div style={{ padding: '1rem', background: 'var(--danger-bg)', border: '1px solid var(--danger-border)', color: '#f87171', borderRadius: 'var(--radius-sm)', marginBottom: '1.5rem' }}>
          {errorMsg}
        </div>
      )}

      {/* Claims Table */}
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Claim Number</th>
              <th>Category & Merchant</th>
              <th>Amount</th>
              <th>Submitted Date</th>
              <th>Policy Status</th>
              <th>Risk Evaluation</th>
              <th>Workflow Status</th>
              <th>Manager Feedback</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {myClaims && myClaims.length > 0 ? (
              myClaims.map((claim) => (
                <tr key={claim.id}>
                  <td style={{ fontWeight: 700, color: 'var(--primary)' }}>{claim.claimNumber}</td>
                  <td>
                    <div style={{ fontWeight: 600 }}>{claim.category}</div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-dim)' }}>{claim.merchantName}</div>
                  </td>
                  <td style={{ fontWeight: 700 }}>
                    {claim.currency} {claim.totalAmount.toLocaleString()}
                  </td>
                  <td style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                    {new Date(claim.claimDate).toLocaleDateString()}
                  </td>
                  <td>
                    {claim.policyStatus === 'VIOLATIONS_FOUND' ? (
                      <span className="badge badge-danger">{claim.violationCount} Violations</span>
                    ) : (
                      <span className="badge badge-success">Compliant</span>
                    )}
                  </td>
                  <td>
                    <span className={`badge ${claim.riskStatus === 'REVIEW_REQUIRED' ? 'badge-review-required' : claim.riskStatus === 'HIGH_RISK' ? 'badge-high-risk' : 'badge-low-risk'}`}>
                      {claim.riskStatus} {claim.riskScore ? `(${claim.riskScore})` : ''}
                    </span>
                  </td>
                  <td>
                    <span className={`status-pill ${claim.status === 'APPROVED' ? 'status-approved' : claim.status === 'REJECTED' ? 'status-rejected' : claim.status === 'REVISION_REQUIRED' ? 'status-revision' : 'status-waiting'}`}>
                      {claim.status}
                    </span>
                  </td>
                  <td style={{ maxWidth: '200px', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                    {claim.status === 'REVISION_REQUIRED' ? (
                      <span style={{ color: '#c084fc' }}>Revision requested by Manager.</span>
                    ) : claim.status === 'REJECTED' ? (
                      <span style={{ color: 'var(--danger)' }}>Claim rejected.</span>
                    ) : (
                      <span style={{ color: 'var(--text-dim)' }}>None</span>
                    )}
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: '0.5rem' }}>
                      <button className="btn btn-secondary btn-sm" onClick={() => onSelectClaim(claim.id)}>
                        <Eye size={12} /> View
                      </button>
                      {claim.status === 'REVISION_REQUIRED' && (
                        <button className="btn btn-warning btn-sm" onClick={() => handleOpenResubmit(claim)}>
                          <Edit3 size={12} /> Resubmit
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan="9" style={{ textAlign: 'center', padding: '3rem', color: 'var(--text-dim)' }}>
                  {loading ? 'Loading claims...' : 'No claims submitted yet.'}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Submit Claim Modal */}
      {showSubmitModal && (
        <div className="modal-overlay" onClick={() => setShowSubmitModal(false)}>
          <div className="modal-container" style={{ maxWidth: '600px' }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2 style={{ fontSize: '1.15rem', fontWeight: 800 }}>Submit New Expense Claim</h2>
              <button onClick={() => setShowSubmitModal(false)} style={{ background: 'transparent', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}>
                &times;
              </button>
            </div>
            <form onSubmit={handleSubmitClaim}>
              <div className="modal-body" style={{ gap: '1rem' }}>
                <div>
                  <label style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-dim)' }}>Category</label>
                  <select
                    className="filter-select"
                    style={{ width: '100%', marginTop: '0.25rem' }}
                    value={formData.category}
                    onChange={(e) => setFormData({ ...formData, category: e.target.value })}
                  >
                    <option value="Meals">Meals (Policy limit: 15,000 LKR)</option>
                    <option value="Hotel">Hotel (Policy limit: 50,000 LKR)</option>
                    <option value="Travel">Travel (Policy limit: 20,000 LKR)</option>
                    <option value="Software">Software (Policy limit: 35,000 LKR)</option>
                    <option value="Entertainment">Entertainment (Policy limit: 50,000 LKR)</option>
                  </select>
                </div>

                <div>
                  <label style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-dim)' }}>Merchant Name</label>
                  <input
                    type="text"
                    className="search-input"
                    style={{ width: '100%', marginTop: '0.25rem' }}
                    placeholder="e.g. Cinnamon Grand, ABC Hotel, Uber"
                    value={formData.merchantName}
                    onChange={(e) => setFormData({ ...formData, merchantName: e.target.value })}
                  />
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
                  <div>
                    <label style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-dim)' }}>Total Amount (LKR)</label>
                    <input
                      type="number"
                      step="0.01"
                      className="search-input"
                      style={{ width: '100%', marginTop: '0.25rem' }}
                      placeholder="e.g. 12500"
                      value={formData.amount}
                      onChange={(e) => setFormData({ ...formData, amount: e.target.value })}
                    />
                  </div>
                  <div>
                    <label style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-dim)' }}>Expense Date</label>
                    <input
                      type="date"
                      className="search-input"
                      style={{ width: '100%', marginTop: '0.25rem' }}
                      value={formData.claimDate}
                      onChange={(e) => setFormData({ ...formData, claimDate: e.target.value })}
                    />
                  </div>
                </div>

                <div>
                  <label style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-dim)' }}>Receipt URL</label>
                  <input
                    type="text"
                    className="search-input"
                    style={{ width: '100%', marginTop: '0.25rem' }}
                    placeholder="Receipt document link (Leave empty to test missing receipt violation)"
                    value={formData.receiptUrl}
                    onChange={(e) => setFormData({ ...formData, receiptUrl: e.target.value })}
                  />
                </div>

                <div>
                  <label style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-dim)' }}>Business Purpose & Description</label>
                  <textarea
                    className="search-input"
                    style={{ width: '100%', marginTop: '0.25rem', minHeight: '70px' }}
                    placeholder="Describe business reason for spend..."
                    value={formData.description}
                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                  />
                </div>
              </div>

              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setShowSubmitModal(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary">
                  <Send size={16} /> Submit Claim to Validation Engine
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Resubmit Modal */}
      {resubmitClaimItem && (
        <div className="modal-overlay" onClick={() => setResubmitClaimItem(null)}>
          <div className="modal-container" style={{ maxWidth: '600px' }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2 style={{ fontSize: '1.15rem', fontWeight: 800 }}>Resubmit Claim #{resubmitClaimItem.claimNumber}</h2>
              <button onClick={() => setResubmitClaimItem(null)} style={{ background: 'transparent', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}>
                &times;
              </button>
            </div>
            <form onSubmit={handleResubmitClaim}>
              <div className="modal-body" style={{ gap: '1rem' }}>
                {resubmitClaimItem.latestRevisionComment && (
                  <div style={{ padding: '0.75rem', background: 'rgba(168, 85, 247, 0.1)', border: '1px solid rgba(168, 85, 247, 0.3)', borderRadius: 'var(--radius-sm)', fontSize: '0.825rem' }}>
                    <strong style={{ color: '#c084fc' }}>Manager Instruction:</strong> {resubmitClaimItem.latestRevisionComment}
                  </div>
                )}

                <div>
                  <label style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-dim)' }}>Updated Amount</label>
                  <input
                    type="number"
                    step="0.01"
                    className="search-input"
                    style={{ width: '100%', marginTop: '0.25rem' }}
                    value={resubmitData.amount}
                    onChange={(e) => setResubmitData({ ...resubmitData, amount: e.target.value })}
                  />
                </div>

                <div>
                  <label style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-dim)' }}>Receipt URL</label>
                  <input
                    type="text"
                    className="search-input"
                    style={{ width: '100%', marginTop: '0.25rem' }}
                    value={resubmitData.receiptUrl}
                    onChange={(e) => setResubmitData({ ...resubmitData, receiptUrl: e.target.value })}
                  />
                </div>

                <div>
                  <label style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-dim)' }}>Employee Explanation / Revision Notes</label>
                  <textarea
                    className="search-input"
                    style={{ width: '100%', marginTop: '0.25rem', minHeight: '70px' }}
                    placeholder="Explain what changes were made in response to manager feedback..."
                    value={resubmitData.resubmissionNotes}
                    onChange={(e) => setResubmitData({ ...resubmitData, resubmissionNotes: e.target.value })}
                    required
                  />
                </div>
              </div>

              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setResubmitClaimItem(null)}>Cancel</button>
                <button type="submit" className="btn btn-warning">
                  <Send size={16} /> Resubmit for Re-evaluation
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
