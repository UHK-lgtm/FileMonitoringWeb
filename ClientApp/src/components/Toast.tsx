import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react'
import { ToastContext, type Notify } from './toastContext'

interface ToastItem {
  id: number
  text: string
  tone: 'success' | 'error'
}

const toastLifetimeMs = 4000

/// Shows short confirmations (saved, enabled, comment added) in the corner of the screen.
export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([])
  const nextId = useRef(1)

  const notify = useCallback<Notify>((text, tone = 'success') => {
    const id = nextId.current
    nextId.current += 1
    setItems((current) => [...current, { id, text, tone }])
    window.setTimeout(() => {
      setItems((current) => current.filter((item) => item.id !== id))
    }, toastLifetimeMs)
  }, [])

  const value = useMemo(() => notify, [notify])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="toasts" aria-live="polite">
        {items.map((item) => (
          <div key={item.id} className={`toast toast-${item.tone}`} role="status">
            {item.text}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}
