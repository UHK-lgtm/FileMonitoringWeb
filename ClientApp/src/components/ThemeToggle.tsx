import type { Theme } from '../theme'

interface ThemeToggleProps {
  theme: Theme
  onToggle: () => void
}

/// A small sun or moon button. The icon shows the theme the click switches to: a moon in light mode, a sun in dark mode.
export function ThemeToggle({ theme, onToggle }: ThemeToggleProps) {
  const isDark = theme === 'dark'
  const label = isDark ? 'Switch to light theme' : 'Switch to dark theme'

  return (
    <button type="button" className="theme-toggle" onClick={onToggle} aria-label={label} title={label}>
      {isDark ? (
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <circle cx="12" cy="12" r="4" fill="currentColor" />
          <g stroke="currentColor" strokeWidth="2" strokeLinecap="round">
            <line x1="12" y1="2" x2="12" y2="4.5" />
            <line x1="12" y1="19.5" x2="12" y2="22" />
            <line x1="2" y1="12" x2="4.5" y2="12" />
            <line x1="19.5" y1="12" x2="22" y2="12" />
            <line x1="4.9" y1="4.9" x2="6.7" y2="6.7" />
            <line x1="17.3" y1="17.3" x2="19.1" y2="19.1" />
            <line x1="4.9" y1="19.1" x2="6.7" y2="17.3" />
            <line x1="17.3" y1="6.7" x2="19.1" y2="4.9" />
          </g>
        </svg>
      ) : (
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <path
            d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"
            fill="currentColor"
          />
        </svg>
      )}
    </button>
  )
}
