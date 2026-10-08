import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { departmentApi } from './api';

export const departmentKeys = { all: ['departments'] };

export function useDepartments(active) {
  return useQuery({
    queryKey: [...departmentKeys.all, active ?? 'all'],
    queryFn: () => departmentApi.list({ page: 1, pageSize: 100, active }),
  });
}

export function useDepartmentMutation(action) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }) => action === 'create'
      ? departmentApi.create(body)
      : action === 'update'
        ? departmentApi.update(id, body)
        : departmentApi.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: departmentKeys.all }),
  });
}
