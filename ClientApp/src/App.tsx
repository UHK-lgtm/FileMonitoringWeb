import { useEffect, useState } from 'react'
import './App.css'
import { auditExportUrl } from './api'
import { ThemeToggle } from './components/ThemeToggle'
import { ToastProvider } from './components/Toast'
import { DefinitionsScreen } from './screens/DefinitionsScreen'
import { DeliveryDetailScreen } from './screens/DeliveryDetailScreen'
import { TodayScreen } from './screens/TodayScreen'
import { applyTheme, saveTheme, storedTheme, systemTheme, type Theme } from './theme'

type Section = 'dashboard' | 'definitions'

const sectionTitles: Record<Section, string> = {
  dashboard: "Today's monitoring",
  definitions: 'Monitoring definitions',
}

function App() {
  const [section, setSection] = useState<Section>('dashboard')
  const [openDeliveryId, setOpenDeliveryId] = useState<number | null>(null)
  const [theme, setTheme] = useState<Theme>(() => storedTheme() ?? systemTheme())

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  function toggleTheme() {
    const next: Theme = theme === 'light' ? 'dark' : 'light'
    saveTheme(next)
    setTheme(next)
  }

  function selectSection(next: Section) {
    setSection(next)
    setOpenDeliveryId(null)
  }

  const title = section === 'dashboard' && openDeliveryId !== null ? 'Delivery detail' : sectionTitles[section]

  return (
    <ToastProvider>
      <div className="app">
        <aside className="sidebar">
          <div className="brand">
            <span className="brand-mark">
              <img src="/generali-logo.png" alt="Generali" width="38" height="38" />
            </span>
            <span className="brand-text">
              Generali Investments
              <small>File Monitoring · Luxembourg</small>
            </span>
          </div>

          <nav className="nav" aria-label="Sections">
            <button
              type="button"
              className={section === 'dashboard' ? 'nav-item active' : 'nav-item'}
              aria-current={section === 'dashboard' ? 'page' : undefined}
              onClick={() => selectSection('dashboard')}
            >
              Dashboard
            </button>
            <button
              type="button"
              className={section === 'definitions' ? 'nav-item active' : 'nav-item'}
              aria-current={section === 'definitions' ? 'page' : undefined}
              onClick={() => selectSection('definitions')}
            >
              Definitions
            </button>
          </nav>

          <div className="sidebar-foot">Business dates in Europe/Luxembourg</div>
        </aside>

        <div className="main">
          <header className="topbar">
            <div>
              <p className="eyebrow">File Monitoring</p>
              <h1>{title}</h1>
            </div>
            <div className="topbar-actions">
              <ThemeToggle theme={theme} onToggle={toggleTheme} />
              <a className="button secondary" href={auditExportUrl}>
                Export to Excel
              </a>
            </div>
          </header>

          <main className="page">
            {section === 'dashboard' && openDeliveryId === null && <TodayScreen onOpen={setOpenDeliveryId} />}
            {section === 'dashboard' && openDeliveryId !== null && (
              <DeliveryDetailScreen expectedDeliveryId={openDeliveryId} onBack={() => setOpenDeliveryId(null)} />
            )}
            {section === 'definitions' && <DefinitionsScreen />}
          </main>
        </div>
      </div>
    </ToastProvider>
  )
}

export default App
