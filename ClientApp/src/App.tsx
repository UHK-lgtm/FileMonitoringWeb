import { useEffect, useState } from 'react'
import './App.css'
import { auditExportUrl } from './api'
import { ThemeToggle } from './components/ThemeToggle'
import { ToastProvider } from './components/Toast'
import { DefinitionsScreen } from './screens/DefinitionsScreen'
import { DataHealthScreen } from './screens/DataHealthScreen'
import { DeliveryDetailScreen } from './screens/DeliveryDetailScreen'
import { TodayScreen } from './screens/TodayScreen'
import { applyTheme, saveTheme, storedTheme, systemTheme, type Theme } from './theme'

type Section = 'dashboard' | 'definitions' | 'health'

const sectionTitles: Record<Section, string> = {
  dashboard: "Today's monitoring",
  definitions: 'Monitoring definitions',
  health: 'Data Health',
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
        <header className="topnav">
          <div className="brand">
            <span className="brand-mark">
              <img src="/generali-logo.png" alt="Generali" width="38" height="38" />
            </span>
            <span className="brand-text">
              Generali Investments
              <small>File Monitoring · Luxembourg</small>
            </span>
          </div>

          <nav className="tabs" aria-label="Sections">
            <button
              type="button"
              className={section === 'dashboard' ? 'tab on' : 'tab'}
              aria-current={section === 'dashboard' ? 'page' : undefined}
              onClick={() => selectSection('dashboard')}
            >
              Today
            </button>
            <button
              type="button"
              className={section === 'definitions' ? 'tab on' : 'tab'}
              aria-current={section === 'definitions' ? 'page' : undefined}
              onClick={() => selectSection('definitions')}
            >
              Definitions
            </button>
            <button
              type="button"
              className={section === 'health' ? 'tab on' : 'tab'}
              aria-current={section === 'health' ? 'page' : undefined}
              onClick={() => selectSection('health')}
            >
              Data Health
            </button>
          </nav>

          <div className="topnav-actions">
            <ThemeToggle theme={theme} onToggle={toggleTheme} />
            <a className="button secondary" href={auditExportUrl}>
              Export to Excel
            </a>
          </div>
        </header>

        <div className="main">
          <header className="page-head">
            <p className="eyebrow">Business dates in Europe/Luxembourg</p>
            <h1>{title}</h1>
          </header>

          <main className="page">
            {section === 'dashboard' && openDeliveryId === null && <TodayScreen onOpen={setOpenDeliveryId} />}
            {section === 'dashboard' && openDeliveryId !== null && (
              <DeliveryDetailScreen expectedDeliveryId={openDeliveryId} onBack={() => setOpenDeliveryId(null)} />
            )}
            {section === 'definitions' && <DefinitionsScreen />}
            {section === 'health' && <DataHealthScreen />}
          </main>
        </div>
      </div>
    </ToastProvider>
  )
}

export default App
