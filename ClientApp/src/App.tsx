import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import './App.css'
import { auditExportUrl } from './api'
import { ThemeToggle } from './components/ThemeToggle'
import { ToastProvider } from './components/Toast'
import { DataHealthScreen } from './screens/DataHealthScreen'
import { DeliveriesStatusScreen } from './screens/DeliveriesStatusScreen'
import { DeliveryDetailScreen } from './screens/DeliveryDetailScreen'
import { applyTheme, saveTheme, storedTheme, systemTheme, type Theme } from './theme'

type Section = 'status' | 'health'

const sectionTitles: Record<Section, string> = {
  status: 'Deliveries status',
  health: 'Data Health',
}

function App() {
  const [section, setSection] = useState<Section>('status')
  const [openDeliveryId, setOpenDeliveryId] = useState<number | null>(null)
  const [theme, setTheme] = useState<Theme>(() => storedTheme() ?? systemTheme())
  // Where the page was scrolled when a delivery opened, so going back returns to the same place.
  const listScrollY = useRef(0)

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  // The list stays mounted while a delivery is open, so its date and filters survive. Restore the scroll once it is shown again.
  useLayoutEffect(() => {
    if (openDeliveryId === null) {
      window.scrollTo(0, listScrollY.current)
    }
  }, [openDeliveryId])

  function openDelivery(expectedDeliveryId: number) {
    listScrollY.current = window.scrollY
    setOpenDeliveryId(expectedDeliveryId)
    window.scrollTo(0, 0)
  }

  function toggleTheme() {
    const next: Theme = theme === 'light' ? 'dark' : 'light'
    saveTheme(next)
    setTheme(next)
  }

  function selectSection(next: Section) {
    setSection(next)
    setOpenDeliveryId(null)
    // Switching tabs starts at the top, not at the position saved for the delivery list.
    listScrollY.current = 0
  }

  const title = section === 'status' && openDeliveryId !== null ? 'Delivery detail' : sectionTitles[section]

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
              className={section === 'status' ? 'tab on' : 'tab'}
              aria-current={section === 'status' ? 'page' : undefined}
              onClick={() => selectSection('status')}
            >
              Deliveries status
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
            {section === 'status' && (
              <div hidden={openDeliveryId !== null}>
                <DeliveriesStatusScreen onOpen={openDelivery} />
              </div>
            )}
            {section === 'status' && openDeliveryId !== null && (
              <DeliveryDetailScreen expectedDeliveryId={openDeliveryId} onBack={() => setOpenDeliveryId(null)} />
            )}
            {section === 'health' && <DataHealthScreen />}
          </main>
        </div>
      </div>
    </ToastProvider>
  )
}

export default App
