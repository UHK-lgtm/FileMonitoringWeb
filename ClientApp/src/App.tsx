import { useState } from 'react'
import './App.css'
import { auditExportUrl } from './api'
import { DefinitionsScreen } from './screens/DefinitionsScreen'
import { DeliveryDetailScreen } from './screens/DeliveryDetailScreen'
import { TodayScreen } from './screens/TodayScreen'

type Tab = 'today' | 'definitions'

function App() {
  const [tab, setTab] = useState<Tab>('today')
  const [openDeliveryId, setOpenDeliveryId] = useState<number | null>(null)

  function selectTab(next: Tab) {
    setTab(next)
    setOpenDeliveryId(null)
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>File Monitoring</h1>
        <a className="button secondary" href={auditExportUrl}>
          Export audit (Excel)
        </a>
      </header>

      <nav className="tabs" aria-label="Sections">
        <button
          type="button"
          className={tab === 'today' ? 'tab active' : 'tab'}
          aria-pressed={tab === 'today'}
          onClick={() => selectTab('today')}
        >
          Today
        </button>
        <button
          type="button"
          className={tab === 'definitions' ? 'tab active' : 'tab'}
          aria-pressed={tab === 'definitions'}
          onClick={() => selectTab('definitions')}
        >
          Definitions
        </button>
      </nav>

      <main className="page">
        {tab === 'today' && openDeliveryId === null && <TodayScreen onOpen={setOpenDeliveryId} />}
        {tab === 'today' && openDeliveryId !== null && (
          <DeliveryDetailScreen expectedDeliveryId={openDeliveryId} onBack={() => setOpenDeliveryId(null)} />
        )}
        {tab === 'definitions' && <DefinitionsScreen />}
      </main>
    </div>
  )
}

export default App
