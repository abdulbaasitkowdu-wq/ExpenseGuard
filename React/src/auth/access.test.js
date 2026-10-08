import { describe, expect, it } from 'vitest';
import { canAccess, homePath, navSectionsFor } from './access';

describe('role access', () => {
  it('limits employees to expenses and department dashboard', () => {
    expect(homePath('Employee')).toBe('/employee');
    expect(canAccess('Employee', '/employee')).toBe(true);
    expect(canAccess('Employee', '/department')).toBe(true);
    expect(canAccess('Employee', '/')).toBe(false);
    expect(canAccess('Employee', '/approvals')).toBe(false);
    expect(canAccess('Employee', '/admin/roles')).toBe(false);
    expect(canAccess('Employee', '/history')).toBe(false);
    expect(navSectionsFor('Employee').flatMap(section => section.items.map(item => item.to)))
      .toEqual(['/employee', '/department']);
  });

  it('gives managers expenses, department, and approvals without finance admin tools', () => {
    expect(homePath('Manager')).toBe('/employee');
    expect(canAccess('Manager', '/employee')).toBe(true);
    expect(canAccess('Manager', '/department')).toBe(true);
    expect(canAccess('Manager', '/approvals')).toBe(true);
    expect(canAccess('Manager', '/approvals/requests/3')).toBe(true);
    expect(canAccess('Manager', '/')).toBe(false);
    expect(canAccess('Manager', '/finance-queue')).toBe(false);
    expect(canAccess('Manager', '/admin/roles')).toBe(false);
  });

  it('gives finance broad access but not admin pages', () => {
    expect(homePath('Finance')).toBe('/');
    expect(canAccess('Finance', '/finance-queue')).toBe(true);
    expect(canAccess('Finance', '/approvals')).toBe(true);
    expect(canAccess('Finance', '/policies')).toBe(true);
    expect(canAccess('Finance', '/history')).toBe(true);
    expect(canAccess('Finance', '/admin/roles')).toBe(false);
  });

  it('keeps directors on department leadership pages without company admin tools', () => {
    expect(canAccess('DepartmentHead', '/employee')).toBe(true);
    expect(canAccess('DepartmentHead', '/department')).toBe(true);
    expect(canAccess('DepartmentHead', '/approvals')).toBe(true);
    expect(canAccess('DepartmentHead', '/')).toBe(false);
    expect(canAccess('DepartmentHead', '/finance-queue')).toBe(false);
    expect(canAccess('DepartmentHead', '/admin/roles')).toBe(false);
  });

  it('gives C-level admins full application access', () => {
    expect(homePath('Admin')).toBe('/');
    expect(canAccess('Admin', '/')).toBe(true);
    expect(canAccess('Admin', '/finance-queue')).toBe(true);
    expect(canAccess('Admin', '/admin/roles')).toBe(true);
    expect(canAccess('Admin', '/department')).toBe(true);
  });
});
