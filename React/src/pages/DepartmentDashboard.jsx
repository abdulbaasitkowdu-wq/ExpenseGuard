import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { getMyProfile, searchClaims } from '../services/api';
import { formatDesignation, LoadingSpinner } from '../components/Shared';

const claimStatuses = ['Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'NeedsCorrection', 'Cancelled'];
const statusName = value => typeof value === 'number' ? claimStatuses[value] : value;

export default function DepartmentDashboard() {
  const profile = useQuery({ queryKey: ['employee-profile'], queryFn: getMyProfile });
  const claims = useQuery({ queryKey: ['claims', 'department'], queryFn: () => searchClaims({ limit: 50 }) });

  if (profile.isPending || claims.isPending) return <div className="page-content"><LoadingSpinner /></div>;
  if (profile.isError) return <div className="page-content"><div className="alert alert-danger">{profile.error.message}</div></div>;

  const rows = claims.data ?? [];
  const totals = rows.reduce((sum, claim) => sum + Number(claim.amount || 0), 0);
  const open = rows.filter(claim => !['Approved', 'Rejected', 'Cancelled'].includes(statusName(claim.status))).length;

  return (
    <div className="page-content">
      <div className="employee-heading">
        <div>
          <h2>Department dashboard</h2>
          <p>A focused view of {profile.data.departmentName || 'your department'} activity for {profile.data.fullName}.</p>
        </div>
        <Link className="btn btn-primary" to="/employee">Open my expenses</Link>
      </div>
      <div className="stats-grid">
        <article className="stat-card primary"><span className="stat-label">Department</span><strong className="stat-value">{profile.data.departmentName || `#${profile.data.departmentId}`}</strong><span className="stat-sub">{formatDesignation(profile.data)}</span></article>
        <article className="stat-card success"><span className="stat-label">Tracked claims</span><strong className="stat-value">{rows.length}</strong><span className="stat-sub">{open} still in progress</span></article>
        <article className="stat-card warning"><span className="stat-label">Claimed amount</span><strong className="stat-value">{totals.toLocaleString()}</strong><span className="stat-sub">Across visible claims</span></article>
      </div>
      <div className="table-wrapper">
        <div className="table-header"><h3>My recent claims</h3></div>
        {!rows.length ? <div className="empty-state">No claims are available for this dashboard.</div> : (
          <table>
            <thead><tr><th>Claim</th><th>Category</th><th>Amount</th><th>Status</th></tr></thead>
            <tbody>{rows.slice(0, 12).map(claim => (
              <tr key={claim.expenseClaimId}>
                <td><Link to={`/employee/claims/${claim.expenseClaimId}`}>#{claim.expenseClaimId}</Link></td>
                <td>{claim.category}</td>
                <td>{claim.currency} {Number(claim.amount).toLocaleString()}</td>
                <td>{statusName(claim.status)}</td>
              </tr>
            ))}</tbody>
          </table>
        )}
      </div>
    </div>
  );
}
