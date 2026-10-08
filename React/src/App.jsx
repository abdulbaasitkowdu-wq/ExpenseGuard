import { BrowserRouter, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import './index.css';
import { useAuth } from './auth/AuthContext';
import { ADMIN_SCOPE_ROLES, APPROVER_ROLES, canAccess, FINANCE_SCOPE_ROLES, homePath } from './auth/access';
import TopNav from './components/TopNav';
import { LoadingSpinner } from './components/Shared';
import FinanceDashboard from './pages/FinanceDashboard';
import FinanceQueue from './pages/FinanceQueue';
import ReimbursementDetails from './pages/ReimbursementDetails';
import BudgetDashboard from './pages/BudgetDashboard';
import SpendReports from './pages/SpendReports';
import FinanceRequestHistory from './pages/FinanceRequestHistory';
import WorkflowAudit from './pages/WorkflowAudit';
import AdminBudgets from './pages/AdminBudgets';
import Login from './pages/Login';
import Roles from './pages/Roles';
import ApprovalQueue from './pages/ApprovalQueue';
import PurchaseRequestReview from './pages/PurchaseRequestReview';
import EmployeeWorkspace from './pages/EmployeeWorkspace';
import EmployeeClaimDetail from './pages/EmployeeClaimDetail';
import DepartmentDashboard from './pages/DepartmentDashboard';
import PolicyManagement from './pages/PolicyManagement';
import { FraudDetail, FraudQueue } from './pages/FraudReview';
import Departments from './pages/Departments';

function ProtectedRoute({ roles }) {
  const auth = useAuth();
  const location = useLocation();
  if (auth.isBootstrapping) return <LoadingSpinner />;
  if (!auth.isAuthenticated) return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  if (roles && !auth.hasRole(...roles)) return <Navigate to="/forbidden" replace />;
  return <Outlet />;
}

function AccessGate() {
  const { session } = useAuth();
  const location = useLocation();
  if (location.pathname !== '/forbidden' && !canAccess(session.role, location.pathname)) {
    return <Navigate to={location.pathname === '/' ? homePath(session.role) : '/forbidden'} replace />;
  }
  return <Outlet />;
}

function Shell() {
  return (
    <div className="app-layout">
      <TopNav />
      <main className="main-content">
        <Outlet />
      </main>
    </div>
  );
}

function RoleHome() {
  const { session } = useAuth();
  if (!canAccess(session.role, '/')) return <Navigate to={homePath(session.role)} replace />;
  return <FinanceDashboard />;
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route element={<ProtectedRoute />}>
          <Route element={<AccessGate />}>
            <Route element={<Shell />}>
              <Route index element={<RoleHome />} />
              <Route path="reimbursements/:id" element={<ReimbursementDetails />} />
              <Route path="employee" element={<EmployeeWorkspace />} />
              <Route path="employee/claims/:id" element={<EmployeeClaimDetail />} />
              <Route path="department" element={<DepartmentDashboard />} />
              <Route path="forbidden" element={<div className="page-content"><div className="alert alert-danger" role="alert">Access denied.</div></div>} />
              <Route element={<ProtectedRoute roles={FINANCE_SCOPE_ROLES} />}>
                <Route path="finance-queue" element={<FinanceQueue />} />
                <Route path="budgets" element={<BudgetDashboard />} />
                <Route path="reports" element={<SpendReports />} />
                <Route path="history" element={<FinanceRequestHistory />} />
                <Route path="workflows" element={<WorkflowAudit />} />
                <Route path="workflows/:id" element={<WorkflowAudit />} />
                <Route path="policies" element={<PolicyManagement />} />
                <Route path="fraud" element={<FraudQueue />} />
                <Route path="fraud/:id" element={<FraudDetail />} />
              </Route>
              <Route element={<ProtectedRoute roles={APPROVER_ROLES} />}>
                <Route path="approvals" element={<ApprovalQueue />} />
                <Route path="approvals/requests/:id" element={<PurchaseRequestReview />} />
                <Route path="approvals/:id" element={<ReimbursementDetails />} />
              </Route>
              <Route element={<ProtectedRoute roles={ADMIN_SCOPE_ROLES} />}>
                <Route path="admin/budgets" element={<AdminBudgets />} />
                <Route path="admin/departments" element={<Departments />} />
                <Route path="admin/roles" element={<Roles />} />
              </Route>
            </Route>
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
