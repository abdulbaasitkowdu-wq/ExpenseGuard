import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach } from 'vitest';
import { AuthProvider } from '../auth/AuthContext';
import { AccessGate, QueryState } from './RequestState';

afterEach(() => localStorage.clear());

function renderWithAuth(ui) {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthProvider>{ui}</AuthProvider>
    </QueryClientProvider>,
  );
}

describe('role-aware request states', () => {
  it('shows authentication guidance without a session', async () => {
    renderWithAuth(<AccessGate roles={['Admin']}><div>secret</div></AccessGate>);
    expect(await screen.findByText('Sign in required')).toBeInTheDocument();
    expect(screen.queryByText('secret')).not.toBeInTheDocument();
  });

  it('shows forbidden guidance for the wrong role', async () => {
    localStorage.setItem('expenseguard.session', JSON.stringify({
      token: 'token', username: 'emp', role: 'Employee', employeeId: 1,
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
    }));
    renderWithAuth(<AccessGate roles={['Admin']}><div>secret</div></AccessGate>);
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
    expect(screen.queryByText('secret')).not.toBeInTheDocument();
  });

  it('uses a safe message for service failures', () => {
    render(<QueryState query={{ isLoading: false, isError: true, error: {}, refetch: vi.fn() }}>content</QueryState>);
    expect(screen.getByText(/No changes were assumed/)).toBeInTheDocument();
  });
});
