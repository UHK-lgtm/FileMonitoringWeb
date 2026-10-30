import { useState } from 'react'
import { DefinitionsScreen } from './DefinitionsScreen'
import { TodayScreen } from './TodayScreen'

type View = 'status' | 'definitions'

interface DeliveriesStatusScreenProps {
  onOpen: (expectedDeliveryId: number) => void
}

// The deliveries page: today's status, and the monitoring definitions it is built from, as two views of one page.
export function DeliveriesStatusScreen({ onOpen }: DeliveriesStatusScreenProps) {
  const [view, setView] = useState<View>('status')

  return (
    <>
      <div className="toolbar">
        <div className="tabs" role="tablist" aria-label="Deliveries views">
          <button type="button" role="tab" aria-selected={view === 'status'} className={view === 'status' ? 'tab on' : 'tab'} onClick={() => setView('status')}>
            Status
          </button>
          <button type="button" role="tab" aria-selected={view === 'definitions'} className={view === 'definitions' ? 'tab on' : 'tab'} onClick={() => setView('definitions')}>
            Definitions
          </button>
        </div>
      </div>

      {view === 'status' ? <TodayScreen onOpen={onOpen} /> : <DefinitionsScreen />}
    </>
  )
}
