import { Link, useLocation } from 'react-router-dom';
import { FiGrid, FiList, FiDollarSign, FiBarChart2, FiActivity, FiSettings, FiUser, FiShield, FiAlertTriangle } from 'react-icons/fi';
import { useAuth } from '../auth/AuthContext';

const navItems = [
  { section: 'Employee', items: [
    { to: '/employee', icon: <FiUser />, label: 'My Expenses', id: 'nav-employee' },
  ]},
  { section: 'Approvals', items: [
    { to: '/approvals', icon: <FiList />, label: 'Approval Queue', id: 'nav-approvals', roles: ['Manager', 'DepartmentHead', 'Finance', 'Admin'] },
  ]},
  { section: 'Finance', items: [
    { to: '/', icon: <FiGrid />, label: 'Dashboard', id: 'nav-dashboard' },
    { to: '/finance-queue', icon: <FiList />, label: 'Processing Queue', id: 'nav-queue', roles: ['Finance', 'Admin'] },
  ]},
  { section: 'Budget', items: [
    { to: '/budgets', icon: <FiDollarSign />, label: 'Budget Overview', id: 'nav-budgets', roles: ['Finance', 'Admin'] },
    { to: '/reports', icon: <FiBarChart2 />, label: 'Spend Analytics', id: 'nav-reports', roles: ['Finance', 'Admin'] },
  ]},
  { section: 'Agentic AI', items: [
    { to: '/workflows', icon: <FiActivity />, label: 'Workflow Monitor', id: 'nav-workflows', roles: ['Finance', 'Admin'] },
  ]},
  { section: 'Risk & Compliance', items: [
    { to: '/policies', icon: <FiShield />, label: 'Policies', id: 'nav-policies', roles: ['Finance', 'Admin'] },
    { to: '/fraud', icon: <FiAlertTriangle />, label: 'Fraud Review', id: 'nav-fraud', roles: ['Finance', 'Admin'] },
  ]},
  { section: 'System', items: [
    { to: '/admin/budgets', icon: <FiSettings />, label: 'Admin: Budgets', id: 'nav-admin', roles: ['Admin'] },
    { to: '/admin/departments', icon: <FiSettings />, label: 'Departments', id: 'nav-departments', roles: ['Admin'] },
    { to: '/admin/roles', icon: <FiSettings />, label: 'Roles', id: 'nav-roles', roles: ['Admin'] },
  ]},
];

export default function Sidebar() {
  const location = useLocation();
  const { hasRole } = useAuth();
  return (
    <aside className="sidebar">
      <div className="sidebar-logo">
        <div className="logo-icon">💰</div>
        <div>
          <h2>ReimburseAI</h2>
          <span>Finance Module</span>
        </div>
      </div>
      <nav className="sidebar-nav">
        {navItems.map(section => (
          <div key={section.section}>
            <div className="nav-section-title">{section.section}</div>
            {section.items.filter(item => !item.roles || hasRole(...item.roles)).map(item => (
              <Link
                key={item.to}
                to={item.to}
                id={item.id}
                className={`nav-item ${location.pathname === item.to ? 'active' : ''}`}
              >
                {item.icon}
                <span>{item.label}</span>
              </Link>
            ))}
          </div>
        ))}
      </nav>
    </aside>
  );
}
