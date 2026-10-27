import { useEffect, useState } from 'react'
import { api, type LatestScanRunResponse } from '../api'
import { formatLuxembourgDateTime } from '../format'

/// One line on Today that says whether the scanner has run recently. Delivery statuses are only as current as the last scan.
/// Shows no failure stage or type: those are for operators and the logs.
export function ScanStatusLine() {
  const [result, setResult] = useState<LatestScanRunResponse | null>(null)
  const [unavailable, setUnavailable] = useState(false)

  useEffect(() => {
    let ignore = false
    api
      .getLatestScanRun()
      .then((response) => {
        if (!ignore) {
          setResult(response)
        }
      })
      .catch(() => {
        if (!ignore) {
          setUnavailable(true)
        }
      })

    return () => {
      ignore = true
    }
  }, [])

  if (unavailable) {
    return <p className="muted small" role="status">Scan status unavailable.</p>
  }

  if (result === null) {
    return null
  }

  return <p className="muted small" role="status">{describe(result)}</p>
}

function describe({ state, latest }: LatestScanRunResponse): string {
  const lastRun = formatLuxembourgDateTime(latest?.finishedUtc ?? latest?.startedUtc ?? null)
  const started = formatLuxembourgDateTime(latest?.startedUtc ?? null)

  switch (state) {
    case 'NoRunRecorded':
      return 'No scan recorded.'
    case 'Current':
      return `Last scan: ${lastRun}, current.`
    case 'Stale':
      return `Last scan: ${lastRun}, stale. Delivery statuses may be out of date.`
    case 'Running':
      return `Scan running since ${started}.`
    case 'DidNotFinish':
      return `Last scan started ${started} did not finish. Delivery statuses may be out of date.`
    case 'Failed':
      return `Last scan failed (${lastRun}). Delivery statuses may be out of date.`
    default:
      return 'Scan status unknown.'
  }
}
