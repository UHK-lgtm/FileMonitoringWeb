import { useEffect, useState } from 'react'
import { api, messagesFrom, type DeliverySummary } from '../api'
import { ErrorList } from '../components/ErrorList'
import { formatDateTime, statusLabel, todayInLuxembourg } from '../format'

interface TodayScreenProps {
  onOpen: (expectedDeliveryId: number) => void
}

export function TodayScreen({ onOpen }: TodayScreenProps) {
  const [date, setDate] = useState(todayInLuxembourg)
  const [items, setItems] = useState<DeliverySummary[]>([])
  const [loading, setLoading] = useState(true)
  const [errors, setErrors] = useState<string[]>([])

  useEffect(() => {
    let ignore = false
    api
      .getDeliveries(date)
      .then((response) => {
        if (!ignore) {
          setItems(response.items)
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
  }, [date])

  function selectDate(value: string) {
    if (value) {
      setLoading(true)
      setDate(value)
    }
  }

  return (
    <section className="card">
      <div className="toolbar">
        <h2>Deliveries</h2>
        <label className="inline-field">
          Date
          <input type="date" value={date} onChange={(event) => selectDate(event.target.value)} />
        </label>
      </div>

      <ErrorList messages={errors} />

      {loading && <p className="muted">Loading…</p>}

      {!loading && errors.length === 0 && items.length === 0 && (
        <p className="muted">No expected deliveries for this date.</p>
      )}

      {!loading && items.length > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Definition</th>
                <th>Expected</th>
                <th>Status</th>
                <th>Received</th>
                <th>Unexpected</th>
                <th>Deadline</th>
                <th>Completed</th>
                <th>Warnings</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.expectedDeliveryId}>
                  <td>
                    <button type="button" className="link-button" onClick={() => onOpen(item.expectedDeliveryId)}>
                      {item.definitionName}
                    </button>
                  </td>
                  <td>{item.expectedDate}</td>
                  <td>
                    <span className={`status status-${item.status}`}>{statusLabel(item.status)}</span>
                  </td>
                  <td>
                    {item.receivedFileCount} / {item.expectedFileCount}
                  </td>
                  <td>{item.unexpectedFileCount}</td>
                  <td>{formatDateTime(item.deadlineUtc)}</td>
                  <td>{formatDateTime(item.completedAtUtc)}</td>
                  <td>{item.warningCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
