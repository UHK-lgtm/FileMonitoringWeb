import { useEffect, useState } from 'react'
import { api, messagesFrom, type AuditEntry, type MonitoringDefinition } from '../api'
import { DefinitionForm } from '../components/DefinitionForm'
import { ErrorList } from '../components/ErrorList'
import { formatDateTime } from '../format'

// null: nothing selected. 'new': the create form. A number: the definition with that id.
type Selection = number | 'new' | null

export function DefinitionsScreen() {
  const [definitions, setDefinitions] = useState<MonitoringDefinition[]>([])
  const [selection, setSelection] = useState<Selection>(null)
  const [audit, setAudit] = useState<{ id: number; entries: AuditEntry[] } | null>(null)
  const [revision, setRevision] = useState(0)
  const [errors, setErrors] = useState<string[]>([])
  const [message, setMessage] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<number | null>(null)

  useEffect(() => {
    let ignore = false
    api
      .getDefinitions()
      .then((list) => {
        if (!ignore) {
          setDefinitions(list)
          setErrors([])
        }
      })
      .catch((error: unknown) => {
        if (!ignore) {
          setErrors(messagesFrom(error))
        }
      })

    return () => {
      ignore = true
    }
  }, [revision])

  const selectedId = typeof selection === 'number' ? selection : null
  const selected = definitions.find((definition) => definition.id === selectedId) ?? null
  const auditEntries = audit !== null && audit.id === selectedId ? audit.entries : []

  useEffect(() => {
    if (selectedId === null) {
      return
    }

    let ignore = false
    api
      .getAudit(selectedId)
      .then((entries) => {
        if (!ignore) {
          setAudit({ id: selectedId, entries })
        }
      })
      .catch((error: unknown) => {
        if (!ignore) {
          setErrors(messagesFrom(error))
        }
      })

    return () => {
      ignore = true
    }
  }, [selectedId, revision])

  function afterChange(text: string) {
    setMessage(text)
    setRevision((current) => current + 1)
  }

  async function toggleEnabled(definition: MonitoringDefinition) {
    setBusyId(definition.id)
    setMessage(null)
    try {
      await api.setEnabled(definition.id, !definition.isEnabled)
      afterChange(definition.isEnabled ? `"${definition.name}" disabled.` : `"${definition.name}" enabled.`)
    } catch (error) {
      setErrors(messagesFrom(error))
    } finally {
      setBusyId(null)
    }
  }

  async function retire(definition: MonitoringDefinition) {
    const confirmed = window.confirm(
      `Retire "${definition.name}"? It stops generating deliveries. Its history is kept and it cannot be changed afterwards.`,
    )
    if (!confirmed) {
      return
    }

    setBusyId(definition.id)
    setMessage(null)
    try {
      await api.retireDefinition(definition.id)
      afterChange(`"${definition.name}" retired.`)
    } catch (error) {
      setErrors(messagesFrom(error))
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section className="definitions">
      <div className="card">
        <div className="toolbar">
          <h2>Monitoring definitions</h2>
          <button type="button" className="button" onClick={() => setSelection('new')}>
            New definition
          </button>
        </div>

        <ErrorList messages={errors} />
        {message && (
          <p className="notice success" role="status">
            {message}
          </p>
        )}

        {definitions.length === 0 && errors.length === 0 && <p className="muted">No definitions yet.</p>}

        {definitions.length > 0 && (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Frequency</th>
                  <th>Deadline</th>
                  <th>State</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {definitions.map((definition) => (
                  <tr key={definition.id} className={definition.isRetired ? 'retired' : undefined}>
                    <td>
                      <button type="button" className="link-button" onClick={() => setSelection(definition.id)}>
                        {definition.name}
                      </button>
                    </td>
                    <td>{definition.frequency}</td>
                    <td>
                      {definition.expectedDeliveryTime} {definition.timeZoneId}
                    </td>
                    <td>
                      {definition.isRetired ? 'Retired' : definition.isEnabled ? 'Enabled' : 'Disabled'}
                    </td>
                    <td className="row-actions">
                      {!definition.isRetired && (
                        <>
                          <button
                            type="button"
                            className="button secondary"
                            disabled={busyId === definition.id}
                            onClick={() => void toggleEnabled(definition)}
                          >
                            {definition.isEnabled ? 'Disable' : 'Enable'}
                          </button>
                          <button
                            type="button"
                            className="button danger"
                            disabled={busyId === definition.id}
                            onClick={() => void retire(definition)}
                          >
                            Retire
                          </button>
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {selection === 'new' && (
        <div className="card">
          <DefinitionForm
            key="new"
            definition={null}
            readOnly={false}
            onSaved={(saved) => {
              setSelection(saved.id)
              void afterChange(`"${saved.name}" created.`)
            }}
            onConflict={() => setRevision((current) => current + 1)}
            onCancel={() => setSelection(null)}
          />
        </div>
      )}

      {selected !== null && (
        <>
          <div className="card">
            <DefinitionForm
              key={selected.id}
              definition={selected}
              readOnly={selected.isRetired}
              onSaved={(saved) => void afterChange(`"${saved.name}" saved.`)}
              onConflict={() => setRevision((current) => current + 1)}
              onCancel={() => setSelection(null)}
            />
          </div>

          <div className="card">
            <h3>Change history</h3>
            {auditEntries.length === 0 ? (
              <p className="muted">No changes recorded.</p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>When</th>
                      <th>Who</th>
                      <th>Field</th>
                      <th>Old value</th>
                      <th>New value</th>
                    </tr>
                  </thead>
                  <tbody>
                    {auditEntries.map((entry) => (
                      <tr key={entry.auditId}>
                        <td>{formatDateTime(entry.changedAtUtc)}</td>
                        <td>{entry.changedBy}</td>
                        <td>{entry.fieldName}</td>
                        <td className="mono">{entry.oldValue ?? '—'}</td>
                        <td className="mono">{entry.newValue ?? '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </section>
  )
}
