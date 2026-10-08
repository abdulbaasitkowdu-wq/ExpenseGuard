import { useState, useEffect } from 'react';
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  PieChart, Pie, Cell, LineChart, Line, Legend, AreaChart, Area
} from 'recharts';
import { getFinanceDashboard } from '../services/api';
import { LoadingSpinner, AmountDisplay, StatusBadge } from '../components/Shared';
import { useTheme } from '../auth/ThemeContext';
import { chartAxis, chartTooltipStyle } from '../theme/chartStyles';

const COLORS = ['#6366f1', '#8b5cf6', '#10b981', '#f59e0b', '#ef4444', '#3b82f6'];

export default function FinanceDashboard() {
  const { theme } = useTheme();
  const tooltipStyle = chartTooltipStyle();
  const { tick: axisStroke, grid: gridStroke } = chartAxis(theme);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [fiscalYear, setFiscalYear] = useState(new Date().getFullYear());

  useEffect(() => {
    setLoading(true);
    getFinanceDashboard(fiscalYear)
      .then(r => setData(r.data))
      .catch(() => setData(getMockDashboard()))
      .finally(() => setLoading(false));
  }, [fiscalYear]);

  if (loading) return <div className="page-content"><LoadingSpinner /></div>;

  const d = data || getMockDashboard();

  const statCards = [
    { label: 'Total Budget', value: <AmountDisplay amount={d.totalBudget} large />, sub: `FY${fiscalYear}`, type: 'primary' },
    { label: 'Total Spend', value: <AmountDisplay amount={d.totalSpend} large />, sub: `${d.overallUtilization?.toFixed(1)}% utilised`, type: 'warning' },
    { label: 'Remaining', value: <AmountDisplay amount={d.remaining} large />, sub: 'Available budget', type: 'success' },
    { label: 'Pending', value: d.pendingReimbursements, sub: 'Awaiting processing', type: 'primary' },
    { label: 'Processing', value: d.processingCount, sub: 'In finance queue', type: 'warning' },
    { label: 'Paid', value: d.paidCount, sub: 'Completed payments', type: 'success' },
    { label: 'Failed Payments', value: d.failedPayments, sub: 'Requires attention', type: 'danger' },
  ];

  return (
    <div className="page-content">
      {/* Fiscal Year Selector */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <h2 style={{ fontSize: 22, fontWeight: 800 }}>Finance Dashboard</h2>
          <p style={{ color: 'var(--text-muted)', fontSize: 14 }}>Reimbursement & Budget Overview</p>
        </div>
        <select
          id="fiscal-year-select"
          className="form-control"
          style={{ width: 120 }}
          value={fiscalYear}
          onChange={e => setFiscalYear(Number(e.target.value))}
        >
          {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
        </select>
      </div>

      {/* Budget Alerts */}
      {d.activeAlerts?.length > 0 && (
        <div className="alert alert-warning" style={{ marginBottom: 16 }}>
          ⚠️ {d.activeAlerts.length} active budget alert{d.activeAlerts.length > 1 ? 's' : ''} — {d.activeAlerts[0]?.departmentId} at {d.activeAlerts[0]?.thresholdPercentage}% utilisation
        </div>
      )}

      {/* Stat Cards */}
      <div className="stats-grid">
        {statCards.map((card, i) => (
          <div key={i} className={`stat-card ${card.type}`}>
            <div className="stat-label">{card.label}</div>
            <div className="stat-value">{card.value}</div>
            <div className="stat-sub">{card.sub}</div>
          </div>
        ))}
      </div>

      {/* Charts Row 1 */}
      <div className="chart-grid">
        {/* Department Spend vs Budget */}
        <div className="chart-card" style={{ gridColumn: 'span 2' }}>
          <div className="chart-title">Department: Budget vs Actual Spend</div>
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={d.departmentBreakdown || []} margin={{ left: 0, right: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke={gridStroke} />
              <XAxis dataKey="departmentName" tick={{ fill: axisStroke, fontSize: 12 }} />
              <YAxis tick={{ fill: axisStroke, fontSize: 11 }} tickFormatter={v => `${(v/1e6).toFixed(1)}M`} />
              <Tooltip contentStyle={tooltipStyle} formatter={v => [`LKR ${v.toLocaleString()}`, '']} />
              <Legend wrapperStyle={{ fontSize: 12, color: '#94a3b8' }} />
              <Bar dataKey="allocatedBudget" name="Allocated" fill="#6366f1" radius={[4, 4, 0, 0]} />
              <Bar dataKey="totalSpent" name="Spent" fill="#10b981" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>

        {/* Department Utilization Pie */}
        <div className="chart-card">
          <div className="chart-title">Spend Distribution by Department</div>
          <ResponsiveContainer width="100%" height={260}>
            <PieChart>
              <Pie
                data={d.departmentBreakdown || []}
                dataKey="totalSpent"
                nameKey="departmentName"
                cx="50%" cy="50%"
                innerRadius={60} outerRadius={100}
                paddingAngle={3}
              >
                {(d.departmentBreakdown || []).map((_, i) => (
                  <Cell key={i} fill={COLORS[i % COLORS.length]} />
                ))}
              </Pie>
              <Tooltip contentStyle={tooltipStyle} formatter={v => `LKR ${v.toLocaleString()}`} />
              <Legend wrapperStyle={{ fontSize: 12, color: '#94a3b8' }} />
            </PieChart>
          </ResponsiveContainer>
        </div>

        {/* Monthly Trend */}
        <div className="chart-card">
          <div className="chart-title">Monthly Spending Trend</div>
          <ResponsiveContainer width="100%" height={260}>
            <AreaChart data={d.monthlyTrend || []}>
              <defs>
                <linearGradient id="gradTrend" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#6366f1" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#6366f1" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke={gridStroke} />
              <XAxis dataKey="month" tick={{ fill: axisStroke, fontSize: 11 }} />
              <YAxis tick={{ fill: axisStroke, fontSize: 11 }} tickFormatter={v => `${(v/1e3).toFixed(0)}K`} />
              <Tooltip contentStyle={tooltipStyle} formatter={v => `LKR ${v.toLocaleString()}`} />
              <Area type="monotone" dataKey="totalSpend" stroke="#6366f1" fill="url(#gradTrend)" strokeWidth={2} name="Spend" />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Department Budget Utilization Table */}
      <div className="table-wrapper">
        <div className="table-header">
          <h3>Department Budget Utilization</h3>
        </div>
        <table>
          <thead>
            <tr>
              <th>Department</th>
              <th>Allocated</th>
              <th>Spent</th>
              <th>Remaining</th>
              <th>Utilization</th>
              <th>Claims</th>
            </tr>
          </thead>
          <tbody>
            {(d.departmentBreakdown || []).map((dept, i) => {
              const pct = dept.utilizationPercentage;
              const colorClass = pct >= 90 ? 'progress-high' : pct >= 70 ? 'progress-medium' : 'progress-low';
              return (
                <tr key={i} id={`dept-row-${dept.departmentId}`}>
                  <td><strong>{dept.departmentName}</strong></td>
                  <td><AmountDisplay amount={dept.allocatedBudget} /></td>
                  <td><AmountDisplay amount={dept.totalSpent} /></td>
                  <td style={{ color: dept.remainingBudget < 0 ? 'var(--accent-danger)' : 'var(--accent-success)' }}>
                    <AmountDisplay amount={dept.remainingBudget} />
                  </td>
                  <td style={{ minWidth: 160 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <div className="progress-bar-track" style={{ flex: 1 }}>
                        <div className={`progress-bar-fill ${colorClass}`} style={{ width: `${Math.min(pct, 100)}%` }} />
                      </div>
                      <span style={{ fontSize: 12, minWidth: 38 }}>{pct?.toFixed(1)}%</span>
                    </div>
                  </td>
                  <td>{dept.reimbursementCount}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// Mock data for when API not connected
function getMockDashboard() {
  return {
    totalBudget: 31_500_000, totalSpend: 8_750_000, remaining: 22_750_000,
    pendingReimbursements: 12, processingCount: 3, paidCount: 47, failedPayments: 2,
    overallUtilization: 27.8,
    activeAlerts: [],
    departmentBreakdown: [
      { departmentId: 'DEPT-ENG', departmentName: 'Engineering', allocatedBudget: 10000000, totalSpent: 4200000, remainingBudget: 5800000, utilizationPercentage: 42, reimbursementCount: 18 },
      { departmentId: 'DEPT-MKT', departmentName: 'Marketing', allocatedBudget: 7500000, totalSpent: 2800000, remainingBudget: 4700000, utilizationPercentage: 37.3, reimbursementCount: 14 },
      { departmentId: 'DEPT-HR', departmentName: 'HR', allocatedBudget: 5000000, totalSpent: 1200000, remainingBudget: 3800000, utilizationPercentage: 24, reimbursementCount: 9 },
      { departmentId: 'DEPT-FIN', departmentName: 'Finance', allocatedBudget: 3000000, totalSpent: 350000, remainingBudget: 2650000, utilizationPercentage: 11.7, reimbursementCount: 4 },
      { departmentId: 'DEPT-OPS', departmentName: 'Operations', allocatedBudget: 6000000, totalSpent: 200000, remainingBudget: 5800000, utilizationPercentage: 3.3, reimbursementCount: 2 },
    ],
    monthlyTrend: [
      { month: 'Jan 2026', totalSpend: 450000, reimbursementCount: 5 },
      { month: 'Feb 2026', totalSpend: 820000, reimbursementCount: 8 },
      { month: 'Mar 2026', totalSpend: 650000, reimbursementCount: 7 },
      { month: 'Apr 2026', totalSpend: 1200000, reimbursementCount: 12 },
      { month: 'May 2026', totalSpend: 980000, reimbursementCount: 10 },
      { month: 'Jun 2026', totalSpend: 1400000, reimbursementCount: 15 },
      { month: 'Jul 2026', totalSpend: 760000, reimbursementCount: 8 },
      { month: 'Aug 2026', totalSpend: 890000, reimbursementCount: 9 },
      { month: 'Sep 2026', totalSpend: 1600000, reimbursementCount: 18 },
    ],
  };
}
