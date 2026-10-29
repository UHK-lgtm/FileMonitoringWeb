import { useEffect, useMemo, useState } from 'react'
import { api, messagesFrom, type AuditEntry, type MonitoringDefinition } from '../api'
import { DefinitionForm } from '../components/DefinitionForm'
import { ErrorList } from '../components/ErrorList'
import { StatCard } from '../components/StatCard'
import { useToast } from '../components/toastContext'
import { daysLabel } from '../definitionRules'
import { formatDateTime } from '../format'

// null: the list. 'new': the create page. A number: the edit page for the definition with that id.
type Selection = number | 'new' | null
type StateFilter = 'All' | 'Enabled' | 'Disabled' | 'Retired'

function stateOf(definition: MonitoringDefinition): Exclude<StateFilter, 'All'> {
  if (definition.isRetired) {
    return 'Retired'
  }
  return definition.isEnabled ? 'Enabled' : 'Disabled'
}

export function DefinitionsScreen() {
  const [definitions, setDefinitions] = useState<MonitoringDefinition[]>([])
  const [selection, setSelection] = useState<Selection>(null)
  const [audit, setAudit] = useState<{ id: number; entries: AuditEntry[] } | null>(null)
  const [revision, setRevision] = useState(0)
  const [errors, setErrors] = useState<string[]>([])
  const [busyId, setBusyId] = useState<number | null>(null)
  const [filter, setFilter] = useState<StateFilter>('All')
  const [search, setSearch] = useState('')
  const notify = useToast()

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

  const counts = useMemo(() => {
    const result = { total: definitions.length, enabled: 0, disabled: 0, retired: 0 }
    for (const definition of definitions) {
      const state = stateOf(definition)
      if (state === 'Enabled') result.enabled += 1
      if (state === 'Disabled') result.disabled += 1
      if (state === 'Retired') result.retired += 1
    }
    return result
  }, [definitions])

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase()
    return definitions.filter(
      (definition) =>
        (filter === 'All' || stateOf(definition) === filter) &&
        (term.length === 0 ||
          definition.name.toLowerCase().includes(term) ||
          definition.relativePath.toLowerCase().includes(term)),
    )
  }, [definitions, filter, search])

  function afterChange(text: string) {
    notify(text)
    setRevision((current) => current + 1)
  }

  async function toggleEnabled(definition: MonitoringDefinition) {
    setBusyId(definition.id)
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
    try {
      await api.retireDefinition(definition.id)
      afterChange(`"${definition.name}" retired.`)
    } catch (error) {
      setErrors(messagesFrom(error))
    } finally {
      setBusyId(null)
    }
  }

  // The editor is its own page, like the delivery detail: a back button returns to the list.
  if (selection !== null) {
    const isNew = selection === 'new'
    const title = isNew ? 'New definition' : selected?.name ?? 'Loading…'

    return (
      <>
        <div className="toolbar">
          <button type="button" className="button secondary" onClick={() => setSelection(null)}>
            ← Back to definitions
          </button>
        </div>

        {isNew && (
          <section className="card">
            <p className="eyebrow">Configuration</p>
            <h2>{title}</h2>
            <DefinitionForm
              key="new"
              definition={null}
              readOnly={false}
              onSaved={(saved) => {
                setSelection(saved.id)
                afterChange(`"${saved.name}" created.`)
              }}
              onConflict={() => setRevision((current) => current + 1)}
              onCancel={() => setSelection(null)}
            />
          </section>
        )}

        {!isNew && selected === null && <p className="muted">Loading…</p>}

        {!isNew && selected !== null && (
          <>
            <section className="card">
              <p className="eyebrow">{selected.isRetired ? 'Retired definition' : 'Configuration'}</p>
              <h2>{title}</h2>
              <DefinitionForm
                key={selected.id}
                definition={selected}
                readOnly={selected.isRetired}
                onSaved={(saved) => afterChange(`"${saved.name}" saved.`)}
                onConflict={() => setRevision((current) => current + 1)}
                onCancel={() => setSelection(null)}
              />
            </section>

            <section className="card">
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
            </section>
          </>
        )}
      </>
    )
  }

  return (
    <>
      <div className="kpis">
        <StatCard label="Definitions" value={counts.total} tone="neutral" hint="All, including retired" />
        <StatCard label="Enabled" value={counts.enabled} tone="ok" hint="Generating deliveries" />
        <StatCard label="Disabled" value={counts.disabled} tone="warn" hint="Paused, can be re-enabled" />
        <StatCard label="Retired" value={counts.retired} tone="neutral" hint="History kept" />
      </div>

      <section className="card">
        <div className="toolbar">
          <h2>Definitions</h2>
          <button type="button" className="button" onClick={() => setSelection('new')}>
            New definition
          </button>
        </div>

        <ErrorList messages={errors} />

        <div className="filters">
          {(['All', 'Enabled', 'Disabled', 'Retired'] as StateFilter[]).map((option) => (
            <button
              key={option}
              type="button"
              className={filter === option ? 'chip active' : 'chip'}
              onClick={() => setFilter(option)}
            >
              {option}{' '}
              <span>
                {option === 'All' ? counts.total : option === 'Enabled' ? counts.enabled : option === 'Disabled' ? counts.disabled : counts.retired}
              </span>
            </button>
          ))}
          <input
            className="search"
            type="search"
            placeholder="Search by name or folder"
            aria-label="Search definitions"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>

        {definitions.length === 0 && errors.length === 0 && (
          <div className="empty">
            <strong>No definitions yet</strong>
            <span>Create the first definition to start monitoring a provider folder.</span>
          </div>
        )}

        {definitions.length > 0 && visible.length === 0 && (
          <div className="empty">
            <strong>No definitions match</strong>
            <span>Try another state or clear the search.</span>
          </div>
        )}

        {visible.length > 0 && (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Schedule</th>
                  <th>Deadline</th>
                  <th>State</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {visible.map((definition) => {
                  const state = stateOf(definition)
                  const pillClass =
                    state === 'Enabled' ? 'pill pill-on' : state === 'Disabled' ? 'pill pill-off' : 'pill pill-retired'
                  return (
                    <tr key={definition.id} className={definition.isRetired ? 'retired' : undefined}>
                      <td>
                        <button type="button" className="link-button" onClick={() => setSelection(definition.id)}>
                          {definition.name}
                        </button>
                        <div className="muted small mono">{definition.relativePath || '(share root)'}</div>
                      </td>
                      <td>
                        {definition.frequency}
                        <div className="muted small">{daysLabel(definition.daysOfWeek)}</div>
                        <div className="muted small">
                          {definition.fileNamePattern}
                          {definition.matchType === 'Regex' ? ' (regex)' : ''}
                        </div>
                      </td>
                      <td>
                        {definition.expectedDeliveryTime}
                        <div className="muted small">{definition.timeZoneId}</div>
                      </td>
                      <td>
                        <span className={pillClass}>{state}</span>
                      </td>
                      <td className="actions-cell">
                        <div className="row-actions">
                        <button type="button" className="button secondary" onClick={() => setSelection(definition.id)}>
                          {definition.isRetired ? 'View' : 'Edit'}
                        </button>
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
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}
