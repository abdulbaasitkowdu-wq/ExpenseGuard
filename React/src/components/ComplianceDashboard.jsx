import React from 'react';
import { AlertCircle, ShieldAlert, FileClock, CheckCheck, DollarSign, ArrowUpRight, Clock } from 'lucide-react';

export default function ComplianceDashboard({ stats, onSelectClaim, onNavigateQueue }) {
  if (!stats) return <div style={{ padding: '2rem', textAlign: 'center' }}>Loading dashboard metrics...</div>;

  return (
    <div>
      <div style={{ marginBottom: '1.75rem' }}>
        <h1 style={{ fontSize: '1.75rem', fontWeight: 800, letterSpacing: '-0.02em' }}>
          Compliance & Risk Intelligence
        </h1>
        <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>
          Real-time deterministic policy enforcement and Fraud/Anomaly-Risk Agent monitoring.
        </p>
      </div>

      {/* Metrics Row */}
      <div className="metrics-grid">
        <div className="glass-panel metric-card alert">
          <div className="metric-title">High-Risk Anomaly Alerts</div>
          <div className="metric-value" style={{ color: 'var(--danger)' }}>
            {stats.highRiskClaims}
          </div>
          <div className="metric-sub">Duplicates, extreme amounts & threshold evasions</div>
        </div>

        <div className="glass-panel metric-card warning">
          <div className="metric-title">Pending Manager Review</div>
          <div className="metric-value" style={{ color: 'var(--warning)' }}>
            {stats.totalPendingReviews}
          </div>
          <div className="metric-sub">Workflows paused for human sign-off</div>
        </div>

        <div className="glass-panel metric-card primary">
          <div className="metric-title">Policy Violations Found</div>
          <div className="metric-value" style={{ color: 'var(--primary)' }}>
            {stats.policyViolationsCount}
          </div>
          <div className="metric-sub">Category limits, missing receipts, budget rules</div>
        </div>

        <div className="glass-panel metric-card warning">
          <div className="metric-title">Claims Awaiting Revision</div>
          <div className="metric-value" style={{ color: '#c084fc' }}>
            {stats.awaitingRevisionCount}
          </div>
          <div className="metric-sub">Sent back to employee with instructions</div>
        </div>

        <div className="glass-panel metric-card success">
          <div className="metric-title">Approved / Disbursed</div>
          <div className="metric-value" style={{ color: 'var(--success)' }}>
            {stats.totalApproved}
          </div>
          <div className="metric-sub">Reimbursements cleared for finance</div>
        </div>

        <div className="glass-panel metric-card primary">
          <div className="metric-title">Total Pending Value</div>
          <div className="metric-value" style={{ fontSize: '1.5rem' }}>
            LKR {stats.totalPendingAmount?.toLocaleString()}
          </div>
          <div className="metric-sub">Under active compliance review</div>
        </div>
      </div>

      {/* Two columns: High-Risk Claims Attention & Recent Audit Trail */}
      <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr', gap: '1.5rem' }}>
        {/* High Risk Attention */}
        <div className="glass-panel" style={{ padding: '1.5rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
              <ShieldAlert size={20} color="var(--danger)" />
              <h2 style={{ fontSize: '1.1rem', fontWeight: 700 }}>Priority Anomaly Claims Requiring Decision</h2>
            </div>
            <button className="btn btn-secondary btn-sm" onClick={onNavigateQueue}>
              View Full Queue <ArrowUpRight size={14} />
            </button>
          </div>

          {stats.recentHighRiskClaims && stats.recentHighRiskClaims.length > 0 ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              {stats.recentHighRiskClaims.map((claim) => (
                <div
                  key={claim.id}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '1rem',
                    background: 'var(--bg-elevated)',
                    border: '1px solid var(--border-color)',
                    borderRadius: 'var(--radius-md)',
                    cursor: 'pointer',
                    transition: 'border-color 0.2s',
                  }}
                  onClick={() => onSelectClaim(claim.id)}
                >
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '0.25rem' }}>
                      <span style={{ fontWeight: 700, color: 'var(--text-main)' }}>{claim.claimNumber}</span>
                      <span className="badge badge-review-required">
                        Score {claim.riskScore}/100 • {claim.riskStatus}
                      </span>
                      {claim.violationCount > 0 && (
                        <span className="badge badge-danger">{claim.violationCount} Violations</span>
                      )}
                    </div>
                    <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                      {claim.employeeName} ({claim.departmentName}) • {claim.category} at {claim.merchantName}
                    </div>
                  </div>

                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontWeight: 700, fontSize: '1rem' }}>
                      {claim.currency} {claim.totalAmount.toLocaleString()}
                    </div>
                    <button className="btn btn-primary btn-sm" style={{ marginTop: '0.25rem' }}>
                      Inspect & Review
                    </button>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-dim)' }}>
              No critical anomalies currently in the review queue.
            </div>
          )}
        </div>

        {/* Live Audit Log Feed */}
        <div className="glass-panel" style={{ padding: '1.5rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', marginBottom: '1.25rem' }}>
            <Clock size={20} color="var(--primary)" />
            <h2 style={{ fontSize: '1.1rem', fontWeight: 700 }}>Live Compliance Audit Stream</h2>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem', maxHeight: '440px', overflowY: 'auto', paddingRight: '0.5rem' }}>
            {stats.recentActivities && stats.recentActivities.length > 0 ? (
              stats.recentActivities.map((log) => (
                <div
                  key={log.id}
                  style={{
                    padding: '0.75rem 0.85rem',
                    background: 'rgba(255, 255, 255, 0.02)',
                    border: '1px solid var(--border-color)',
                    borderRadius: 'var(--radius-sm)',
                    fontSize: '0.825rem',
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.25rem' }}>
                    <span style={{ fontWeight: 700, color: 'var(--primary)' }}>{log.action}</span>
                    <span style={{ fontSize: '0.72rem', color: 'var(--text-dim)' }}>
                      {new Date(log.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                    </span>
                  </div>
                  <div style={{ color: 'var(--text-main)', marginBottom: '0.25rem' }}>{log.details}</div>
                  <div style={{ fontSize: '0.72rem', color: 'var(--text-dim)' }}>
                    Actor: <span style={{ color: 'var(--text-muted)' }}>{log.actorName || log.actorRole}</span>
                  </div>
                </div>
              ))
            ) : (
              <div style={{ color: 'var(--text-dim)', textAlign: 'center', padding: '2rem' }}>No recent audit events</div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
