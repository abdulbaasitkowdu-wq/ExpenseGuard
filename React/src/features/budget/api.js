import api from '../../services/api';

export const budgetApi = {
  allocate: (data) => api.post('/budgets', data).then(({ data: body }) => body),
  get: (id) => api.get(`/budgets/${id}`).then(({ data }) => data),
  utilization: (params) => api.get('/budgets/reports/utilization', { params }).then(({ data }) => data),
  transactions: (id, params) => api.get(`/budgets/${id}/transactions`, { params }).then(({ data }) => data),
  alerts: (id, status) => api.get(`/budgets/${id}/alerts`, { params: status ? { status } : {} }).then(({ data }) => data),
  availability: (id, amount) => api.get(`/budgets/${id}/availability`, { params: { amount } }).then(({ data }) => data),
  reserve: (id, data) => api.post(`/budgets/${id}/reserve`, data).then(({ data: body }) => body),
  release: (id, data) => api.post(`/budgets/${id}/release`, data).then(({ data: body }) => body),
  spend: (id, data) => api.post(`/budgets/${id}/spend`, data).then(({ data: body }) => body),
};

export function apiError(error) {
  const status = error?.response?.status;
  const body = error?.response?.data;
  const detail = body?.detail || body?.title || body?.message || body?.error;
  if (status === 403) return { kind: 'forbidden', message: 'You do not have permission for this operation.' };
  if (status === 409) return { kind: 'conflict', message: detail || 'The budget changed or funds are unavailable. Refresh and retry.' };
  if (status === 422 || status === 400) return { kind: 'validation', message: detail || 'Check the submitted values.' };
  return { kind: 'error', message: detail || 'The request failed. Please retry.' };
}
