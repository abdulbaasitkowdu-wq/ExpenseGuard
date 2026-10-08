import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { FiMoon, FiSun } from 'react-icons/fi';
import { useAuth } from '../auth/AuthContext';
import { canAccess, homePath } from '../auth/access';
import { useTheme } from '../auth/ThemeContext';
import { apiErrorMessage } from '../components/Shared';

export default function Login() {
  const { login, isAuthenticated, session } = useAuth();
  const { isDark, toggleTheme } = useTheme();
  const [form, setForm] = useState({ username: '', password: '' });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const fallback = homePath(session?.role);

  if (isAuthenticated) return <Navigate to={fallback} replace />;

  const submit = async event => {
    event.preventDefault();
    if (!form.username.trim() || !form.password) {
      setError('Username and password are required.');
      return;
    }
    setBusy(true);
    setError('');
    try {
      const next = await login(form.username, form.password);
      const requested = location.state?.from;
      navigate(requested && canAccess(next.role, requested) ? requested : homePath(next.role), { replace: true });
    } catch (err) {
      setError(apiErrorMessage(err, 'Sign in failed. Try again.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="login-page">
      <form className="card login-card" onSubmit={submit} noValidate>
        <div className="login-card-header">
          <div>
            <h1>ExpenseGuard</h1>
            <p>Sign in to continue.</p>
          </div>
          <button className="btn btn-ghost btn-sm" type="button" onClick={toggleTheme}
            aria-label={isDark ? 'Switch to light mode' : 'Switch to dark mode'}>
            {isDark ? <FiSun /> : <FiMoon />}
          </button>
        </div>
        {error && <div className="alert alert-danger" role="alert">{error}</div>}
        <label className="form-label" htmlFor="username">Username</label>
        <input id="username" autoComplete="username" value={form.username}
          onChange={e => setForm({ ...form, username: e.target.value })} />
        <label className="form-label" htmlFor="password">Password</label>
        <input id="password" type="password" autoComplete="current-password" value={form.password}
          onChange={e => setForm({ ...form, password: e.target.value })} />
        <button className="btn btn-primary" type="submit" disabled={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  );
}
