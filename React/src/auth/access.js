import { matchPath } from 'react-router-dom';

export const ALL_ROLES = ['Employee', 'Manager', 'DepartmentHead', 'Finance', 'Admin'];
export const APPROVER_ROLES = ['Manager', 'DepartmentHead', 'Finance', 'Admin'];
export const FINANCE_SCOPE_ROLES = ['Finance', 'Admin'];
export const ADMIN_SCOPE_ROLES = ['Admin'];
export const DEPARTMENT_DASHBOARD_ROLES = ['Employee', 'Manager', 'DepartmentHead', 'Finance', 'Admin'];
export const COMPANY_DASHBOARD_ROLES = ['Finance', 'Admin'];

const ROUTES = [
  { pattern: '/', end: true, roles: COMPANY_DASHBOARD_ROLES },
  { pattern: '/forbidden', roles: ALL_ROLES },
  { pattern: '/employee', end: true, roles: ALL_ROLES },
  { pattern: '/employee/claims/:id', roles: ALL_ROLES },
  { pattern: '/department', roles: DEPARTMENT_DASHBOARD_ROLES },
  { pattern: '/reimbursements/:id', roles: APPROVER_ROLES },
  { pattern: '/approvals', end: true, roles: APPROVER_ROLES },
  { pattern: '/approvals/requests/:id', roles: APPROVER_ROLES },
  { pattern: '/approvals/:id', roles: APPROVER_ROLES },
  { pattern: '/finance-queue', roles: FINANCE_SCOPE_ROLES },
  { pattern: '/budgets', roles: FINANCE_SCOPE_ROLES },
  { pattern: '/reports', roles: FINANCE_SCOPE_ROLES },
  { pattern: '/history', roles: FINANCE_SCOPE_ROLES },
  { pattern: '/workflows/:id', roles: FINANCE_SCOPE_ROLES },
  { pattern: '/workflows', roles: FINANCE_SCOPE_ROLES },
  { pattern: '/policies', roles: FINANCE_SCOPE_ROLES },
  { pattern: '/fraud/:id', roles: FINANCE_SCOPE_ROLES },
  { pattern: '/fraud', roles: FINANCE_SCOPE_ROLES },
  { pattern: '/admin/budgets', roles: ADMIN_SCOPE_ROLES },
  { pattern: '/admin/departments', roles: ADMIN_SCOPE_ROLES },
  { pattern: '/admin/roles', roles: ADMIN_SCOPE_ROLES },
];

export const NAV_SECTIONS = [
  {
    id: 'work',
    label: 'Work',
    items: [
      { to: '/employee', label: 'My Expenses' },
      { to: '/department', label: 'Department' },
    ],
  },
  {
    id: 'approvals',
    label: 'Approvals',
    items: [{ to: '/approvals', label: 'Approval Queue' }],
  },
  {
    id: 'finance',
    label: 'Finance',
    items: [
      { to: '/', label: 'Dashboard' },
      { to: '/finance-queue', label: 'Processing Queue' },
      { to: '/budgets', label: 'Budgets' },
      { to: '/reports', label: 'Spend Analytics' },
      { to: '/history', label: 'Request History' },
      { to: '/workflows', label: 'Workflows' },
    ],
  },
  {
    id: 'risk',
    label: 'Risk',
    items: [
      { to: '/policies', label: 'Policies' },
      { to: '/fraud', label: 'Fraud Review' },
    ],
  },
  {
    id: 'admin',
    label: 'Admin',
    items: [
      { to: '/admin/budgets', label: 'Budget Admin' },
      { to: '/admin/departments', label: 'Departments' },
      { to: '/admin/roles', label: 'Roles' },
    ],
  },
];

export function canAccess(role, pathname) {
  if (!role || !pathname) return false;
  return ROUTES.some(route => {
    const match = matchPath({ path: route.pattern, end: route.end ?? false }, pathname);
    return Boolean(match) && route.roles.includes(role);
  });
}

export function homePath(role) {
  return role === 'Finance' || role === 'Admin' ? '/' : '/employee';
}

export function navSectionsFor(role) {
  return NAV_SECTIONS
    .map(section => ({
      ...section,
      items: section.items.filter(item => canAccess(role, item.to)),
    }))
    .filter(section => section.items.length > 0);
}

export function isNavActive(pathname, to) {
  if (to === '/') return pathname === '/';
  return pathname === to || pathname.startsWith(`${to}/`);
}
