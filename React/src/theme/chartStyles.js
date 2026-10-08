export function cssVar(name) {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

export function chartTooltipStyle() {
  return {
    backgroundColor: cssVar('--bg-card'),
    border: `1px solid ${cssVar('--border')}`,
    borderRadius: 8,
    padding: '10px 14px',
    color: cssVar('--text-primary'),
    fontSize: 13,
  };
}

export function chartAxis(theme) {
  return {
    tick: theme === 'light' ? '#475569' : '#94a3b8',
    grid: theme === 'light' ? '#e2e8f0' : '#1e293b',
  };
}
