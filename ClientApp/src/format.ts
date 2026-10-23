import type { DeliveryStatus } from './api'

const statusLabels: Record<DeliveryStatus, string> = {
  Pending: 'Pending',
  OnTime: 'On Time',
  Missing: 'Missing',
  Incomplete: 'Incomplete',
  Late: 'Late',
}

export function statusLabel(status: DeliveryStatus): string {
  return statusLabels[status]
}

/// Shown in the viewer's own time zone, from a UTC value sent by the API.
export function formatDateTime(value: string | null): string {
  if (value === null) {
    return '—'
  }

  return new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

/// Today's date in Europe/Luxembourg as yyyy-MM-dd, the business date the API uses by default.
export function todayInLuxembourg(): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Europe/Luxembourg',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date())
}
