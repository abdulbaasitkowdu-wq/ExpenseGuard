import React, { useState, useEffect } from 'react';
import { ShieldCheck, Plus, CheckCircle2, AlertCircle } from 'lucide-react';
import { api } from '../services/api';

export default function PoliciesView() {
  const [policies, setPolicies] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadPolicies();
  }, []);

  const loadPolicies = async () => {
    try {
      setLoading(true);
      const data = await api.getPolicies();
      setPolicies(data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <div style={{ marginBottom: '1.5rem' }}>
        <h1 style={{ fontSize: '1.75rem', fontWeight: 800, letterSpacing: '-0.02em' }}>
          Corporate Expense Policies & Limits
        </h1>
        <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>
          Configured spending limits evaluated by the deterministic policy validation engine.
        </p>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Policy Name</th>
              <th>Category</th>
              <th>Scope</th>
              <th>Maximum Limit</th>
              <th>Requires Receipt</th>
              <th>Requires Manager Sign-Off</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {policies && policies.length > 0 ? (
              policies.map((p) => (
                <tr key={p.id}>
                  <td style={{ fontWeight: 700, color: 'var(--text-main)' }}>{p.policyName}</td>
                  <td><span className="badge badge-muted">{p.category}</span></td>
                  <td>{p.isGlobalPolicy ? 'Global Company Policy' : p.departmentName || 'Department'}</td>
                  <td style={{ fontWeight: 700 }}>
                    {p.allowedCurrency} {p.maximumAmount.toLocaleString()}
                  </td>
                  <td>
                    {p.requiresReceipt ? (
                      <span className="badge badge-danger">Mandatory</span>
                    ) : (
                      <span className="badge badge-muted">Optional</span>
                    )}
                  </td>
                  <td>
                    {p.requiresManagerApproval ? (
                      <span className="badge badge-high-risk">Required</span>
                    ) : (
                      <span className="badge badge-success">Auto-cleared if compliant</span>
                    )}
                  </td>
                  <td>
                    <span className="badge badge-success">Active</span>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan="7" style={{ textAlign: 'center', padding: '3rem', color: 'var(--text-dim)' }}>
                  {loading ? 'Loading policies...' : 'No policies configured.'}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
