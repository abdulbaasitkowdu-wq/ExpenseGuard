import React from 'react';
import { ShieldCheck, UserCheck, AlertTriangle, FileText, CheckCircle, RefreshCw, Layers } from 'lucide-react';

export const DEMO_USERS = [
  { username: 'sarah.chen', name: 'Sarah Chen', role: 'Manager', dept: 'Sales & Marketing' },
  { username: 'david.miller', name: 'David Miller', role: 'Manager', dept: 'Engineering' },
  { username: 'john.doe', name: 'John Doe', role: 'Employee', dept: 'Sales & Marketing' },
  { username: 'jane.smith', name: 'Jane Smith', role: 'Employee', dept: 'Engineering' },
  { username: 'michael.scott', name: 'Michael Scott', role: 'Finance', dept: 'Operations' },
];

export default function Navbar({ activeTab, setActiveTab, currentUser, onSwitchUser, onRefresh }) {
  return (
    <header className="navbar">
      <div className="nav-brand">
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <ShieldCheck size={28} color="#6366f1" />
          <span style={{ fontSize: '1.2rem', fontWeight: 800 }}>SpendGuard</span>
        </div>
        <span className="brand-badge">Policy & Compliance</span>
      </div>

      <nav className="tabs-container">
        <button
          className={`tab-btn ${activeTab === 'dashboard' ? 'active' : ''}`}
          onClick={() => setActiveTab('dashboard')}
        >
          <Layers size={16} />
          Dashboard
        </button>
        <button
          className={`tab-btn ${activeTab === 'review-queue' ? 'active' : ''}`}
          onClick={() => setActiveTab('review-queue')}
        >
          <AlertTriangle size={16} />
          Review Queue
        </button>
        <button
          className={`tab-btn ${activeTab === 'employee' ? 'active' : ''}`}
          onClick={() => setActiveTab('employee')}
        >
          <FileText size={16} />
          Employee Portal
        </button>
        <button
          className={`tab-btn ${activeTab === 'policies' ? 'active' : ''}`}
          onClick={() => setActiveTab('policies')}
        >
          <CheckCircle size={16} />
          Policies
        </button>
      </nav>

      <div className="nav-actions">
        <div className="role-switcher">
          <UserCheck size={16} color="#94a3b8" />
          <span style={{ color: 'var(--text-dim)', fontSize: '0.8rem' }}>Role:</span>
          <select
            value={currentUser?.username || 'sarah.chen'}
            onChange={(e) => onSwitchUser(e.target.value)}
          >
            {DEMO_USERS.map((u) => (
              <option key={u.username} value={u.username}>
                {u.name} ({u.role} - {u.dept})
              </option>
            ))}
          </select>
        </div>

        <button
          className="btn btn-secondary btn-sm"
          onClick={onRefresh}
          title="Refresh live data"
        >
          <RefreshCw size={14} />
          Refresh
        </button>
      </div>
    </header>
  );
}
