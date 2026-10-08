import { useAuth } from '../auth/AuthContext';
import { LoadingSpinner } from './Shared';

export function AccessGate({ roles, children }) {
  const auth = useAuth();
  if (!auth.isAuthenticated) {
    return <StatePanel title="Sign in required" message="Add a valid bearer token through the shared authentication flow to continue." />;
  }
  if (!auth.hasAnyRole(...roles)) {
    return <StatePanel title="Access denied" message="Your account does not have a role allowed to use this view." />;
  }
  return children;
}

export function QueryState({ query, empty, children }) {
  if (query.isLoading) return <LoadingSpinner />;
  if (query.isError) {
    const status = query.error?.response?.status;
    const title = status === 401 ? 'Session expired' : status === 403 ? 'Access denied' : 'Could not load data';
    return <StatePanel title={title} message={safeMessage(query.error)} action="Try again" onAction={() => query.refetch()} />;
  }
  if (empty) return <StatePanel title="Nothing here yet" message="No records match the current filters." />;
  return children;
}

export function MutationError({ error }) {
  if (!error) return null;
  return <div className="alert alert-danger" role="alert">{safeMessage(error)}</div>;
}

export function StatePanel({ title, message, action, onAction }) {
  return (
    <div className="card state-panel" role="status">
      <h3>{title}</h3>
      <p>{message}</p>
      {action && <button className="btn btn-primary" onClick={onAction}>{action}</button>}
    </div>
  );
}

function safeMessage(error) {
  const status = error?.response?.status;
  if (status === 401) return 'Your session is missing or expired. Sign in and retry.';
  if (status === 403) return 'You do not have permission to perform this action.';
  if (status === 400) return error.response?.data?.title ?? 'Some submitted values are invalid. Review the form and retry.';
  return 'The service did not complete the request. No changes were assumed; retry when ready.';
}

