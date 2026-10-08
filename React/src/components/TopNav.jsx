import { useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { FiChevronDown, FiMenu, FiMoon, FiSun, FiX } from 'react-icons/fi';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '../auth/AuthContext';
import { useTheme } from '../auth/ThemeContext';
import { isNavActive, navSectionsFor } from '../auth/access';
import { formatDesignation } from './Shared';
import { getMyProfile } from '../services/api';

function displayRole(role) {
  if (role === 'DepartmentHead') return 'Director';
  if (role === 'Admin') return 'C-Level';
  return role;
}

export default function TopNav() {
  const { session, logout } = useAuth();
  const { isDark, toggleTheme } = useTheme();
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const sections = navSectionsFor(session.role);
  const profile = useQuery({ queryKey: ['employee-profile'], queryFn: getMyProfile });
  const identity = profile.data
    ? `${session.username} · ${formatDesignation(profile.data)}`
    : `${session.username} · ${displayRole(session.role)}`;

  return (
    <header className="top-nav">
      <div className="top-nav-brand">
        <div className="logo-icon" aria-hidden="true">EG</div>
        <div>
          <strong>ExpenseGuard</strong>
          <span>Corporate expense control</span>
        </div>
      </div>
      <button className="btn btn-ghost btn-sm top-nav-toggle" type="button" aria-expanded={open}
        aria-label={open ? 'Close navigation' : 'Open navigation'} onClick={() => setOpen(value => !value)}>
        {open ? <FiX /> : <FiMenu />}
      </button>
      <nav className={`top-nav-links ${open ? 'open' : ''}`} aria-label="Primary">
        {sections.map(section => (
          <NavSection key={section.id} section={section} pathname={location.pathname}
            onNavigate={() => setOpen(false)} />
        ))}
      </nav>
      <div className="top-nav-actions">
        <button className="btn btn-ghost btn-sm" type="button" onClick={toggleTheme}
          aria-label={isDark ? 'Switch to light mode' : 'Switch to dark mode'}>
          {isDark ? <FiSun /> : <FiMoon />}
          <span className="top-nav-theme-label">{isDark ? 'Light' : 'Dark'}</span>
        </button>
        <span className="top-nav-user">{identity}</span>
        <button className="btn btn-ghost btn-sm" type="button" onClick={logout}>Sign out</button>
      </div>
    </header>
  );
}

function NavSection({ section, pathname, onNavigate }) {
  if (section.items.length === 1) {
    const item = section.items[0];
    return (
      <Link to={item.to} className={`top-nav-link ${isNavActive(pathname, item.to) ? 'active' : ''}`} onClick={onNavigate}>
        {item.label}
      </Link>
    );
  }

  const active = section.items.some(item => isNavActive(pathname, item.to));
  return (
    <div className={`top-nav-menu ${active ? 'active' : ''}`}>
      <button className="top-nav-link top-nav-menu-button" type="button" aria-haspopup="true">
        {section.label}
        <FiChevronDown />
      </button>
      <div className="top-nav-menu-panel">
        {section.items.map(item => (
          <Link key={item.to} to={item.to} className={`top-nav-link ${isNavActive(pathname, item.to) ? 'active' : ''}`}
            onClick={onNavigate}>
            {item.label}
          </Link>
        ))}
      </div>
    </div>
  );
}
