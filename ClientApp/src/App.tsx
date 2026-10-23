import { useState } from 'react'
import './App.css'
import { auditExportUrl } from './api'
import { ToastProvider } from './components/Toast'
import { DefinitionsScreen } from './screens/DefinitionsScreen'
import { DeliveryDetailScreen } from './screens/DeliveryDetailScreen'
import { TodayScreen } from './screens/TodayScreen'

type Section = 'dashboard' | 'definitions'

const sectionTitles: Record<Section, string> = {
  dashboard: "Today's monitoring",
  definitions: 'Monitoring definitions',
}

function App() {
  const [section, setSection] = useState<Section>('dashboard')
  const [openDeliveryId, setOpenDeliveryId] = useState<number | null>(null)

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
            <span className="brand-mark" aria-hidden="true">
              GI
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
            <a className="button secondary" href={auditExportUrl}>
              Export to Excel
            </a>
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
