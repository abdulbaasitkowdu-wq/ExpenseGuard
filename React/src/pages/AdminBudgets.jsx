import { useState, useEffect } from 'react';
import { getAllBudgets, createBudget, updateBudget } from '../services/api';
import { LoadingSpinner, AmountDisplay } from '../components/Shared';
import { FiPlusCircle, FiEdit2, FiSave } from 'react-icons/fi';

export default function AdminBudgets() {
  const [budgets, setBudgets] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editId, setEditId] = useState(null);
  const [form, setForm] = useState({ departmentId: '', departmentName: '', fiscalYear: new Date().getFullYear(), allocatedAmount: '', currency: 'LKR' });
  const [msg, setMsg] = useState(null);

  const fetchBudgets = () => {
    setLoading(true);
    getAllBudgets()
      .then(r => setBudgets(r.data?.length ? r.data : []))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(fetchBudgets, []);

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editId) {
        await updateBudget(editId, { allocatedAmount: Number(form.allocatedAmount) });
        setMsg({ type: 'success', text: 'Budget updated successfully' });
      } else {
        await createBudget({ ...form, allocatedAmount: Number(form.allocatedAmount) });
        setMsg({ type: 'success', text: 'Budget created successfully' });
      }
      setShowForm(false); setEditId(null);
      fetchBudgets();
    } catch (e) {
      setMsg({ type: 'danger', text: e.response?.data?.error || 'Error saving budget' });
    }
  };

  const startEdit = (b) => {
    setEditId(b.id);
    setForm({ ...b, allocatedAmount: b.allocatedAmount });
    setShowForm(true);
  };

  return (
    <div className="page-content">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <h2 style={{ fontSize: 22, fontWeight: 800 }}>Manage Department Budgets</h2>
          <p style={{ color: 'var(--text-muted)', fontSize: 14 }}>Admin: Allocate and reallocate budgets</p>
        </div>
        <button id="btn-new-budget" className="btn btn-primary" onClick={() => { setShowForm(true); setEditId(null); setForm({ departmentId: '', departmentName: '', fiscalYear: new Date().getFullYear(), allocatedAmount: '', currency: 'LKR' }); }}>
          <FiPlusCircle /> New Budget
        </button>
      </div>

      {msg && <div className={`alert alert-${msg.type}`} style={{ marginBottom: 16 }}>{msg.text}</div>}

      {showForm && (
        <div className="card" style={{ marginBottom: 20, borderColor: 'var(--accent-primary)' }}>
          <h3 style={{ marginBottom: 16 }}>{editId ? 'Update Budget' : 'Create New Budget'}</h3>
          <form onSubmit={handleSubmit}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16 }}>
              <div className="form-group">
                <label className="form-label">Department ID</label>
                <input id="input-dept-id" className="form-control" required
                  value={form.departmentId} onChange={e => setForm(f => ({ ...f, departmentId: e.target.value }))} />
              </div>
              <div className="form-group">
                <label className="form-label">Department Name</label>
                <input id="input-dept-name" className="form-control" required
                  value={form.departmentName} onChange={e => setForm(f => ({ ...f, departmentName: e.target.value }))} />
              </div>
              <div className="form-group">
                <label className="form-label">Fiscal Year</label>
                <input id="input-fiscal-year" type="number" className="form-control" required
                  value={form.fiscalYear} onChange={e => setForm(f => ({ ...f, fiscalYear: Number(e.target.value) }))} />
              </div>
              <div className="form-group">
                <label className="form-label">Allocated Amount (LKR)</label>
                <input id="input-amount" type="number" className="form-control" required min={1}
                  value={form.allocatedAmount} onChange={e => setForm(f => ({ ...f, allocatedAmount: e.target.value }))} />
              </div>
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
              <button id="btn-save-budget" type="submit" className="btn btn-primary"><FiSave /> Save</button>
              <button type="button" className="btn btn-ghost" onClick={() => setShowForm(false)}>Cancel</button>
            </div>
          </form>
        </div>
      )}

      {loading ? <LoadingSpinner /> : (
        <div className="table-wrapper">
          <div className="table-header"><h3>All Department Budgets</h3></div>
          <table>
            <thead>
              <tr>
                <th>Department</th><th>FY</th><th>Allocated</th><th>Spent</th><th>Remaining</th><th>Utilization</th><th>Active</th><th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {budgets.map(b => (
                <tr key={b.id} id={`admin-budget-${b.id}`}>
                  <td><strong>{b.departmentName}</strong><br /><span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{b.departmentId}</span></td>
                  <td>{b.fiscalYear}</td>
                  <td><AmountDisplay amount={b.allocatedAmount} /></td>
                  <td><AmountDisplay amount={b.approvedSpend} /></td>
                  <td><AmountDisplay amount={b.remainingBudget} /></td>
                  <td>{b.utilizationPercentage?.toFixed(1)}%</td>
                  <td>{b.isActive ? '✅' : '❌'}</td>
                  <td>
                    <button id={`btn-edit-${b.id}`} className="btn btn-ghost btn-sm" onClick={() => startEdit(b)}>
                      <FiEdit2 />
                    </button>
                  </td>
                </tr>
              ))}
              {!budgets.length && (
                <tr><td colSpan={8} style={{ textAlign: 'center', color: 'var(--text-muted)', padding: 32 }}>
                  No budgets configured. Create one above.
                </td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
