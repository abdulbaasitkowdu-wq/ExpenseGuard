import { useState } from 'react';
import { LoadingSpinner } from '../../../components/Shared';
import StatePanel from '../../budget/components/StatePanel';
import { useDepartmentMutation, useDepartments } from '../hooks';

const blank = { code: '', name: '', isActive: true, version: '' };

export default function DepartmentAdmin() {
  const departments = useDepartments();
  const create = useDepartmentMutation('create');
  const update = useDepartmentMutation('update');
  const remove = useDepartmentMutation('remove');
  const [form, setForm] = useState(blank);
  const [editing, setEditing] = useState(null);
  const [validation, setValidation] = useState('');
  const mutationError = create.error || update.error || remove.error;

  const submit = (event) => {
    event.preventDefault();
    const code = form.code.trim().toUpperCase();
    const name = form.name.trim();
    if (!editing && !/^[A-Z0-9_-]{2,20}$/.test(code)) {
      setValidation('Code must be 2–20 letters, numbers, underscores, or hyphens.');
      return;
    }
    if (name.length < 2 || name.length > 120) {
      setValidation('Name must be 2–120 characters.');
      return;
    }
    setValidation('');
    const mutation = editing ? update : create;
    const body = editing
      ? { name, isActive: form.isActive, version: form.version }
      : { code, name };
    mutation.mutate({ id: editing, body }, {
      onSuccess: () => { setForm(blank); setEditing(null); },
    });
  };

  const startEdit = (item) => {
    setEditing(item.id);
    setForm({ code: item.code, name: item.name, isActive: item.isActive, version: item.version });
  };

  return (
    <section className="feature-grid">
      <form className="card" onSubmit={submit}>
        <h3>{editing ? 'Edit department' : 'Create department'}</h3>
        {!editing && <label className="form-group"><span className="form-label">Code</span>
          <input className="form-control" value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} />
        </label>}
        <label className="form-group"><span className="form-label">Name</span>
          <input className="form-control" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        </label>
        {editing && <label className="check-row">
          <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} /> Active
        </label>}
        {validation && <div className="alert alert-danger" role="alert">{validation}</div>}
        {mutationError && <StatePanel error={mutationError} />}
        <div className="action-row">
          <button className="btn btn-primary" disabled={create.isPending || update.isPending}>
            {create.isPending || update.isPending ? 'Saving…' : 'Save'}
          </button>
          {editing && <button type="button" className="btn btn-ghost" onClick={() => { setEditing(null); setForm(blank); }}>Cancel</button>}
        </div>
      </form>

      <div className="table-wrapper">
        <div className="table-header"><h3>Departments</h3></div>
        {departments.isPending ? <LoadingSpinner /> : departments.isError
          ? <div className="state-pad"><StatePanel error={departments.error} onRetry={departments.refetch} /></div>
          : !departments.data?.items?.length
            ? <div className="empty-state">No departments have been configured.</div>
            : <table><thead><tr><th>Code</th><th>Name</th><th>Status</th><th>Actions</th></tr></thead>
              <tbody>{departments.data.items.map((item) => <tr key={item.id}>
                <td>{item.code}</td><td>{item.name}</td><td>{item.isActive ? 'Active' : 'Inactive'}</td>
                <td className="action-row">
                  <button className="btn btn-ghost btn-sm" onClick={() => startEdit(item)}>Edit</button>
                  <button className="btn btn-danger btn-sm" disabled={remove.isPending}
                    onClick={() => window.confirm(`Delete ${item.name}?`) && remove.mutate({ id: item.id })}>Delete</button>
                </td>
              </tr>)}</tbody>
            </table>}
      </div>
    </section>
  );
}
