import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { assignRole, getRoles } from '../services/api';
import { EmptyState, ErrorState, LoadingSpinner, apiErrorMessage } from '../components/Shared';

export default function Roles() {
  const [employeeId, setEmployeeId] = useState('');
  const [roleId, setRoleId] = useState('');
  const [message, setMessage] = useState('');
  const roles = useQuery({ queryKey: ['roles'], queryFn: async () => (await getRoles()).data });
  const assignment = useMutation({
    mutationFn: () => assignRole(Number(employeeId), Number(roleId)),
    onSuccess: () => setMessage('Role assigned successfully.'),
  });

  const submit = event => {
    event.preventDefault();
    setMessage('');
    if (!/^[1-9]\d*$/.test(employeeId) || !roleId) return;
    assignment.mutate();
  };

  return (
    <div className="page-content">
      <h2>Role administration</h2>
      {roles.isPending && <LoadingSpinner />}
      {roles.isError && <ErrorState error={roles.error} onRetry={roles.refetch} />}
      {roles.data?.length === 0 && <EmptyState message="No roles are configured." />}
      {roles.data?.length > 0 && (
        <>
          <div className="table-wrapper">
            <table><thead><tr><th>Role</th><th>Approval limit</th><th>Approve</th><th>Payments</th><th>Manage roles</th></tr></thead>
              <tbody>{roles.data.map(role => <tr key={role.roleId}>
                <td>{role.roleName}</td><td>{role.approvalLimit ?? '—'}</td>
                <td>{role.canApprove ? 'Yes' : 'No'}</td><td>{role.canProcessPayments ? 'Yes' : 'No'}</td>
                <td>{role.canManageRoles ? 'Yes' : 'No'}</td>
              </tr>)}</tbody>
            </table>
          </div>
          <form className="card role-form" onSubmit={submit}>
            <h3>Assign role</h3>
            {message && <div className="alert alert-success" role="status">{message}</div>}
            {assignment.isError && <div className="alert alert-danger" role="alert">{apiErrorMessage(assignment.error)}</div>}
            <label className="form-label" htmlFor="employee-id">Employee ID</label>
            <input id="employee-id" inputMode="numeric" required pattern="[1-9]\d*" value={employeeId} onChange={e => setEmployeeId(e.target.value)} />
            <label className="form-label" htmlFor="role-id">Role</label>
            <select id="role-id" required value={roleId} onChange={e => setRoleId(e.target.value)}>
              <option value="">Select a role</option>
              {roles.data.map(role => <option key={role.roleId} value={role.roleId}>{role.roleName}</option>)}
            </select>
            <button className="btn btn-primary" type="submit" disabled={assignment.isPending || !employeeId || !roleId}>Assign</button>
          </form>
        </>
      )}
    </div>
  );
}
