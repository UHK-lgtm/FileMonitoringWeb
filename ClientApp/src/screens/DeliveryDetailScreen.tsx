import { useEffect, useState, type FormEvent } from 'react'
import { api, messagesFrom, type DeliveryDetail } from '../api'
import { ErrorList } from '../components/ErrorList'
import { StatusBadge } from '../components/StatusBadge'
import { useToast } from '../components/toastContext'
import { formatDateTime, statusLabel } from '../format'

interface DeliveryDetailScreenProps {
  expectedDeliveryId: number
  onBack: () => void
}

const commentLimit = 2000

const warningTitles: Record<string, string> = {
  UnexpectedFileWarning: 'Unexpected file',
  SizeWarning: 'Size warning',
}

export function DeliveryDetailScreen({ expectedDeliveryId, onBack }: DeliveryDetailScreenProps) {
  const [detail, setDetail] = useState<DeliveryDetail | null>(null)
  const [loadErrors, setLoadErrors] = useState<string[]>([])
  const [commentText, setCommentText] = useState('')
  const [commentErrors, setCommentErrors] = useState<string[]>([])
  const [submitting, setSubmitting] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const notify = useToast()

  useEffect(() => {
    let ignore = false
    api
      .getDelivery(expectedDeliveryId)
      .then((response) => {
        if (!ignore) {
          setDetail(response)
          setLoadErrors([])
        }
      })
      .catch((error: unknown) => {
        if (!ignore) {
          setLoadErrors(messagesFrom(error))
        }
      })

    return () => {
      ignore = true
    }
  }, [expectedDeliveryId, reloadKey])

  async function submitComment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const text = commentText.trim()
    if (text.length === 0) {
      setCommentErrors(['Write a comment before saving.'])
      return
    }
    if (text.length > commentLimit) {
      setCommentErrors([`Comments must be at most ${commentLimit} characters.`])
      return
    }

    setSubmitting(true)
    setCommentErrors([])
    try {
      await api.addComment(expectedDeliveryId, text)
      setCommentText('')
      setReloadKey((key) => key + 1)
      notify('Comment added.')
    } catch (error) {
      setCommentErrors(messagesFrom(error))
    } finally {
      setSubmitting(false)
    }
  }

  const delivery = detail?.delivery ?? null
  const percent =
    delivery === null || delivery.expectedFileCount === 0
      ? 0
      : Math.min(100, Math.round((delivery.receivedFileCount / delivery.expectedFileCount) * 100))

  return (
    <>
      <div className="toolbar">
        <button type="button" className="button secondary" onClick={onBack}>
          ← Back to dashboard
        </button>
      </div>

      <ErrorList messages={loadErrors} />

      {detail === null && loadErrors.length === 0 && (
        <div className="card" aria-hidden="true">
          <div className="skeleton" />
          <div className="skeleton" />
          <div className="skeleton" />
        </div>
      )}

      {detail !== null && delivery !== null && (
        <>
          <section className="card">
            <div className="hero">
              <div>
                <p className="eyebrow">Expected delivery {delivery.expectedDeliveryId}</p>
                <h2>{delivery.definitionName}</h2>
                <StatusBadge status={delivery.status} />
                <div className="hero-meta">
                  <span>Expected {delivery.expectedDate}</span>
                  <span>Deadline {formatDateTime(delivery.deadlineUtc)}</span>
                  <span>Completed {formatDateTime(delivery.completedAtUtc)}</span>
                </div>
              </div>
              <div className="kpis" style={{ marginBottom: 0 }}>
                <div className="kpi kpi-neutral">
                  <span className="kpi-label">Received</span>
                  <span className="kpi-value">
                    {delivery.receivedFileCount} / {delivery.expectedFileCount}
                  </span>
                </div>
                <div className="kpi kpi-warn">
                  <span className="kpi-label">Unexpected</span>
                  <span className="kpi-value">{delivery.unexpectedFileCount}</span>
                </div>
              </div>
            </div>
            <div className="progress" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent}>
              <span style={{ width: `${percent}%` }} />
            </div>
          </section>

          <div className="two-col">
            <section className="card">
              <h3>Delivery facts</h3>
              <dl className="facts">
                <dt>Folder</dt>
                <dd className="mono">{delivery.relativePath || '(share root)'}</dd>
                <dt>File name pattern</dt>
                <dd className="mono">{delivery.fileNamePattern}</dd>
                <dt>Time zone</dt>
                <dd>{delivery.timeZoneId}</dd>
                <dt>Current status</dt>
                <dd>{statusLabel(delivery.status)}</dd>
              </dl>
            </section>

            <section className="card">
              <h3>Status timeline</h3>
              {detail.transitions.length === 0 ? (
                <p className="muted">No status changes recorded.</p>
              ) : (
                <ol className="timeline">
                  {detail.transitions.map((transition, index) => (
                    <li key={index}>
                      <strong>
                        {transition.fromStatus ? `${statusLabel(transition.fromStatus)} → ` : ''}
                        {statusLabel(transition.toStatus)}
                      </strong>
                      <div className="small">
                        {formatDateTime(transition.occurredAtUtc)} · {transition.receivedFileCount} /{' '}
                        {transition.expectedFileCount} received
                      </div>
                    </li>
                  ))}
                </ol>
              )}
            </section>
          </div>

          <section className="card">
            <h3>Files</h3>
            {detail.files.length === 0 ? (
              <div className="empty">
                <strong>No matching files yet</strong>
                <span>Files appear here once they are found in the folder.</span>
              </div>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>File</th>
                      <th>Size</th>
                      <th>Last modified</th>
                      <th>Observed</th>
                      <th>Kind</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.files.map((file, index) => (
                      <tr key={index}>
                        <td className="mono">{file.fileName}</td>
                        <td>{file.sizeBytes.toLocaleString()} bytes</td>
                        <td>{formatDateTime(file.fileLastModifiedUtc)}</td>
                        <td>{formatDateTime(file.observedAtUtc)}</td>
                        <td>
                          <span className={file.isUnexpected ? 'pill pill-off' : 'pill pill-on'}>
                            {file.isUnexpected ? 'Unexpected' : 'Expected'}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <div className="two-col">
            <section className="card">
              <h3>Warnings</h3>
              {detail.warnings.length === 0 ? (
                <p className="muted">No warnings. The delivery is clean.</p>
              ) : (
                detail.warnings.map((warning, index) => (
                  <div key={index} className="warning-card">
                    <strong>{warningTitles[warning.warningType] ?? warning.warningType}</strong>
                    {warning.detail && <span>{warning.detail}</span>}
                    <span className="small muted">Detected {formatDateTime(warning.detectedAtUtc)}</span>
                  </div>
                ))
              )}
            </section>

            <section className="card">
              <h3>Comments and reasons</h3>
              {detail.comments.length === 0 ? (
                <p className="muted">No comments yet.</p>
              ) : (
                <div>
                  {detail.comments.map((comment) => (
                    <div key={comment.commentId} className="comment">
                      <p>{comment.commentText}</p>
                      <small>
                        {comment.createdBy} · {formatDateTime(comment.createdAtUtc)}
                      </small>
                    </div>
                  ))}
                </div>
              )}

              <form className="comment-form" onSubmit={submitComment}>
                <label>
                  Add a comment or reason
                  <textarea
                    value={commentText}
                    maxLength={commentLimit}
                    rows={3}
                    onChange={(event) => setCommentText(event.target.value)}
                    disabled={submitting}
                  />
                </label>
                <ErrorList messages={commentErrors} />
                <div>
                  <button type="submit" className="button" disabled={submitting}>
                    {submitting ? 'Saving…' : 'Save comment'}
                  </button>
                </div>
              </form>
            </section>
          </div>
        </>
      )}
    </>
  )
}
