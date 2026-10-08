import React, { useState } from 'react';
import {
  X,
  CheckCircle,
  XCircle,
  HelpCircle,
  AlertTriangle,
  FileText,
  ShieldAlert,
  Copy,
  Clock,
  ExternalLink,
  ChevronRight,
  TrendingUp,
} from 'lucide-react';

export default function ClaimReviewModal({
  claim,
  currentUser,
  onClose,
  onApprove,
  onReject,
  onRequestRevision,
}) {
  const [activeTab, setActiveTab] = useState('assessment'); // 'assessment', 'audit', 'items'
  const [actionType, setActionType] = useState(null); // 'approve', 'reject', 'revision'
  const [commentText, setCommentText] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  if (!claim) return null;

  const isManagerOrAdmin = currentUser?.role === 'Manager' || currentUser?.role === 'Admin';
  const isFinance = currentUser?.role === 'Finance';
  const canDecide = isManagerOrAdmin && claim.status === 'WAITING_FOR_MANAGER_APPROVAL';

  const risk = claim.riskAssessment;
  const riskScore = risk?.riskScore || claim.riskScore || 0;

  const getScoreColorClass = (score) => {
    if (score >= 75) return 'danger';
    if (score >= 40) return 'warning';
    return 'success';
  };

  const handleActionSubmit = async () => {
    if ((actionType === 'reject' || actionType === 'revision') && !commentText.trim()) {
      setErrorMsg(`A specific explanation is required when ${actionType === 'reject' ? 'rejecting' : 'requesting revision'}.`);
      return;
    }

    try {
      setIsSubmitting(true);
      setErrorMsg('');

      if (actionType === 'approve') {
        await onApprove(claim.id, commentText);
      } else if (actionType === 'reject') {
        await onReject(claim.id, commentText);
      } else if (actionType === 'revision') {
        await onRequestRevision(claim.id, commentText);
      }
      onClose();
    } catch (err) {
      setErrorMsg(err.message || 'Operation failed.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-container" onClick={(e) => e.stopPropagation()}>
        {/* Modal Header */}
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
            <span style={{ fontSize: '1.25rem', fontWeight: 800 }}>{claim.claimNumber}</span>
            <span className="badge badge-muted">{claim.category}</span>
            <span className="status-pill status-waiting">{claim.status}</span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
            <div style={{ textAlign: 'right' }}>
              <div style={{ fontSize: '1.25rem', fontWeight: 800 }}>
                {claim.currency} {claim.totalAmount.toLocaleString()}
              </div>
              <div style={{ fontSize: '0.75rem', color: 'var(--text-dim)' }}>
                {claim.merchantName} • {new Date(claim.claimDate).toLocaleDateString()}
              </div>
            </div>
            <button
              onClick={onClose}
              style={{ background: 'transparent', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
            >
              <X size={24} />
            </button>
          </div>
        </div>

        {/* Modal Sub-Navigation Tabs */}
        <div style={{ padding: '0.75rem 1.75rem 0', background: 'rgba(255,255,255,0.02)', borderBottom: '1px solid var(--border-color)', display: 'flex', gap: '1rem' }}>
          <button
            className={`tab-btn ${activeTab === 'assessment' ? 'active' : ''}`}
            onClick={() => setActiveTab('assessment')}
          >
            <ShieldAlert size={16} />
            Policy & Risk Assessment
          </button>
          <button
            className={`tab-btn ${activeTab === 'items' ? 'active' : ''}`}
            onClick={() => setActiveTab('items')}
          >
            <FileText size={16} />
            Expense Items ({claim.items?.length || 0})
          </button>
          <button
            className={`tab-btn ${activeTab === 'audit' ? 'active' : ''}`}
            onClick={() => setActiveTab('audit')}
          >
            <Clock size={16} />
            Audit Trail ({claim.auditLogs?.length || 0})
          </button>
        </div>

        {/* Modal Body */}
        <div className="modal-body">
          {errorMsg && (
            <div style={{ padding: '0.75rem 1rem', background: 'var(--danger-bg)', border: '1px solid var(--danger-border)', color: '#f87171', borderRadius: 'var(--radius-sm)', fontSize: '0.85rem' }}>
              {errorMsg}
            </div>
          )}

          {activeTab === 'assessment' && (
            <div className="review-grid">
              {/* Left Column: Claim & Policy Violations */}
              <div>
                {/* Employee Info Card */}
                <div className="review-card" style={{ marginBottom: '1.25rem' }}>
                  <div className="review-card-title">Employee & Submitter Details</div>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', fontSize: '0.85rem' }}>
                    <div>
                      <span style={{ color: 'var(--text-dim)' }}>Employee:</span>
                      <div style={{ fontWeight: 600 }}>{claim.employeeName}</div>
                    </div>
                    <div>
                      <span style={{ color: 'var(--text-dim)' }}>Department:</span>
                      <div style={{ fontWeight: 600 }}>{claim.departmentName}</div>
                    </div>
                    <div>
                      <span style={{ color: 'var(--text-dim)' }}>Expense Date:</span>
                      <div>{new Date(claim.claimDate).toLocaleDateString()}</div>
                    </div>
                    <div>
                      <span style={{ color: 'var(--text-dim)' }}>Submitted:</span>
                      <div>{claim.submittedAt ? new Date(claim.submittedAt).toLocaleString() : 'N/A'}</div>
                    </div>
                  </div>
                  <div style={{ marginTop: '0.75rem', paddingTop: '0.75rem', borderTop: '1px solid var(--border-color)', fontSize: '0.85rem' }}>
                    <span style={{ color: 'var(--text-dim)' }}>Claim Description:</span>
                    <div style={{ marginTop: '0.25rem', color: 'var(--text-main)' }}>{claim.description || 'No description provided.'}</div>
                  </div>
                  {claim.latestRevisionComment && (
                    <div style={{ marginTop: '0.75rem', padding: '0.5rem 0.75rem', background: 'rgba(168, 85, 247, 0.1)', border: '1px solid rgba(168, 85, 247, 0.3)', borderRadius: 'var(--radius-sm)', fontSize: '0.8rem' }}>
                      <strong style={{ color: '#c084fc' }}>Revision Instruction:</strong> {claim.latestRevisionComment}
                    </div>
                  )}
                </div>

                {/* Deterministic Policy Engine Results */}
                <div className="review-card">
                  <div className="review-card-title">
                    <span>Deterministic Policy Checks</span>
                    <span className={claim.violations && claim.violations.length > 0 ? 'badge badge-danger' : 'badge badge-success'}>
                      {claim.violations && claim.violations.length > 0 ? `${claim.violations.length} Violations` : 'Passed All Rules'}
                    </span>
                  </div>

                  {claim.violations && claim.violations.length > 0 ? (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                      {claim.violations.map((v) => (
                        <div key={v.id} style={{ padding: '0.75rem', background: 'var(--danger-bg)', border: '1px solid var(--danger-border)', borderRadius: 'var(--radius-sm)', fontSize: '0.825rem' }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.25rem' }}>
                            <span style={{ fontWeight: 700, color: '#f87171' }}>{v.ruleCode}</span>
                            <span className="badge badge-danger">{v.severity}</span>
                          </div>
                          <div style={{ color: 'var(--text-main)', marginBottom: '0.35rem' }}>{v.message}</div>
                          <div style={{ display: 'flex', gap: '1rem', fontSize: '0.75rem', color: 'var(--text-dim)' }}>
                            <span>Actual: <strong style={{ color: '#fca5a5' }}>{v.actualValue}</strong></span>
                            <span>Allowed: <strong style={{ color: '#86efac' }}>{v.allowedValue}</strong></span>
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '1rem', background: 'rgba(16, 185, 129, 0.05)', border: '1px solid rgba(16, 185, 129, 0.2)', borderRadius: 'var(--radius-sm)' }}>
                      <CheckCircle size={20} color="var(--success)" />
                      <div style={{ fontSize: '0.85rem', color: 'var(--text-main)' }}>
                        All deterministic spending policies (maximum limits, category eligibility, currency, receipts, and department budgets) are fully satisfied.
                      </div>
                    </div>
                  )}
                </div>
              </div>

              {/* Right Column: Fraud/Anomaly-Risk Agent Subsystem */}
              <div>
                <div className="review-card">
                  <div className="review-card-title">
                    <span>Fraud / Anomaly-Risk Agent Analysis</span>
                    <span className="brand-badge" style={{ fontSize: '0.65rem' }}>Agentic Subsystem</span>
                  </div>

                  {/* Score Gauge */}
                  <div className="agent-score-container">
                    <div className={`score-circle ${getScoreColorClass(riskScore)}`}>
                      {riskScore}
                    </div>
                    <div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.25rem' }}>
                        <span style={{ fontWeight: 800, fontSize: '1.1rem' }}>{risk?.riskLevel || claim.riskStatus}</span>
                      </div>
                      <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                        Evaluated by <code style={{ color: 'var(--primary)' }}>{risk?.agentVersion || 'FraudAnomalyRiskAgent-v1.2'}</code>
                      </div>
                    </div>
                  </div>

                  {/* Agent Summary */}
                  <div style={{ padding: '0.75rem 1rem', background: 'rgba(255,255,255,0.02)', border: '1px solid var(--border-color)', borderRadius: 'var(--radius-sm)', fontSize: '0.85rem', marginBottom: '1rem', lineHeight: '1.4' }}>
                    <div style={{ fontWeight: 600, color: 'var(--text-muted)', marginBottom: '0.25rem', fontSize: '0.75rem', textTransform: 'uppercase' }}>
                      Agent Synthesis & Reason Summary
                    </div>
                    {risk?.reasonSummary || 'Assessment completed with baseline scoring.'}
                  </div>

                  {/* Signals List */}
                  <div style={{ marginBottom: '1rem' }}>
                    <div style={{ fontSize: '0.75rem', fontWeight: 700, textTransform: 'uppercase', color: 'var(--text-dim)', marginBottom: '0.5rem' }}>
                      Detected Anomaly & Risk Signals
                    </div>
                    {risk?.signals && risk.signals.length > 0 ? (
                      risk.signals.map((sig, idx) => (
                        <div key={idx} className={`signal-box ${sig.severity === 'CRITICAL' ? '' : sig.severity === 'HIGH' ? 'warning' : 'medium'}`}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.2rem' }}>
                            <strong style={{ color: sig.severity === 'CRITICAL' ? 'var(--danger)' : 'var(--warning)' }}>
                              {sig.type}
                            </strong>
                            <span style={{ fontSize: '0.7rem', fontWeight: 700, textTransform: 'uppercase' }}>{sig.severity}</span>
                          </div>
                          <div style={{ color: 'var(--text-main)', fontSize: '0.8rem' }}>{sig.evidence}</div>
                        </div>
                      ))
                    ) : (
                      <div style={{ fontSize: '0.8rem', color: 'var(--text-dim)', padding: '0.5rem' }}>
                        No anomalous signals or suspicious spending patterns flagged by agent.
                      </div>
                    )}
                  </div>

                  {/* Duplicate Matches Box */}
                  {claim.duplicateMatches && claim.duplicateMatches.length > 0 && (
                    <div style={{ marginTop: '1rem', borderTop: '1px solid var(--border-color)', paddingTop: '1rem' }}>
                      <div style={{ fontSize: '0.75rem', fontWeight: 700, textTransform: 'uppercase', color: 'var(--danger)', marginBottom: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
                        <Copy size={14} /> Duplicate Claim Candidate Matches
                      </div>
                      {claim.duplicateMatches.map((dm) => (
                        <div key={dm.id} style={{ padding: '0.75rem', background: 'rgba(239, 68, 68, 0.08)', border: '1px solid var(--danger-border)', borderRadius: 'var(--radius-sm)', fontSize: '0.8rem', marginBottom: '0.5rem' }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', fontWeight: 700, color: 'var(--danger)' }}>
                            <span>Matching Claim: #{dm.matchingClaimNumber}</span>
                            <span>Similarity: {Math.round(dm.similarityScore * 100)}%</span>
                          </div>
                          <div style={{ margin: '0.25rem 0', color: 'var(--text-main)' }}>{dm.evidence}</div>
                          <div style={{ fontSize: '0.72rem', color: 'var(--text-dim)' }}>
                            Amount: {claim.currency} {dm.matchingAmount.toLocaleString()} • Date: {new Date(dm.matchingClaimDate).toLocaleDateString()}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}

          {activeTab === 'items' && (
            <div className="review-card">
              <div className="review-card-title">Itemized Line Items & Receipts</div>
              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Category</th>
                      <th>Merchant</th>
                      <th>Description</th>
                      <th>Amount</th>
                      <th>Receipt Document</th>
                    </tr>
                  </thead>
                  <tbody>
                    {claim.items && claim.items.length > 0 ? (
                      claim.items.map((item) => (
                        <tr key={item.id}>
                          <td>{new Date(item.expenseDate).toLocaleDateString()}</td>
                          <td><span className="badge badge-muted">{item.category}</span></td>
                          <td style={{ fontWeight: 600 }}>{item.merchant}</td>
                          <td>{item.description}</td>
                          <td style={{ fontWeight: 700 }}>{item.currency} {item.amount.toLocaleString()}</td>
                          <td>
                            {item.receiptUrl ? (
                              <a
                                href={item.receiptUrl}
                                target="_blank"
                                rel="noreferrer"
                                style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem', color: 'var(--primary)', textDecoration: 'none', fontWeight: 600 }}
                              >
                                View Receipt <ExternalLink size={12} />
                              </a>
                            ) : (
                              <span style={{ color: 'var(--danger)', fontSize: '0.8rem', fontWeight: 600 }}>Missing Receipt</span>
                            )}
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td colSpan="6" style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-dim)' }}>
                          No individual line items attached.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {activeTab === 'audit' && (
            <div className="review-card">
              <div className="review-card-title">Chronological Compliance Audit Trail</div>
              <div className="timeline">
                {claim.auditLogs && claim.auditLogs.length > 0 ? (
                  claim.auditLogs.map((log) => (
                    <div key={log.id} className="timeline-item">
                      <div className="timeline-dot" />
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <span style={{ fontWeight: 700, color: 'var(--text-main)' }}>{log.action}</span>
                        <span className="timeline-time">
                          {new Date(log.timestamp).toLocaleString()}
                        </span>
                      </div>
                      <div className="timeline-content">
                        <div>{log.details}</div>
                        <div style={{ marginTop: '0.25rem', fontSize: '0.72rem', color: 'var(--text-dim)', display: 'flex', gap: '1rem' }}>
                          <span>Actor: <strong style={{ color: 'var(--text-muted)' }}>{log.actorName || log.actorRole}</strong></span>
                          {log.oldStatus && <span>Transition: {log.oldStatus} &rarr; {log.newStatus}</span>}
                          <span>Correlation: <code>{log.correlationId?.substring(0, 8)}</code></span>
                        </div>
                      </div>
                    </div>
                  ))
                ) : (
                  <div style={{ color: 'var(--text-dim)' }}>No audit events logged.</div>
                )}
              </div>
            </div>
          )}

          {/* Action Input Box (Approve / Reject / Revision) */}
          {actionType && (
            <div style={{ background: 'var(--bg-elevated)', border: '1px solid var(--primary)', borderRadius: 'var(--radius-md)', padding: '1.25rem', marginTop: '1rem' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
                <span style={{ fontWeight: 700, textTransform: 'uppercase', fontSize: '0.85rem', color: actionType === 'approve' ? 'var(--success)' : actionType === 'reject' ? 'var(--danger)' : '#c084fc' }}>
                  {actionType === 'approve' ? 'Confirm Approval' : actionType === 'reject' ? 'Reject Claim (Reason Required)' : 'Request Revision (Feedback Required)'}
                </span>
                <button onClick={() => setActionType(null)} style={{ background: 'transparent', border: 'none', color: 'var(--text-dim)', cursor: 'pointer' }}>
                  <X size={16} />
                </button>
              </div>

              <textarea
                style={{
                  width: '100%',
                  background: 'var(--bg-dark)',
                  border: '1px solid var(--border-color)',
                  color: 'var(--text-main)',
                  borderRadius: 'var(--radius-sm)',
                  padding: '0.75rem',
                  fontSize: '0.85rem',
                  minHeight: '80px',
                  resize: 'vertical',
                  outline: 'none',
                }}
                placeholder={
                  actionType === 'approve'
                    ? 'Optional manager approval comment...'
                    : actionType === 'reject'
                    ? 'State the specific business policy reason for rejecting this claim...'
                    : 'Detail the exact corrections or missing documents needed from the employee...'
                }
                value={commentText}
                onChange={(e) => setCommentText(e.target.value)}
              />

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '0.75rem' }}>
                <button className="btn btn-secondary btn-sm" onClick={() => setActionType(null)}>Cancel</button>
                <button
                  className={`btn btn-sm ${actionType === 'approve' ? 'btn-success' : actionType === 'reject' ? 'btn-danger' : 'btn-warning'}`}
                  onClick={handleActionSubmit}
                  disabled={isSubmitting}
                >
                  {isSubmitting ? 'Processing...' : `Submit ${actionType === 'approve' ? 'Approval' : actionType === 'reject' ? 'Rejection' : 'Revision Request'}`}
                </button>
              </div>
            </div>
          )}
        </div>

        {/* Modal Footer Controls */}
        <div className="modal-footer">
          <div>
            {!canDecide ? (
              <span style={{ fontSize: '0.8rem', color: 'var(--text-dim)' }}>
                {!isManagerOrAdmin
                  ? `Logged in as ${currentUser?.role || 'Employee'}. Switch to Manager Sarah or David to submit approval decisions.`
                  : `Claim status is ${claim.status}. Action not required.`}
              </span>
            ) : (
              <span style={{ fontSize: '0.8rem', color: 'var(--warning)', fontWeight: 600 }}>
                Workflow is paused. Authorized manager decision required.
              </span>
            )}
          </div>

          <div style={{ display: 'flex', gap: '0.75rem' }}>
            <button className="btn btn-secondary" onClick={onClose}>Close</button>

            {canDecide && (
              <>
                <button
                  className="btn btn-warning"
                  onClick={() => { setActionType('revision'); setCommentText(''); }}
                  disabled={actionType !== null}
                >
                  <HelpCircle size={16} /> Request Revision
                </button>

                <button
                  className="btn btn-danger"
                  onClick={() => { setActionType('reject'); setCommentText(''); }}
                  disabled={actionType !== null}
                >
                  <XCircle size={16} /> Reject
                </button>

                <button
                  className="btn btn-success"
                  onClick={() => { setActionType('approve'); setCommentText(''); }}
                  disabled={actionType !== null}
                >
                  <CheckCircle size={16} /> Approve
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
