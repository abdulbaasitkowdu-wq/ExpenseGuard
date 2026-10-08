import DepartmentAdmin from '../features/department/components/DepartmentAdmin';

export default function Departments() {
  return <div className="page-content"><header className="feature-header"><div><h2>Department administration</h2><p>Create and maintain departments used by budget allocation.</p></div></header><DepartmentAdmin /></div>;
}
