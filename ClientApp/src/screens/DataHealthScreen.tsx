import { useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  api,
  messagesFrom,
  type ApprovedSource,
  type AuditEntry,
  type DataHealthControl,
  type DataHealthControlInput,
  type DataHealthControlResult,
  type DataHealthStatus,
} from '../api'
import { ErrorList } from '../components/ErrorList'
import { useToast } from '../components/toastContext'
import { formatDateTime, todayInLuxembourg } from '../format'

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

const dateColumnTypes = ['date', 'datetime', 'datetime2', 'smalldatetime', 'datetimeoffset']
const textColumnTypes = ['varchar', 'nvarchar', 'char', 'nchar']

// Must match DataHealthRules.AllowedStyles on the server.
const allowedStyles = new Set([23, 101, 102, 103, 104, 105, 106, 107, 110, 111, 112, 113, 114, 120, 121, 126])

// The last result of each control, kept in this browser, so the page can show it while the new checks run.
const lastKnownKey = 'fm-data-health-last'

interface KnownResult {
  result: DataHealthControlResult
  checkedAt: string
}

/// Reads "112, 23" into [112, 23]. Null when a part is not an allowed style or a style is repeated.
function parseStyleList(value: string): number[] | null {
  const styles: number[] = []
  for (const part of value.split(',').map((item) => item.trim())) {
    if (!/^\d+$/.test(part)) {
      return null
    }

    const style = Number(part)
    if (!allowedStyles.has(style) || styles.includes(style)) {
      return null
    }

    styles.push(style)
  }

  return styles.length > 0 ? styles : null
}

function readLastKnown(): Record<number, KnownResult> {
  try {
    const raw = window.localStorage.getItem(lastKnownKey)
    return raw ? (JSON.parse(raw) as Record<number, KnownResult>) : {}
  } catch {
    // Storage can be blocked or full. The page still works without it.
    return {}
  }
}

function saveLastKnown(controlId: number, known: KnownResult) {
  try {
    const all = readLastKnown()
    all[controlId] = known
    window.localStorage.setItem(lastKnownKey, JSON.stringify(all))
  } catch {
    // Storage can be blocked or full. The page still works without it.
  }
}

function formatDate(value: string | null): string {
  return value === null ? '—' : new Date(`${value}T00:00:00`).toLocaleDateString(undefined, { dateStyle: 'medium' })
}

function formatCount(value: number | null): string {
  return value === null ? '—' : value.toLocaleString()
}

function Skeleton({ width = 56 }: { width?: number }) {
  return <span className="skeleton" style={{ width }} aria-hidden="true" />
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

// One row of the results page. Each row fills in when its own check returns.
interface CheckRow {
  control: DataHealthControl
  // The result to show. While a check runs it can be the last known result, shown with its time.
  result: DataHealthControlResult | null
  checkedAt: string | null
  state: 'checking' | 'done' | 'failed'
}

function ResultsView() {
  const [rows, setRows] = useState<CheckRow[]>([])
  const [errors, setErrors] = useState<string[]>([])
  const [loadingList, setLoadingList] = useState(true)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let ignore = false
    const known = readLastKnown()

    api
      .getDataHealthControls()
      .then((controls) => {
        if (ignore) {
          return
        }

        const enabled = controls.filter((control) => control.isEnabled && !control.isRetired)
        setRows(
          enabled.map((control) => ({
            control,
            result: known[control.id]?.result ?? null,
            checkedAt: known[control.id]?.checkedAt ?? null,
            state: 'checking',
          })),
        )
        setErrors([])
        setLoadingList(false)

        // Each check is requested on its own, so a slow source delays only its own row.
        for (const control of enabled) {
          api
            .getDataHealthControlResult(control.id)
            .then((result) => {
              if (ignore) {
                return
              }

              const checkedAt = new Date().toISOString()
              saveLastKnown(control.id, { result, checkedAt })
              setRows((current) =>
                current.map((row) => (row.control.id === control.id ? { ...row, result, checkedAt, state: 'done' } : row)),
              )
            })
            .catch((error: unknown) => {
              if (ignore) {
                return
              }

              setRows((current) =>
                current.map((row) => (row.control.id === control.id ? { ...row, result: null, checkedAt: null, state: 'failed' } : row)),
              )
              setErrors(messagesFrom(error))
            })
        }
      })
      .catch((error: unknown) => {
        if (!ignore) {
          setErrors(messagesFrom(error))
          setLoadingList(false)
        }
      })

    return () => {
      ignore = true
    }
  }, [reloadKey])

  function refresh() {
    setLoadingList(true)
    setReloadKey((key) => key + 1)
  }

  const checking = rows.filter((row) => row.state === 'checking').length
  const finished = rows.length - checking
  const done = rows.filter((row) => row.state === 'done' && row.result !== null)
  const missingCount = done.filter((row) => row.result?.status === 'Missing').length
  const decreaseCount = done.filter((row) => row.result?.status === 'Decrease').length
  const errorCount = rows.filter((row) => row.state === 'failed' || (row.state === 'done' && row.result?.status === 'Error')).length
  const healthy = rows.every((row) => row.state === 'done' && row.result !== null && !row.result.attention)
  const latestCheck = rows
    .map((row) => (row.state === 'done' ? row.checkedAt : null))
    .filter((value): value is string => value !== null)
    .sort()
    .at(-1)

  return (
    <>
      <div className="toolbar">
        <div>
          <h2>Data Health</h2>
          {checking > 0 ? (
            <span className="muted small">Checking {finished} of {rows.length}…</span>
          ) : (
            latestCheck && <span className="muted small">Refreshed {formatDateTime(latestCheck)}</span>
          )}
        </div>
        <button type="button" className="button secondary" onClick={refresh} disabled={loadingList || checking > 0}>
          Refresh
        </button>
      </div>

      <ErrorList messages={errors} />

      {loadingList && rows.length === 0 && <p className="muted">Loading the checks…</p>}

      {(rows.length > 0 || !loadingList) && (
        <>
          <section className="summary-strip" aria-label="Data Health summary">
            <div className="summary-cell">
              <span className="summary-label">Control date</span>
              <span className="summary-value">{formatDate(todayInLuxembourg())}</span>
              <span className="summary-hint">Today in Europe/Luxembourg</span>
            </div>
            <div className="summary-cell">
              <span className="summary-label">Enabled sources</span>
              <span className="summary-value">{rows.length}</span>
              <span className="summary-hint">Checks that run</span>
            </div>
            <div className="summary-cell">
              <span className="summary-label">Missing</span>
              <span className="summary-value attention">{missingCount}</span>
              <span className="summary-hint">No data on the control date</span>
            </div>
            <div className="summary-cell">
              <span className="summary-label">Decrease</span>
              <span className="summary-value warn">{decreaseCount}</span>
              <span className="summary-hint">Lower than the comparison</span>
            </div>
            <div className="summary-cell">
              <span className="summary-label">Overall</span>
              <span className={checking === 0 && healthy ? 'summary-value ok' : checking > 0 ? 'summary-value' : 'summary-value attention'}>
                {checking > 0 ? 'Checking…' : healthy ? 'Healthy' : 'Attention'}
              </span>
              <span className="summary-hint">
                {checking > 0 ? `${finished} of ${rows.length} done` : errorCount > 0 ? `${errorCount} check(s) did not run` : 'No open attention items'}
              </span>
            </div>
          </section>

          <section className="card">
            {rows.length === 0 ? (
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
                    {rows.map((row) => (
                      <ResultRow key={row.control.id} row={row} />
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

function ResultRow({ row }: { row: CheckRow }) {
  const { control, result, checkedAt, state } = row
  const pending = state === 'checking' && result === null
  const showingLastKnown = state === 'checking' && result !== null

  return (
    <tr aria-busy={state === 'checking'}>
      <td>
        {control.name}
        <div className="muted small">{control.sourceName}</div>
        {showingLastKnown && (
          <div className="muted small">Last checked {formatDateTime(checkedAt)}. Checking now…</div>
        )}
        {state === 'failed' && <div className="small">This check could not run. Ask an administrator to look at the logs.</div>}
        {state === 'done' && result?.message && <div className="small">{result.message}</div>}
        {state === 'done' && result !== null && result.unreadableCount > 0 && (
          <div className="small unreadable-note">
            {result.unreadableCount.toLocaleString()} row(s) have a date that matches none of the configured styles. They are not counted.
          </div>
        )}
      </td>
      <td>{pending ? <Skeleton /> : formatDate(result?.controlDate ?? null)}</td>
      <td>{pending ? <Skeleton width={40} /> : formatCount(result?.controlCount ?? null)}</td>
      <td>{pending ? <Skeleton /> : formatDate(result?.comparisonDate ?? null)}</td>
      <td>{pending ? <Skeleton width={40} /> : formatCount(result?.previousCount ?? null)}</td>
      <td>
        {pending ? (
          <Skeleton width={44} />
        ) : result?.percentChange == null ? (
          '—'
        ) : (
          `${result.percentChange > 0 ? '+' : ''}${result.percentChange}%`
        )}
      </td>
      <td>
        {pending ? (
          <Skeleton />
        ) : (
          <>
            {formatDate(result?.latestVisibleDate ?? null)}
            <div className="muted small">{result?.latestVisibleCount == null ? '' : `${formatCount(result.latestVisibleCount)} rows`}</div>
          </>
        )}
      </td>
      <td>
        {pending ? (
          <Skeleton width={72} />
        ) : state === 'failed' ? (
          <span className={`status ${statusBadge.Error}`}>{statusLabels.Error}</span>
        ) : result !== null ? (
          <span className={`status ${statusBadge[result.status]}`}>{statusLabels[result.status]}</span>
        ) : null}
      </td>
      <td>{pending ? <Skeleton width={120} /> : <Sparkline points={result?.trend ?? []} />}</td>
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
                <AuditTable entries={audit.entries} />
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
                <th>Date format</th>
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
                    <span className="mono">{control.dateColumnType}</span>
                    {control.dateStyles && <div className="muted small mono">styles {control.dateStyles}</div>}
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

function AuditTable({ entries }: { entries: AuditEntry[] }) {
  return (
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
          {entries.map((entry) => (
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
    dateColumnType: control.dateColumnType,
    dateStyles: control.dateStyles,
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
          dateColumnType: 'date',
          dateStyles: null,
        },
  )
  // The style list is typed as text and checked on save. It is stored as the numbers, in order.
  const [styleText, setStyleText] = useState(() => control?.dateStyles ?? '')
  const [errors, setErrors] = useState<string[]>([])
  const [saving, setSaving] = useState(false)
  const readOnly = control?.isRetired ?? false
  const isText = textColumnTypes.includes(draft.dateColumnType)
  const sourceColumn = sources.find((source) => source.id === draft.approvedSourceId)?.dateColumn

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
    if (isText && parseStyleList(styleText) === null) {
      problems.push('Convert styles must be one or more allowed style numbers, separated by commas. Each can appear only once.')
    }
    return problems
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const problems = validate()
    if (problems.length > 0) {
      setErrors(problems)
      return
    }

    const payload = {
      ...draft,
      name: draft.name.trim(),
      dateStyles: isText ? parseStyleList(styleText)!.join(',') : null,
    }
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
          <label className="field">
            Date column type
            <select value={draft.dateColumnType} onChange={(event) => update('dateColumnType', event.target.value)}>
              <optgroup label="Date types">
                {dateColumnTypes.map((type) => (
                  <option key={type} value={type}>
                    {type}
                  </option>
                ))}
              </optgroup>
              <optgroup label="Text types">
                {textColumnTypes.map((type) => (
                  <option key={type} value={type}>
                    {type}
                  </option>
                ))}
              </optgroup>
            </select>
          </label>
          {isText && (
            <label className="field">
              Convert styles, in order
              <input value={styleText} placeholder="112" onChange={(event) => setStyleText(event.target.value)} />
            </label>
          )}
        </div>

        <p className="muted small">
          {sourceColumn && (
            <>
              The date column in the source is <span className="mono">{sourceColumn}</span>.{' '}
            </>
          )}
          {isText
            ? 'Text dates are read with these SQL Server style numbers, tried left to right. 112 is yyyyMMdd, 23 is yyyy-MM-dd and 103 is dd/MM/yyyy. Rows that match none of them are reported as unreadable on the results page.'
            : 'A real date column is read directly. Choose a text type if the column stores dates as text.'}
        </p>

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
