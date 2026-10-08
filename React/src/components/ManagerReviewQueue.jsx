import React, { useState } from 'react';
import { Search, Filter, AlertTriangle, ShieldCheck, Copy, ArrowRight, Eye } from 'lucide-react';

export default function ManagerReviewQueue({ claims, onSelectClaim, onFilterChange, filters }) {
  const [searchTerm, setSearchTerm] = useState(filters.searchTerm || '');

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    onFilterChange({ ...filters, searchTerm });
  };

  const getRiskBadge = (riskStatus, riskScore) => {
    switch (riskStatus) {
      case 'REVIEW_REQUIRED':
        return <span className="badge badge-review-required">REVIEW REQUIRED ({riskScore || 0})</span>;
      case 'HIGH_RISK':
        return <span className="badge badge-high-risk">HIGH RISK ({riskScore || 0})</span>;
      case 'MEDIUM_RISK':
        return <span className="badge badge-medium-risk">MEDIUM RISK ({riskScore || 0})</span>;
      case 'LOW_RISK':
        return <span className="badge badge-low-risk">LOW RISK ({riskScore || 0})</span>;
      default:
        return <span className="badge badge-muted">NOT ASSESSED</span>;
    }
  };

  const getPolicyBadge = (status, count) => {
    if (status === 'VIOLATIONS_FOUND') {
      return <span className="badge badge-danger">{count} Violations</span>;
    }
    return <span className="badge badge-success">Compliant</span>;
  };

  const getStatusPill = (status) => {
    switch (status) {
      case 'WAITING_FOR_MANAGER_APPROVAL':
        return <span className="status-pill status-waiting">Waiting Approval</span>;
      case 'REVISION_REQUIRED':
        return <span className="status-pill status-revision">Revision Required</span>;
      case 'APPROVED':
        return <span className="status-pill status-approved">Approved</span>;
      case 'REJECTED':
        return <span className="status-pill status-rejected">Rejected</span>;
      case 'REIMBURSED':
        return <span className="status-pill status-approved">Reimbursed</span>;
      default:
        return <span className="status-pill status-waiting">{status}</span>;
    }
  };

  return (
    <div>
      <div style={{ marginBottom: '1.5rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1 style={{ fontSize: '1.75rem', fontWeight: 800, letterSpacing: '-0.02em' }}>
            Manager Review Queue
          </h1>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>
            Authorized managerial decision queue for claims paused by policy validation or Fraud/Anomaly-Risk Agent.
          </p>
        </div>
      </div>

      {/* Filter Toolbar */}
      <div className="glass-panel toolbar-panel">
        <form onSubmit={handleSearchSubmit} style={{ display: 'flex', gap: '0.5rem', flex: '1', minWidth: '260px' }}>
          <input
            type="text"
            className="search-input"
            placeholder="Search claim #, employee, merchant..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            style={{ width: '100%' }}
          />
          <button type="submit" className="btn btn-secondary btn-sm">
            <Search size={14} /> Search
          </button>
        </form>

        <div className="filter-group">
          {/* Risk Level Filter */}
          <select
            className="filter-select"
            value={filters.riskLevel || ''}
            onChange={(e) => onFilterChange({ ...filters, riskLevel: e.target.value })}
          >
            <option value="">All Risk Levels</option>
            <option value="REVIEW_REQUIRED">Review Required</option>
            <option value="HIGH_RISK">High Risk</option>
            <option value="MEDIUM_RISK">Medium Risk</option>
            <option value="LOW_RISK">Low Risk</option>
          </select>

          {/* Policy Status Filter */}
          <select
            className="filter-select"
            value={filters.policyStatus || ''}
            onChange={(e) => onFilterChange({ ...filters, policyStatus: e.target.value })}
          >
            <option value="">All Policy Status</option>
            <option value="VIOLATIONS_FOUND">Violations Found</option>
            <option value="COMPLIANT">Compliant</option>
          </select>

          {/* Status Filter */}
          <select
            className="filter-select"
            value={filters.status || ''}
            onChange={(e) => onFilterChange({ ...filters, status: e.target.value })}
          >
            <option value="">Active (Waiting Approval & Revision)</option>
            <option value="WAITING_FOR_MANAGER_APPROVAL">Waiting Approval Only</option>
            <option value="REVISION_REQUIRED">Revision Required Only</option>
            <option value="APPROVED">Approved Claims</option>
            <option value="REJECTED">Rejected Claims</option>
          </select>

          {/* Category Filter */}
          <select
            className="filter-select"
            value={filters.category || ''}
            onChange={(e) => onFilterChange({ ...filters, category: e.target.value })}
          >
            <option value="">All Categories</option>
            <option value="Meals">Meals</option>
            <option value="Hotel">Hotel</option>
            <option value="Travel">Travel</option>
            <option value="Software">Software</option>
            <option value="Entertainment">Entertainment</option>
          </select>

          <button
            className="btn btn-secondary btn-sm"
            onClick={() => onFilterChange({})}
            title="Reset Filters"
          >
            Reset
          </button>
        </div>
      </div>

      {/* Table */}
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Claim Number</th>
              <th>Employee & Dept</th>
              <th>Amount</th>
              <th>Category</th>
              <th>Policy Status</th>
              <th>Risk Level</th>
              <th>Signals</th>
              <th>Submitted Date</th>
              <th>Status</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {claims && claims.length > 0 ? (
              claims.map((claim) => (
                <tr key={claim.id}>
                  <td style={{ fontWeight: 700 }}>
                    <div style={{ color: 'var(--primary)' }}>{claim.claimNumber}</div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-dim)' }}>{claim.merchantName}</div>
                  </td>
                  <td>
                    <div style={{ fontWeight: 600 }}>{claim.employeeName}</div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>{claim.departmentName}</div>
                  </td>
                  <td style={{ fontWeight: 700 }}>
                    {claim.currency} {claim.totalAmount.toLocaleString()}
                  </td>
                  <td>
                    <span className="badge badge-muted">{claim.category}</span>
                  </td>
                  <td>{getPolicyBadge(claim.policyStatus, claim.violationCount)}</td>
                  <td>{getRiskBadge(claim.riskStatus, claim.riskScore)}</td>
                  <td>
                    {claim.duplicateCount > 0 && (
                      <span className="badge badge-danger" title="Duplicate match found">
                        <Copy size={12} /> {claim.duplicateCount} Dup
                      </span>
                    )}
                  </td>
                  <td style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                    {new Date(claim.claimDate).toLocaleDateString()}
                  </td>
                  <td>{getStatusPill(claim.status)}</td>
                  <td>
                    <button
                      className="btn btn-primary btn-sm"
                      onClick={() => onSelectClaim(claim.id)}
                    >
                      <Eye size={14} /> Review
                    </button>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan="10" style={{ textAlign: 'center', padding: '3rem', color: 'var(--text-dim)' }}>
                  No claims found matching selected filter criteria.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
