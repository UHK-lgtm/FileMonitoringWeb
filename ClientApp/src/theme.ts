export type Theme = 'light' | 'dark'

const storageKey = 'fm-theme'

/// The theme the operating system asks for. Used until the viewer makes a choice.
export function systemTheme(): Theme {
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

/// The viewer's saved choice in this browser, or null. Browser storage can be blocked, so a failure means no choice.
export function storedTheme(): Theme | null {
  try {
    const value = window.localStorage.getItem(storageKey)
    return value === 'light' || value === 'dark' ? value : null
  } catch {
    return null
  }
}

/// Remembers the choice in this browser only. If storage is blocked, the choice lasts for this visit.
export function saveTheme(theme: Theme) {
  try {
    window.localStorage.setItem(storageKey, theme)
  } catch {
    // Storage is unavailable (private window or blocked site data). The page still switches theme.
  }
}

/// Sets the theme on the page root. The stylesheet reads it, and so do native controls through color-scheme.
export function applyTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme
  document.documentElement.style.colorScheme = theme
}
