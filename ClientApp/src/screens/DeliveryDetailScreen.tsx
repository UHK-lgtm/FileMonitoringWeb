import { useEffect, useState, type FormEvent } from 'react'
import { api, messagesFrom, type DeliveryDetail } from '../api'
import { ErrorList } from '../components/ErrorList'
import { formatDateTime, statusLabel } from '../format'

interface DeliveryDetailScreenProps {
  expectedDeliveryId: number
  onBack: () => void
}

const commentLimit = 2000

export function DeliveryDetailScreen({ expectedDeliveryId, onBack }: DeliveryDetailScreenProps) {
  const [detail, setDetail] = useState<DeliveryDetail | null>(null)
  const [loadErrors, setLoadErrors] = useState<string[]>([])
  const [commentText, setCommentText] = useState('')
  const [commentErrors, setCommentErrors] = useState<string[]>([])
  const [submitting, setSubmitting] = useState(false)

  const [reloadKey, setReloadKey] = useState(0)

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
    } catch (error) {
      setCommentErrors(messagesFrom(error))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="detail">
      <div className="toolbar">
        <button type="button" className="button secondary" onClick={onBack}>
          ← Back to deliveries
        </button>
      </div>

      <ErrorList messages={loadErrors} />

      {detail === null && loadErrors.length === 0 && <p className="muted">Loading…</p>}

      {detail !== null && (
        <>
          <div className="card">
            <h2>{detail.delivery.definitionName}</h2>
            <span className={`status status-${detail.delivery.status}`}>{statusLabel(detail.delivery.status)}</span>
            <dl className="facts">
              <dt>Expected date</dt>
              <dd>{detail.delivery.expectedDate}</dd>
              <dt>Received</dt>
              <dd>
                {detail.delivery.receivedFileCount} / {detail.delivery.expectedFileCount}
                {detail.delivery.unexpectedFileCount > 0 && ` (${detail.delivery.unexpectedFileCount} unexpected)`}
              </dd>
              <dt>Deadline</dt>
              <dd>{formatDateTime(detail.delivery.deadlineUtc)}</dd>
              <dt>Completed</dt>
              <dd>{formatDateTime(detail.delivery.completedAtUtc)}</dd>
              <dt>Folder</dt>
              <dd className="mono">{detail.delivery.relativePath || '(share root)'}</dd>
              <dt>File name pattern</dt>
              <dd className="mono">{detail.delivery.fileNamePattern}</dd>
              <dt>Time zone</dt>
              <dd>{detail.delivery.timeZoneId}</dd>
            </dl>
          </div>

          <div className="card">
            <h3>Status history</h3>
            {detail.transitions.length === 0 ? (
              <p className="muted">No status changes recorded.</p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>From</th>
                      <th>To</th>
                      <th>Occurred</th>
                      <th>Received</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.transitions.map((transition, index) => (
                      <tr key={index}>
                        <td>{transition.fromStatus ? statusLabel(transition.fromStatus) : '—'}</td>
                        <td>{statusLabel(transition.toStatus)}</td>
                        <td>{formatDateTime(transition.occurredAtUtc)}</td>
                        <td>
                          {transition.receivedFileCount} / {transition.expectedFileCount}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <div className="card">
            <h3>Files</h3>
            {detail.files.length === 0 ? (
              <p className="muted">No matching files observed.</p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>File</th>
                      <th>Size (bytes)</th>
                      <th>Last modified</th>
                      <th>Observed</th>
                      <th>Kind</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.files.map((file, index) => (
                      <tr key={index}>
                        <td className="mono">{file.fileName}</td>
                        <td>{file.sizeBytes.toLocaleString()}</td>
                        <td>{formatDateTime(file.fileLastModifiedUtc)}</td>
                        <td>{formatDateTime(file.observedAtUtc)}</td>
                        <td>{file.isUnexpected ? 'Unexpected' : 'Expected'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <div className="card">
            <h3>Warnings</h3>
            {detail.warnings.length === 0 ? (
              <p className="muted">No warnings.</p>
            ) : (
              <ul className="plain-list">
                {detail.warnings.map((warning, index) => (
                  <li key={index}>
                    <strong>{warning.warningType === 'UnexpectedFileWarning' ? 'Unexpected file' : warning.warningType}</strong>
                    {warning.detail ? `: ${warning.detail}` : ''} <span className="muted">({formatDateTime(warning.detectedAtUtc)})</span>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="card">
            <h3>Comments</h3>
            {detail.comments.length === 0 ? (
              <p className="muted">No comments yet.</p>
            ) : (
              <ul className="plain-list">
                {detail.comments.map((comment) => (
                  <li key={comment.commentId}>
                    <p>{comment.commentText}</p>
                    <p className="muted small">
                      {comment.createdBy} · {formatDateTime(comment.createdAtUtc)}
                    </p>
                  </li>
                ))}
              </ul>
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
              <button type="submit" className="button" disabled={submitting}>
                {submitting ? 'Saving…' : 'Save comment'}
              </button>
            </form>
          </div>
        </>
      )}
    </section>
  )
}
