import { useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  api,
  messagesFrom,
  type ApprovedSource,
  type AuditEntry,
  type DataHealthControl,
  type DataHealthControlInput,
  type DataHealthControlResult,
  type DataHealthResults,
  type DataHealthStatus,
} from '../api'
import { ErrorList } from '../components/ErrorList'
import { useToast } from '../components/toastContext'
import { formatDateTime } from '../format'

type View = 'results' | 'configure'

const statusLabels: Record<DataHealthStatus, string> = {
  Missing: 'Missing',
  Decrease: 'Decrease',
  Increase: 'Increase',
  Stable: 'Stable',
  NoHistory: 'No history',
  Error: 'Error',
}

// Reuses the status badge colours that the rest of the app already has.
const statusBadge: Record<DataHealthStatus, string> = {
  Missing: 'status-Missing',
  Decrease: 'status-Incomplete',
  Increase: 'status-OnTime',
  Stable: 'status-OnTime',
  NoHistory: 'status-Pending',
  Error: 'status-Missing',
}

function formatDate(value: string | null): string {
  return value === null ? '—' : new Date(`${value}T00:00:00`).toLocaleDateString(undefined, { dateStyle: 'medium' })
}

function formatCount(value: number | null): string {
  return value === null ? '—' : value.toLocaleString()
}

function Sparkline({ points }: { points: { count: number }[] }) {
  if (points.length === 0) {
    return <span className="muted">—</span>
  }

  const max = Math.max(1, ...points.map((point) => point.count))
  const width = 120
  const height = 32
  const step = points.length > 1 ? width / (points.length - 1) : 0
  const path = points
    .map((point, index) => `${index * step},${height - (point.count / max) * (height - 4) - 2}`)
    .join(' ')

  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Record count trend">
      <polyline points={path} fill="none" stroke="var(--c-brand)" strokeWidth="2" strokeLinejoin="round" />
    </svg>
  )
}

export function DataHealthScreen() {
  const [view, setView] = useState<View>('results')

  return (
    <>
      <div className="toolbar">
        <div className="tabs" role="tablist" aria-label="Data Health views">
          <button type="button" role="tab" aria-selected={view === 'results'} className={view === 'results' ? 'tab on' : 'tab'} onClick={() => setView('results')}>
            Results
          </button>
          <button type="button" role="tab" aria-selected={view === 'configure'} className={view === 'configure' ? 'tab on' : 'tab'} onClick={() => setView('configure')}>
            Configure
          </button>
        </div>
      </div>

      {view === 'results' ? <ResultsView /> : <ConfigureView />}
    </>
  )
}

function ResultsView() {
  const [results, setResults] = useState<DataHealthResults | null>(null)
  const [errors, setErrors] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let ignore = false
    api
      .getDataHealth()
      .then((response) => {
        if (!ignore) {
          setResults(response)
          setErrors([])
        }
      })
      .catch((error: unknown) => {
        if (!ignore) {
          setErrors(messagesFrom(error))
        }
      })
      .finally(() => {
        if (!ignore) {
          setLoading(false)
        }
      })

    return () => {
      ignore = true
    }
  }, [reloadKey])

  function refresh() {
    setLoading(true)
    setReloadKey((key) => key + 1)
  }

  return (
    <>
      <div className="toolbar">
        <div>
          <h2>Data Health</h2>
          {results && <span className="muted small">Refreshed {formatDateTime(results.refreshedAtUtc)}</span>}
        </div>
        <button type="button" className="button secondary" onClick={refresh} disabled={loading}>
          Refresh
        </button>
      </div>

      <ErrorList messages={errors} />

      {loading && results === null && <p className="muted">Running the checks…</p>}

      {results !== null && (
        <>
          <section className="summary-strip" aria-label="Data Health summary">
            <div className="summary-cell">
              <span className="summary-label">Control date</span>
              <span className="summary-value">{formatDate(results.today)}</span>
              <span className="summary-hint">Today in Europe/Luxembourg</span>
            </div>
            <div className="summary-cell">
              <span className="summary-label">Enabled sources</span>
              <span className="summary-value">{results.enabledSources}</span>
              <span className="summary-hint">Checks that ran</span>
            </div>
            <div className="summary-cell">
              <span className="summary-label">Missing</span>
              <span className="summary-value attention">{results.missingCount}</span>
              <span className="summary-hint">No data on the control date</span>
            </div>
            <div className="summary-cell">
              <span className="summary-label">Decrease</span>
              <span className="summary-value warn">{results.decreaseCount}</span>
              <span className="summary-hint">Lower than the comparison</span>
            </div>
            <div className="summary-cell">
              <span className="summary-label">Overall</span>
              <span className={results.healthy ? 'summary-value ok' : 'summary-value attention'}>
                {results.healthy ? 'Healthy' : 'Attention'}
              </span>
              <span className="summary-hint">{results.errorCount > 0 ? `${results.errorCount} check(s) did not run` : 'No open attention items'}</span>
            </div>
          </section>

          <section className="card">
            {results.controls.length === 0 ? (
              <div className="empty">
                <strong>No enabled controls</strong>
                <span>Add a control on the Configure tab. It must use an approved source.</span>
              </div>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Data source</th>
                      <th>Control date</th>
                      <th>Count</th>
                      <th>Comparison</th>
                      <th>Previous</th>
                      <th>Change</th>
                      <th>Latest visible</th>
                      <th>Status</th>
                      <th>Trend</th>
                    </tr>
                  </thead>
                  <tbody>
                    {results.controls.map((row) => (
                      <ResultRow key={row.controlId} row={row} />
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

function ResultRow({ row }: { row: DataHealthControlResult }) {
  return (
    <tr>
      <td>
        {row.name}
        <div className="muted small">{row.sourceName}</div>
        {row.message && <div className="small">{row.message}</div>}
      </td>
      <td>{formatDate(row.controlDate)}</td>
      <td>{formatCount(row.controlCount)}</td>
      <td>{formatDate(row.comparisonDate)}</td>
      <td>{formatCount(row.previousCount)}</td>
      <td>{row.percentChange === null ? '—' : `${row.percentChange > 0 ? '+' : ''}${row.percentChange}%`}</td>
      <td>
        {formatDate(row.latestVisibleDate)}
        <div className="muted small">{row.latestVisibleCount === null ? '' : `${formatCount(row.latestVisibleCount)} rows`}</div>
      </td>
      <td>
        <span className={`status ${statusBadge[row.status]}`}>{statusLabels[row.status]}</span>
      </td>
      <td>
        <Sparkline points={row.trend} />
      </td>
    </tr>
  )
}

function ConfigureView() {
  const [controls, setControls] = useState<DataHealthControl[]>([])
  const [sources, setSources] = useState<ApprovedSource[]>([])
  const [selection, setSelection] = useState<number | 'new' | null>(null)
  const [audit, setAudit] = useState<{ id: number; entries: AuditEntry[] } | null>(null)
  const [revision, setRevision] = useState(0)
  const [errors, setErrors] = useState<string[]>([])
  const [busyId, setBusyId] = useState<number | null>(null)
  const notify = useToast()

  useEffect(() => {
    let ignore = false
    Promise.all([api.getDataHealthControls(), api.getDataHealthSources()])
      .then(([list, approved]) => {
        if (!ignore) {
          setControls(list)
          setSources(approved)
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
  const selected = controls.find((control) => control.id === selectedId) ?? null

  useEffect(() => {
    if (selectedId === null) {
      return
    }

    let ignore = false
    api
      .getDataHealthControlAudit(selectedId)
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

  const sorted = useMemo(() => [...controls].sort((a, b) => a.displayOrder - b.displayOrder), [controls])

  function afterChange(text: string) {
    notify(text)
    setRevision((current) => current + 1)
  }

  async function toggleEnabled(control: DataHealthControl) {
    setBusyId(control.id)
    try {
      await api.setDataHealthControlEnabled(control.id, !control.isEnabled)
      afterChange(control.isEnabled ? `"${control.name}" disabled.` : `"${control.name}" enabled.`)
    } catch (error) {
      setErrors(messagesFrom(error))
    } finally {
      setBusyId(null)
    }
  }

  async function retire(control: DataHealthControl) {
    if (!window.confirm(`Retire "${control.name}"? It stops being checked. Its history is kept.`)) {
      return
    }

    setBusyId(control.id)
    try {
      await api.retireDataHealthControl(control.id)
      afterChange(`"${control.name}" retired.`)
    } catch (error) {
      setErrors(messagesFrom(error))
    } finally {
      setBusyId(null)
    }
  }

  async function move(control: DataHealthControl, direction: -1 | 1) {
    const index = sorted.findIndex((item) => item.id === control.id)
    const neighbour = sorted[index + direction]
    if (!neighbour) {
      return
    }

    setBusyId(control.id)
    try {
      await api.updateDataHealthControl(control.id, toInput({ ...control, displayOrder: neighbour.displayOrder }), control.concurrencyToken)
      await api.updateDataHealthControl(neighbour.id, toInput({ ...neighbour, displayOrder: control.displayOrder }), neighbour.concurrencyToken)
      afterChange('Order updated.')
    } catch (error) {
      setErrors(messagesFrom(error))
    } finally {
      setBusyId(null)
    }
  }

  if (selection !== null) {
    const isNew = selection === 'new'
    return (
      <>
        <div className="toolbar">
          <button type="button" className="button secondary" onClick={() => setSelection(null)}>
            ← Back to controls
          </button>
        </div>

        {isNew && (
          <section className="card">
            <p className="eyebrow">Data Health control</p>
            <h2>New control</h2>
            <ControlEditor
              key="new"
              control={null}
              sources={sources}
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
              <p className="eyebrow">{selected.isRetired ? 'Retired control' : 'Data Health control'}</p>
              <h2>{selected.name}</h2>
              <ControlEditor
                key={selected.id}
                control={selected}
                sources={sources}
                onSaved={(saved) => afterChange(`"${saved.name}" saved.`)}
                onConflict={() => setRevision((current) => current + 1)}
                onCancel={() => setSelection(null)}
              />
            </section>

            <section className="card">
              <h3>Change history</h3>
              {audit === null || audit.id !== selectedId || audit.entries.length === 0 ? (
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
                      {audit.entries.map((entry) => (
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
    <section className="card">
      <div className="toolbar">
        <h2>Controls</h2>
        <button type="button" className="button" onClick={() => setSelection('new')} disabled={sources.length === 0}>
          New control
        </button>
      </div>

      <ErrorList messages={errors} />

      {sources.length === 0 && (
        <p className="notice error" role="status">
          No approved sources yet. An administrator adds them with the approved-source script before controls can be created.
        </p>
      )}

      {sorted.length === 0 ? (
        <div className="empty">
          <strong>No controls yet</strong>
          <span>Create a control to start checking a source.</span>
        </div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Order</th>
                <th>Control</th>
                <th>Source</th>
                <th>Offset</th>
                <th>State</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {sorted.map((control, index) => (
                <tr key={control.id} className={control.isRetired ? 'retired' : undefined}>
                  <td>
                    <div className="row-actions">
                      <button type="button" className="button secondary" aria-label="Move up" disabled={busyId === control.id || index === 0 || control.isRetired} onClick={() => void move(control, -1)}>
                        ↑
                      </button>
                      <button type="button" className="button secondary" aria-label="Move down" disabled={busyId === control.id || index === sorted.length - 1 || control.isRetired} onClick={() => void move(control, 1)}>
                        ↓
                      </button>
                    </div>
                  </td>
                  <td>
                    <button type="button" className="link-button" onClick={() => setSelection(control.id)}>
                      {control.name}
                    </button>
                  </td>
                  <td>
                    {control.sourceName}
                    <div className="muted small mono">{control.sourceKey}</div>
                  </td>
                  <td>
                    {control.offsetDays} {control.offsetType === 'Weekdays' ? 'weekdays' : 'days'} back
                    <div className="muted small">History {control.historyDays} days</div>
                  </td>
                  <td>
                    <span className={control.isRetired ? 'pill pill-retired' : control.isEnabled ? 'pill pill-on' : 'pill pill-off'}>
                      {control.isRetired ? 'Retired' : control.isEnabled ? 'Enabled' : 'Disabled'}
                    </span>
                  </td>
                  <td className="actions-cell">
                    <div className="row-actions">
                      <button type="button" className="button secondary" onClick={() => setSelection(control.id)}>
                        {control.isRetired ? 'View' : 'Edit'}
                      </button>
                      {!control.isRetired && (
                        <>
                          <button type="button" className="button secondary" disabled={busyId === control.id} onClick={() => void toggleEnabled(control)}>
                            {control.isEnabled ? 'Disable' : 'Enable'}
                          </button>
                          <button type="button" className="button danger" disabled={busyId === control.id} onClick={() => void retire(control)}>
                            Retire
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function toInput(control: DataHealthControl): DataHealthControlInput {
  return {
    name: control.name,
    displayOrder: control.displayOrder,
    isEnabled: control.isEnabled,
    approvedSourceId: control.approvedSourceId,
    offsetDays: control.offsetDays,
    offsetType: control.offsetType,
    historyDays: control.historyDays,
    missingCheck: control.missingCheck,
    decreaseCheck: control.decreaseCheck,
    decreaseTolerancePercent: control.decreaseTolerancePercent,
  }
}

interface ControlEditorProps {
  control: DataHealthControl | null
  sources: ApprovedSource[]
  onSaved: (saved: DataHealthControl) => void
  onConflict: () => void
  onCancel: () => void
}

function ControlEditor({ control, sources, onSaved, onConflict, onCancel }: ControlEditorProps) {
  const [draft, setDraft] = useState<DataHealthControlInput>(() =>
    control
      ? toInput(control)
      : {
          name: '',
          displayOrder: 0,
          isEnabled: true,
          approvedSourceId: sources[0]?.id ?? 0,
          offsetDays: 2,
          offsetType: 'Weekdays',
          historyDays: 10,
          missingCheck: true,
          decreaseCheck: true,
          decreaseTolerancePercent: 0,
        },
  )
  const [errors, setErrors] = useState<string[]>([])
  const [saving, setSaving] = useState(false)
  const readOnly = control?.isRetired ?? false

  function update<K extends keyof DataHealthControlInput>(key: K, value: DataHealthControlInput[K]) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  function validate(): string[] {
    const problems: string[] = []
    if (draft.name.trim().length === 0) problems.push('Name is required.')
    if (draft.name.trim().length > 200) problems.push('Name must be at most 200 characters.')
    if (draft.approvedSourceId <= 0) problems.push('Choose an approved data source.')
    if (draft.offsetDays < 0 || draft.offsetDays > 60) problems.push('Offset must be between 0 and 60.')
    if (draft.historyDays < 1 || draft.historyDays > 365) problems.push('History must be between 1 and 365 days.')
    if (draft.decreaseTolerancePercent < 0 || draft.decreaseTolerancePercent > 100) problems.push('Tolerance must be between 0 and 100.')
    if (draft.displayOrder < 0) problems.push('Display order cannot be negative.')
    return problems
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const problems = validate()
    if (problems.length > 0) {
      setErrors(problems)
      return
    }

    const payload = { ...draft, name: draft.name.trim() }
    setSaving(true)
    setErrors([])
    try {
      const saved = control
        ? await api.updateDataHealthControl(control.id, payload, control.concurrencyToken)
        : await api.createDataHealthControl(payload)
      onSaved(saved)
    } catch (error) {
      const status = (error as { status?: number }).status
      if (status === 409) {
        setErrors(['This control changed since you opened it. It has been reloaded. Review your changes and save again.'])
        onConflict()
      } else {
        setErrors(messagesFrom(error))
      }
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="definition-form" onSubmit={save}>
      <fieldset disabled={readOnly || saving}>
        {readOnly && <p className="muted">This control is retired. It cannot be changed.</p>}

        <div className="form-grid">
          <label className="field">
            Name
            <input value={draft.name} maxLength={200} onChange={(event) => update('name', event.target.value)} />
          </label>
          <label className="field">
            Data source
            <select value={draft.approvedSourceId} onChange={(event) => update('approvedSourceId', Number(event.target.value))}>
              {sources.map((source) => (
                <option key={source.id} value={source.id}>
                  {source.displayName}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            Display order
            <input type="number" min={0} value={draft.displayOrder} onChange={(event) => update('displayOrder', Number(event.target.value))} />
          </label>
          <label className="field">
            Offset from today
            <input type="number" min={0} max={60} value={draft.offsetDays} onChange={(event) => update('offsetDays', Number(event.target.value))} />
          </label>
          <label className="field">
            Offset type
            <select value={draft.offsetType} onChange={(event) => update('offsetType', event.target.value as DataHealthControlInput['offsetType'])}>
              <option value="Weekdays">Weekdays</option>
              <option value="CalendarDays">Calendar days</option>
            </select>
          </label>
          <label className="field">
            History (days)
            <input type="number" min={1} max={365} value={draft.historyDays} onChange={(event) => update('historyDays', Number(event.target.value))} />
          </label>
          <label className="field">
            Decrease tolerance (%)
            <input type="number" min={0} max={100} step="0.01" value={draft.decreaseTolerancePercent} onChange={(event) => update('decreaseTolerancePercent', Number(event.target.value))} />
          </label>
        </div>

        <div className="form-grid" style={{ marginTop: 14 }}>
          <label className="checkbox">
            <input type="checkbox" checked={draft.isEnabled} onChange={(event) => update('isEnabled', event.target.checked)} />
            Enabled
          </label>
          <label className="checkbox">
            <input type="checkbox" checked={draft.missingCheck} onChange={(event) => update('missingCheck', event.target.checked)} />
            Raise attention when missing
          </label>
          <label className="checkbox">
            <input type="checkbox" checked={draft.decreaseCheck} onChange={(event) => update('decreaseCheck', event.target.checked)} />
            Raise attention on decrease
          </label>
        </div>
      </fieldset>

      <ErrorList messages={errors} />

      {!readOnly && (
        <div className="actions">
          <button type="submit" className="button" disabled={saving}>
            {saving ? 'Saving…' : control ? 'Save control' : 'Create control'}
          </button>
          <button type="button" className="button secondary" onClick={onCancel} disabled={saving}>
            Cancel
          </button>
        </div>
      )}
    </form>
  )
}
