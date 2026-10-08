import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { budgetApi } from './api';

export const budgetKeys = {
  all: ['budgets'],
  report: (departmentId) => ['budgets', 'utilization', departmentId ?? 'all'],
  detail: (id) => ['budgets', Number(id)],
};

export function useUtilization(departmentId) {
  return useQuery({
    queryKey: budgetKeys.report(departmentId),
    queryFn: () => budgetApi.utilization({ page: 1, pageSize: 100, departmentId: departmentId || undefined }),
  });
}

export function useBudget(id) {
  return useQuery({
    queryKey: budgetKeys.detail(id),
    queryFn: () => budgetApi.get(id),
    enabled: Boolean(id),
  });
}

export function useBudgetTransactions(id) {
  return useQuery({
    queryKey: [...budgetKeys.detail(id), 'transactions'],
    queryFn: () => budgetApi.transactions(id, { page: 1, pageSize: 100 }),
    enabled: Boolean(id),
  });
}

export function useBudgetAlerts(id, status) {
  return useQuery({
    queryKey: [...budgetKeys.detail(id), 'alerts', status || 'all'],
    queryFn: () => budgetApi.alerts(id, status),
    enabled: Boolean(id),
  });
}

export function useBudgetMutation(action, id) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body) => budgetApi[action](id, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: budgetKeys.all }),
    onError: () => queryClient.invalidateQueries({ queryKey: budgetKeys.detail(id) }),
  });
}
