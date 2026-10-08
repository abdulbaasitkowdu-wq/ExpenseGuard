import api from '../../services/api';

export const departmentApi = {
  list: (params = {}) => api.get('/departments', { params }).then(({ data }) => data),
  create: (data) => api.post('/departments', data).then(({ data: body }) => body),
  update: (id, data) => api.put(`/departments/${id}`, data).then(({ data: body }) => body),
  remove: (id) => api.delete(`/departments/${id}`),
};
