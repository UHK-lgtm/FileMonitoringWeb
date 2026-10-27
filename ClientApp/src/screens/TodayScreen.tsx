import { useEffect, useMemo, useState } from 'react'
import { api, messagesFrom, type DeliveryStatus, type DeliverySummary } from '../api'
import { ErrorList } from '../components/ErrorList'
import { StatCard } from '../components/StatCard'
import { ScanStatusLine } from '../components/ScanStatusLine'
import { StatusBadge } from '../components/StatusBadge'
import { formatDateTime, statusLabel, todayInLuxembourg } from '../format'

interface TodayScreenProps {
  onOpen: (expectedDeliveryId: number) => void
}

type Filter = 'All' | DeliveryStatus

const statusOrder: DeliveryStatus[] = ['OnTime', 'Pending', 'Incomplete', 'Missing', 'Late']
const statusColour: Record<DeliveryStatus, string> = {
  OnTime: 'var(--c-ok)',
  Pending: 'var(--c-pending)',
  Incomplete: 'var(--c-incomplete)',
  Missing: 'var(--c-missing)',
  Late: 'var(--c-late)',
}

export function TodayScreen({ onOpen }: TodayScreenProps) {
  const [date, setDate] = useState(todayInLuxembourg)
  const [items, setItems] = useState<DeliverySummary[]>([])
  const [loading, setLoading] = useState(true)
  const [errors, setErrors] = useState<string[]>([])
  const [reloadKey, setReloadKey] = useState(0)
  const [nowMs, setNowMs] = useState(() => Date.now())
  const [filter, setFilter] = useState<Filter>('All')
  const [search, setSearch] = useState('')

  useEffect(() => {
    let ignore = false
    api
      .getDeliveries(date)
      .then((response) => {
        if (!ignore) {
          setItems(response.items)
          setNowMs(Date.now())
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
  }, [date, reloadKey])

  const counts = useMemo(() => {
    const byStatus: Record<DeliveryStatus, number> = { Pending: 0, OnTime: 0, Missing: 0, Incomplete: 0, Late: 0 }
    for (const item of items) {
      byStatus[item.status] += 1
    }
    return byStatus
  }, [items])

  const summary = useMemo(() => {
    const total = items.length
    const pending = counts.Pending
    const decided = total - pending
    const onTimeRate = decided === 0 ? null : Math.round((counts.OnTime / decided) * 100)
    const attention = counts.Missing + counts.Incomplete + counts.Late
    const openWarnings = items.reduce((sum, item) => sum + item.warningCount, 0)

    const nextDeadline =
      items
        .filter((item) => item.status === 'Pending' && Date.parse(item.deadlineUtc) > nowMs)
        .map((item) => item.deadlineUtc)
        .sort()[0] ?? null

    return { total, pending, onTimeRate, attention, openWarnings, nextDeadline }
  }, [items, counts, nowMs])

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase()
    return items.filter(
      (item) =>
        (filter === 'All' || item.status === filter) &&
        (term.length === 0 || item.definitionName.toLowerCase().includes(term)),
    )
  }, [items, filter, search])

  function selectDate(value: string) {
    if (value) {
      setLoading(true)
      setDate(value)
    }
  }

  function refresh() {
    setLoading(true)
    setReloadKey((key) => key + 1)
  }

  const showingToday = date === todayInLuxembourg()

  return (
    <>
      <ScanStatusLine />
      <div className="toolbar">
        <div>
          <h2>{showingToday ? 'Today' : 'Deliveries'}</h2>
          <span className="muted small">{date}</span>
        </div>
        <div className="row-actions">
          <label className="inline-field">
            Date
            <input type="date" value={date} onChange={(event) => selectDate(event.target.value)} />
          </label>
          <button type="button" className="button secondary" onClick={refresh} disabled={loading}>
            Refresh
          </button>
        </div>
      </div>

      <ErrorList messages={errors} />

      {loading && (
        <div className="kpis" aria-hidden="true">
          {[0, 1, 2, 3, 4, 5].map((key) => (
            <div key={key} className="kpi">
              <div className="skeleton" />
              <div className="skeleton" />
            </div>
          ))}
        </div>
      )}

      {!loading && items.length > 0 && (
        <>
          <div className="kpis">
            <StatCard label="Expected today" value={summary.total} tone="neutral" hint="Scheduled deliveries" />
            <StatCard
              label="On time"
              value={counts.OnTime}
              tone="ok"
              hint={summary.onTimeRate === null ? 'No decided deliveries yet' : `${summary.onTimeRate}% of decided`}
            />
            <StatCard label="Pending" value={counts.Pending} tone="pending" hint="Before their deadline" />
            <StatCard
              label="Needs attention"
              value={summary.attention}
              tone="attention"
              hint={`${counts.Missing} missing · ${counts.Incomplete} incomplete · ${counts.Late} late`}
            />
            <StatCard label="Open warnings" value={summary.openWarnings} tone="warn" hint="Unexpected files" />
            <StatCard
              label="Next deadline"
              value={summary.nextDeadline === null ? '—' : formatDateTime(summary.nextDeadline)}
              tone="neutral"
              hint="For a pending delivery"
            />
          </div>

          <section className="card">
            <h3>Status mix</h3>
            <div className="distribution" role="img" aria-label="Deliveries by status">
              {statusOrder
                .filter((status) => counts[status] > 0)
                .map((status) => (
                  <span
                    key={status}
                    title={`${statusLabel(status)}: ${counts[status]}`}
                    style={{ width: `${(counts[status] / summary.total) * 100}%`, background: statusColour[status] }}
                  />
                ))}
            </div>
            <div className="legend">
              {statusOrder.map((status) => (
                <span key={status}>
                  <span className="legend-dot" style={{ background: statusColour[status] }} />
                  {statusLabel(status)} <strong>{counts[status]}</strong>
                </span>
              ))}
            </div>
          </section>

          <div className="filters">
            <button type="button" className={filter === 'All' ? 'chip active' : 'chip'} onClick={() => setFilter('All')}>
              All <span>{items.length}</span>
            </button>
            {statusOrder.map((status) => (
              <button
                key={status}
                type="button"
                className={filter === status ? 'chip active' : 'chip'}
                onClick={() => setFilter(status)}
              >
                {statusLabel(status)} <span>{counts[status]}</span>
              </button>
            ))}
            <input
              className="search"
              type="search"
              placeholder="Search definitions"
              aria-label="Search definitions"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>

          <section className="card">
            {visible.length === 0 ? (
              <div className="empty">
                <strong>No deliveries match</strong>
                <span>Try another status or clear the search.</span>
              </div>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Definition</th>
                      <th>Status</th>
                      <th>Received</th>
                      <th>Deadline</th>
                      <th>Completed</th>
                      <th>Unexpected</th>
                      <th>Warnings</th>
                    </tr>
                  </thead>
                  <tbody>
                    {visible.map((item) => {
                      const percent =
                        item.expectedFileCount === 0
                          ? 0
                          : Math.min(100, Math.round((item.receivedFileCount / item.expectedFileCount) * 100))
                      return (
                        <tr key={item.expectedDeliveryId} className="clickable" onClick={() => onOpen(item.expectedDeliveryId)}>
                          <td>
                            <button
                              type="button"
                              className="link-button"
                              onClick={(event) => {
                                event.stopPropagation()
                                onOpen(item.expectedDeliveryId)
                              }}
                            >
                              {item.definitionName}
                            </button>
                            <div className="muted small">{item.expectedDate}</div>
                          </td>
                          <td>
                            <StatusBadge status={item.status} />
                          </td>
                          <td>
                            <div className="progress-cell">
                              <span>
                                {item.receivedFileCount} / {item.expectedFileCount}
                              </span>
                              <div className="progress" aria-hidden="true">
                                <span style={{ width: `${percent}%`, background: statusColour[item.status] }} />
                              </div>
                            </div>
                          </td>
                          <td>{formatDateTime(item.deadlineUtc)}</td>
                          <td>{formatDateTime(item.completedAtUtc)}</td>
                          <td>{item.unexpectedFileCount}</td>
                          <td>
                            {item.warningCount > 0 ? (
                              <span className="pill pill-off">{item.warningCount} open</span>
                            ) : (
                              <span className="muted">None</span>
                            )}
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
      )}

      {!loading && errors.length === 0 && items.length === 0 && (
        <section className="card">
          <div className="empty">
            <strong>No expected deliveries for this date</strong>
            <span>Deliveries appear here once the scheduled run has planned them.</span>
          </div>
        </section>
      )}
    </>
  )
}
