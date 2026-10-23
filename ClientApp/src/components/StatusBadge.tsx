import type { DeliveryStatus } from '../api'
import { statusLabel } from '../format'

export function StatusBadge({ status }: { status: DeliveryStatus }) {
  return <span className={`status status-${status}`}>{statusLabel(status)}</span>
}
