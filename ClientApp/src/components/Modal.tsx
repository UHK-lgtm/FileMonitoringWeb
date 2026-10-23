import { useEffect, useId, type ReactNode } from 'react'

interface ModalProps {
  title: string
  onClose: () => void
  children: ReactNode
}

/// A dialog over the page. Closes with Escape, the close button, or a click on the backdrop.
export function Modal({ title, onClose, children }: ModalProps) {
  const titleId = useId()

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  return (
    <div
      className="modal-backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose()
        }
      }}
    >
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby={titleId}>
        <div className="modal-head">
          <h2 id={titleId}>{title}</h2>
          <button type="button" className="button secondary" onClick={onClose} aria-label="Close dialog">
            Close
          </button>
        </div>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  )
}
