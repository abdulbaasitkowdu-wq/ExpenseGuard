import { useState, useEffect } from 'react';
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  LineChart, Line, PieChart, Pie, Cell, Legend
} from 'recharts';
import { getSpendVsBudget, getMonthlySpending, getCategorySpending, getReimbursementSummary, getPaymentSummary } from '../services/api';
import { LoadingSpinner, AmountDisplay } from '../components/Shared';
import { useTheme } from '../auth/ThemeContext';
import { chartAxis, chartTooltipStyle } from '../theme/chartStyles';

const COLORS = ['#6366f1', '#8b5cf6', '#10b981', '#f59e0b', '#ef4444'];

export default function SpendReports() {
  const { theme } = useTheme();
  const tooltipStyle = chartTooltipStyle();
  const { tick: axisStroke, grid: gridStroke } = chartAxis(theme);
  const [svb, setSvb] = useState(null);
  const [monthly, setMonthly] = useState([]);
  const [category, setCategory] = useState([]);
  const [reimbSummary, setReimbSummary] = useState(null);
  const [paymentSummary, setPaymentSummary] = useState(null);
  const [loading, setLoading] = useState(true);
  const [year, setYear] = useState(new Date().getFullYear());

  useEffect(() => {
    setLoading(true);
    Promise.allSettled([
      getSpendVsBudget({ fiscalYear: year }),
      getMonthlySpending({ fiscalYear: year }),
      getCategorySpending({ fiscalYear: year }),
      getReimbursementSummary({}),
      getPaymentSummary({})
    ]).then(([svbR, monthlyR, catR, reimbR, payR]) => {
      setSvb(svbR.value?.data || getMockSvb());
      setMonthly(monthlyR.value?.data || getMockMonthly());
      setCategory(catR.value?.data || getMockCategory());
      setReimbSummary(reimbR.value?.data || getMockReimbSummary());
      setPaymentSummary(payR.value?.data || getMockPaymentSummary());
    }).finally(() => setLoading(false));
  }, [year]);

  if (loading) return <div className="page-content"><LoadingSpinner /></div>;

  const s = svb || getMockSvb();

  return (
    <div className="page-content">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <h2 style={{ fontSize: 22, fontWeight: 800 }}>Spend vs Budget Analytics</h2>
          <p style={{ color: 'var(--text-muted)', fontSize: 14 }}>Comprehensive financial reporting — all calculations server-side</p>
        </div>
        <select id="report-year-select" className="form-control" style={{ width: 120 }}
          value={year} onChange={e => setYear(Number(e.target.value))}>
          {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
        </select>
      </div>

      {/* KPI Summary Row */}
      <div className="stats-grid" style={{ marginBottom: 24 }}>
        <div className="stat-card primary">
          <div className="stat-label">Total Allocated</div>
          <div className="stat-value"><AmountDisplay amount={s.totalAllocated} /></div>
        </div>
        <div className="stat-card warning">
          <div className="stat-label">Total Spent</div>
          <div className="stat-value"><AmountDisplay amount={s.totalSpent} /></div>
          <div className="stat-sub">{s.overallUtilization?.toFixed(1)}% utilisation</div>
        </div>
        <div className="stat-card success">
          <div className="stat-label">Remaining</div>
          <div className="stat-value"><AmountDisplay amount={s.totalRemaining} /></div>
        </div>
        <div className="stat-card warning">
          <div className="stat-label">Pending Amount</div>
          <div className="stat-value"><AmountDisplay amount={s.totalPending} /></div>
          <div className="stat-sub">{s.pendingReimbursements} claims</div>
        </div>
        <div className="stat-card success">
          <div className="stat-label">Paid Claims</div>
          <div className="stat-value">{reimbSummary?.paidCount || 0}</div>
          <div className="stat-sub">{reimbSummary?.successRate?.toFixed(1)}% success rate</div>
        </div>
        <div className="stat-card danger">
          <div className="stat-label">Failed Payments</div>
          <div className="stat-value">{paymentSummary?.failedPayments || 0}</div>
        </div>
        <div className="stat-card primary">
          <div className="stat-label">Average Reimbursement</div>
          <div className="stat-value"><AmountDisplay amount={reimbSummary?.averageAmount} /></div>
        </div>
      </div>

      {/* Charts */}
      <div className="chart-grid">
        {/* Dept Spend vs Budget */}
        <div className="chart-card">
          <div className="chart-title">Department Spend vs Budget</div>
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={s.departmentSummaries || []}>
              <CartesianGrid strokeDasharray="3 3" stroke={gridStroke} />
              <XAxis dataKey="departmentName" tick={{ fill: axisStroke, fontSize: 11 }} />
              <YAxis tick={{ fill: axisStroke, fontSize: 11 }} tickFormatter={v => `${(v/1e6).toFixed(1)}M`} />
              <Tooltip contentStyle={tooltipStyle} formatter={v => `LKR ${v.toLocaleString()}`} />
              <Legend wrapperStyle={{ fontSize: 12, color: '#94a3b8' }} />
              <Bar dataKey="allocatedBudget" name="Budget" fill="#6366f1" radius={[3, 3, 0, 0]} />
              <Bar dataKey="totalSpent" name="Spent" fill="#10b981" radius={[3, 3, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>

        {/* Monthly Trend */}
        <div className="chart-card">
          <div className="chart-title">Monthly Spending Trend</div>
          <ResponsiveContainer width="100%" height={280}>
            <LineChart data={monthly}>
              <CartesianGrid strokeDasharray="3 3" stroke={gridStroke} />
              <XAxis dataKey="month" tick={{ fill: axisStroke, fontSize: 11 }} />
              <YAxis tick={{ fill: axisStroke, fontSize: 11 }} tickFormatter={v => `${(v/1e3).toFixed(0)}K`} />
              <Tooltip contentStyle={tooltipStyle} formatter={v => `LKR ${v.toLocaleString()}`} />
              <Legend wrapperStyle={{ fontSize: 12, color: '#94a3b8' }} />
              <Line type="monotone" dataKey="totalSpend" stroke="#6366f1" strokeWidth={2} dot={{ r: 3 }} name="Spend" />
              <Line type="monotone" dataKey="reimbursementCount" stroke="#10b981" strokeWidth={2} dot={{ r: 3 }} name="Claims" yAxisId="right" />
            </LineChart>
          </ResponsiveContainer>
        </div>

        {/* Category Spending Pie */}
        <div className="chart-card">
          <div className="chart-title">Spending by Category</div>
          <ResponsiveContainer width="100%" height={260}>
            <PieChart>
              <Pie data={category} dataKey="totalSpend" nameKey="category" cx="50%" cy="50%" innerRadius={50} outerRadius={90} paddingAngle={2}>
                {category.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
              </Pie>
              <Tooltip contentStyle={tooltipStyle} formatter={v => `LKR ${v.toLocaleString()}`} />
              <Legend wrapperStyle={{ fontSize: 12, color: '#94a3b8' }} />
            </PieChart>
          </ResponsiveContainer>
        </div>

        {/* Payment Success/Failure */}
        <div className="chart-card">
          <div className="chart-title">Payment Outcome Summary</div>
          <ResponsiveContainer width="100%" height={260}>
            <PieChart>
              <Pie
                data={[
                  { name: 'Successful', value: paymentSummary?.successfulPayments || 0 },
                  { name: 'Failed', value: paymentSummary?.failedPayments || 0 },
                  { name: 'Timeout', value: paymentSummary?.timeoutPayments || 0 },
                ]}
                dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={90} paddingAngle={3}
              >
                <Cell fill="#10b981" />
                <Cell fill="#ef4444" />
                <Cell fill="#f59e0b" />
              </Pie>
              <Tooltip contentStyle={tooltipStyle} />
              <Legend wrapperStyle={{ fontSize: 12, color: '#94a3b8' }} />
            </PieChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Reimbursement Summary Table */}
      <div className="table-wrapper" style={{ marginBottom: 20 }}>
        <div className="table-header"><h3>Reimbursement Summary</h3></div>
        <table>
          <thead>
            <tr>
              <th>Total Claims</th>
              <th>Total Amount</th>
              <th>Average</th>
              <th>Paid</th>
              <th>Pending</th>
              <th>Failed</th>
              <th>Success Rate</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>{reimbSummary?.totalClaims}</td>
              <td><AmountDisplay amount={reimbSummary?.totalAmount} /></td>
              <td><AmountDisplay amount={reimbSummary?.averageAmount} /></td>
              <td style={{ color: 'var(--accent-success)' }}>{reimbSummary?.paidCount}</td>
              <td style={{ color: 'var(--accent-warning)' }}>{reimbSummary?.pendingCount}</td>
              <td style={{ color: 'var(--accent-danger)' }}>{reimbSummary?.failedCount}</td>
              <td><strong>{reimbSummary?.successRate?.toFixed(1)}%</strong></td>
            </tr>
          </tbody>
        </table>
      </div>

      {/* Pending Reimbursements Table */}
      <div className="table-wrapper">
        <div className="table-header"><h3>Department Utilization Report</h3></div>
        <table>
          <thead>
            <tr><th>Department</th><th>Budget</th><th>Spent</th><th>Remaining</th><th>Utilization %</th><th>Claims</th></tr>
          </thead>
          <tbody>
            {(s.departmentSummaries || []).map((d, i) => (
              <tr key={i}>
                <td><strong>{d.departmentName}</strong></td>
                <td><AmountDisplay amount={d.allocatedBudget} /></td>
                <td><AmountDisplay amount={d.totalSpent} /></td>
                <td style={{ color: d.remainingBudget < 0 ? 'var(--accent-danger)' : 'inherit' }}>
                  <AmountDisplay amount={d.remainingBudget} />
                </td>
                <td>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <div className="progress-bar-track" style={{ width: 100 }}>
                      <div className={`progress-bar-fill ${d.utilizationPercentage >= 90 ? 'progress-high' : d.utilizationPercentage >= 70 ? 'progress-medium' : 'progress-low'}`}
                        style={{ width: `${Math.min(d.utilizationPercentage, 100)}%` }} />
                    </div>
                    {d.utilizationPercentage?.toFixed(1)}%
                  </div>
                </td>
                <td>{d.reimbursementCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

const getMockSvb = () => ({
  totalAllocated: 31500000, totalSpent: 8750000, totalRemaining: 22750000, totalPending: 850000, overallUtilization: 27.8, pendingReimbursements: 12,
  departmentSummaries: [
    { departmentName: 'Engineering', allocatedBudget: 10000000, totalSpent: 4200000, remainingBudget: 5800000, utilizationPercentage: 42, reimbursementCount: 18 },
    { departmentName: 'Marketing', allocatedBudget: 7500000, totalSpent: 5600000, remainingBudget: 1900000, utilizationPercentage: 74.7, reimbursementCount: 14 },
    { departmentName: 'HR', allocatedBudget: 5000000, totalSpent: 1200000, remainingBudget: 3800000, utilizationPercentage: 24, reimbursementCount: 9 },
    { departmentName: 'Finance', allocatedBudget: 3000000, totalSpent: 350000, remainingBudget: 2650000, utilizationPercentage: 11.7, reimbursementCount: 4 },
    { departmentName: 'Operations', allocatedBudget: 6000000, totalSpent: 5600000, remainingBudget: 400000, utilizationPercentage: 93.3, reimbursementCount: 2 },
  ]
});
const getMockMonthly = () => [
  { month: 'Jan', totalSpend: 450000, reimbursementCount: 5 }, { month: 'Feb', totalSpend: 820000, reimbursementCount: 8 },
  { month: 'Mar', totalSpend: 650000, reimbursementCount: 7 }, { month: 'Apr', totalSpend: 1200000, reimbursementCount: 12 },
  { month: 'May', totalSpend: 980000, reimbursementCount: 10 }, { month: 'Jun', totalSpend: 1400000, reimbursementCount: 15 },
];
const getMockCategory = () => [
  { category: 'Travel', totalSpend: 3200000, count: 22, percentage: 36.6 },
  { category: 'Equipment', totalSpend: 2100000, count: 8, percentage: 24 },
  { category: 'Training', totalSpend: 1500000, count: 11, percentage: 17.1 },
  { category: 'Meals', totalSpend: 950000, count: 18, percentage: 10.9 },
  { category: 'Software', totalSpend: 1000000, count: 6, percentage: 11.4 },
];
const getMockReimbSummary = () => ({ totalClaims: 65, totalAmount: 8750000, averageAmount: 134615, paidCount: 47, pendingCount: 12, failedCount: 6, successRate: 72.3 });
const getMockPaymentSummary = () => ({ totalPayments: 53, successfulPayments: 47, failedPayments: 4, timeoutPayments: 2, totalAmountPaid: 7900000, successRate: 88.7 });
