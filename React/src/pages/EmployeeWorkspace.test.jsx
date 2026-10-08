import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import EmployeeWorkspace from './EmployeeWorkspace';
import * as api from '../services/api';

vi.mock('../services/api', () => ({
  createClaim: vi.fn(), createPurchaseRequest: vi.fn(), deleteClaim: vi.fn(),
  deletePurchaseRequest: vi.fn(), getMyProfile: vi.fn(),
  getPurchaseRequests: vi.fn(), searchClaims: vi.fn(), submitClaim: vi.fn(),
  submitPurchaseRequest: vi.fn(), updatePurchaseRequest: vi.fn(),
}));

function renderWorkspace() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<MemoryRouter><QueryClientProvider client={client}><EmployeeWorkspace /></QueryClientProvider></MemoryRouter>);
}

describe('EmployeeWorkspace', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.searchClaims.mockResolvedValue([]);
    api.getPurchaseRequests.mockResolvedValue([]);
  });

  it('shows a useful empty claim state', async () => {
    renderWorkspace();
    expect(await screen.findByText('No claims match these filters.')).toBeInTheDocument();
  });

  it('loads the current employee profile without an employee selector', async () => {
    api.getMyProfile.mockResolvedValue({
      employeeId: 7, fullName: 'Asha Perera', email: 'asha@example.com',
      username: 'asha', designation: 'Engineer', departmentName: 'Engineering & IT', isActive: true, isLocked: false,
    });
    renderWorkspace();
    fireEvent.click(screen.getByRole('button', { name: 'Profile' }));
    expect(await screen.findByText('Asha Perera')).toBeInTheDocument();
    expect(screen.getByText('Engineer · Engineering & IT')).toBeInTheDocument();
    expect(screen.queryByLabelText(/employee id/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Employee admin' })).not.toBeInTheDocument();
  });

  it('lets an employee edit a draft purchase request', async () => {
    api.getPurchaseRequests.mockResolvedValue([{
      purchaseRequestId: 4, description: 'Disney Plus Subscription', estimatedAmount: 50000,
      currency: 'LKR', vendor: 'Disney', category: 'Entertainment subscriptions',
      status: 'Draft', version: 2,
      approvalSteps: [{ status: 'REVISION_REQUIRED', comment: 'Please add a quote' }],
    }]);
    api.updatePurchaseRequest.mockResolvedValue({ purchaseRequestId: 4, version: 3 });
    renderWorkspace();
    fireEvent.click(screen.getByRole('button', { name: 'Purchase requests' }));
    expect(await screen.findByText('Disney Plus Subscription')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Edit' }));
    expect(screen.getByText('Edit purchase request #4')).toBeInTheDocument();
    expect(screen.getByText('Revision requested: Please add a quote')).toBeInTheDocument();
    fireEvent.change(screen.getByDisplayValue('Disney Plus Subscription'), { target: { value: 'Team training software' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(api.updatePurchaseRequest).toHaveBeenCalledWith(4, expect.objectContaining({
      description: 'Team training software', vendor: 'Disney', version: 2,
    })));
  });
});
