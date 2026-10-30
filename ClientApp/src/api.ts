// Types and calls for the API described in docs/api-contract.md. Keep these in step with the contract.

export type DeliveryStatus = 'Pending' | 'OnTime' | 'Missing' | 'Incomplete' | 'Late'
export type FrequencyName = 'Daily' | 'Weekly' | 'EveryNDays' | 'Monthly' | 'Quarterly'
export type FilenameDateMode = 'NoDate' | 'SameDay' | 'PreviousDay' | 'MonthYear'
export type MatchType = 'Wildcard' | 'Regex'

export interface DeliverySummary {
  expectedDeliveryId: number
  monitoringDefinitionId: number
  definitionName: string
  expectedDate: string
  status: DeliveryStatus
  expectedFileCount: number
  receivedFileCount: number
  unexpectedFileCount: number
  deadlineUtc: string
  completedAtUtc: string | null
  warningCount: number
}

export interface DeliveriesResponse {
  date: string
  items: DeliverySummary[]
}

export interface DeliveryFacts {
  expectedDeliveryId: number
  monitoringDefinitionId: number
  definitionName: string
  expectedDate: string
  relativePath: string
  fileNamePattern: string
  timeZoneId: string
  status: DeliveryStatus
  expectedFileCount: number
  receivedFileCount: number
  unexpectedFileCount: number
  deadlineUtc: string
  completedAtUtc: string | null
}

export interface StatusTransition {
  fromStatus: DeliveryStatus | null
  toStatus: DeliveryStatus
  occurredAtUtc: string
  recordedAtUtc: string
  expectedFileCount: number
  receivedFileCount: number
  deadlineUtc: string
}

export interface ObservedFile {
  fileName: string
  relativePath: string
  sizeBytes: number
  fileCreatedUtc: string | null
  fileLastModifiedUtc: string | null
  observedAtUtc: string
  isUnexpected: boolean
}

export interface DeliveryWarning {
  warningType: string
  fileName: string
  detail: string | null
  detectedAtUtc: string
  resolvedAtUtc: string | null
}

export interface DeliveryComment {
  commentId: number
  commentText: string
  createdBy: string
  createdAtUtc: string
}

export interface DeliveryDetail {
  delivery: DeliveryFacts
  transitions: StatusTransition[]
  files: ObservedFile[]
  warnings: DeliveryWarning[]
  comments: DeliveryComment[]
}

export interface MonitoringDefinition {
  id: number
  name: string
  provider: string
  relativePath: string
  fileNamePattern: string
  matchType: MatchType
  expectedFileCount: number
  frequency: FrequencyName
  daysOfWeek: number[] | null
  isoWeekday: number | null
  intervalDays: number | null
  anchorDate: string | null
  dayOfMonth: number | null
  monthOfQuarter: number | null
  expectedDeliveryTime: string
  bufferMinutes: number
  timeZoneId: string
  filenameDateMode: FilenameDateMode
  filenameDateFormat: string | null
  sizeCheckEnabled: boolean
  referenceSizeBytes: number | null
  sizeTolerancePercent: number | null
  isEnabled: boolean
  isRetired: boolean
  concurrencyToken: string
}

export type MonitoringDefinitionInput = Omit<MonitoringDefinition, 'id' | 'isRetired' | 'concurrencyToken'>

export interface AuditEntry {
  auditId: number
  changedAtUtc: string
  changedBy: string
  fieldName: string
  oldValue: string | null
  newValue: string | null
}

export type ScanDisplayState =
  | 'NoRunRecorded'
  | 'Current'
  | 'Stale'
  | 'Running'
  | 'DidNotFinish'
  | 'Failed'
  | 'Unknown'

export interface LatestScanRun {
  scanRunId: number
  startedUtc: string
  finishedUtc: string | null
  status: 'Running' | 'Succeeded' | 'Failed'
  scheduledToday: number | null
  openDeliveries: number | null
  statusChanges: number | null
}

export interface LatestScanRunResponse {
  state: ScanDisplayState
  latest: LatestScanRun | null
}

export const genericMessage = 'Something went wrong. Try again.'
export const sessionEndedMessage = 'Your session has ended. Reload the page to sign in again.'

export class ApiError extends Error {
  readonly status: number
  readonly messages: string[]

  constructor(status: number, messages: string[]) {
    super(messages[0] ?? genericMessage)
    this.status = status
    this.messages = messages
  }
}

/// The messages to show for any failure. Only the server's own messages are shown, never raw error text.
export function messagesFrom(error: unknown): string[] {
  return error instanceof ApiError ? error.messages : [genericMessage]
}

function errorMessages(status: number, data: unknown): string[] {
  if (status === 401) {
    return [sessionEndedMessage]
  }

  if (typeof data === 'object' && data !== null) {
    const record = data as Record<string, unknown>
    if (Array.isArray(record.errors)) {
      return record.errors.filter((item): item is string => typeof item === 'string')
    }
    if (typeof record.message === 'string') {
      return [record.message]
    }
  }

  return [genericMessage]
}

async function request<T>(method: 'GET' | 'POST' | 'PUT', path: string, body?: unknown): Promise<T> {
  let response: Response
  try {
    response = await fetch(`/api${path}`, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, [genericMessage])
  }

  // App Service sends an unauthenticated request to the sign-in page, which is not JSON.
  const isJson = response.headers.get('Content-Type')?.includes('application/json') ?? false
  if (!isJson) {
    throw response.ok || response.status === 401
      ? new ApiError(401, [sessionEndedMessage])
      : new ApiError(response.status, [genericMessage])
  }

  const data: unknown = await response.json().catch(() => null)
  if (!response.ok) {
    throw new ApiError(response.status, errorMessages(response.status, data))
  }

  return data as T
}

export const api = {
  getDeliveries: (date?: string) =>
    request<DeliveriesResponse>('GET', date ? `/deliveries?date=${encodeURIComponent(date)}` : '/deliveries'),

  getDelivery: (id: number) => request<DeliveryDetail>('GET', `/deliveries/${id}`),

  addComment: (id: number, text: string) => request<DeliveryComment>('POST', `/deliveries/${id}/comments`, { text }),

  getDefinitions: () => request<MonitoringDefinition[]>('GET', '/monitoring-definitions'),

  createDefinition: (input: MonitoringDefinitionInput) =>
    request<MonitoringDefinition>('POST', '/monitoring-definitions', input),

  updateDefinition: (id: number, input: MonitoringDefinitionInput, concurrencyToken: string) =>
    request<MonitoringDefinition>('PUT', `/monitoring-definitions/${id}`, { ...input, concurrencyToken }),

  setEnabled: (id: number, isEnabled: boolean) =>
    request<MonitoringDefinition>('POST', `/monitoring-definitions/${id}/enabled`, { isEnabled }),

  retireDefinition: (id: number) => request<MonitoringDefinition>('POST', `/monitoring-definitions/${id}/retire`),

  getAudit: (id: number) => request<AuditEntry[]>('GET', `/monitoring-definitions/${id}/audit`),

  getLatestScanRun: () => request<LatestScanRunResponse>('GET', '/scan-runs/latest'),

  getDataHealth: () => request<DataHealthResults>('GET', '/data-health'),
  getDataHealthSources: () => request<ApprovedSource[]>('GET', '/data-health/sources'),
  getDataHealthControls: () => request<DataHealthControl[]>('GET', '/data-health/controls'),
  getDataHealthControl: (id: number) => request<DataHealthControl>('GET', `/data-health/controls/${id}`),
  createDataHealthControl: (input: DataHealthControlInput) =>
    request<DataHealthControl>('POST', '/data-health/controls', input),
  updateDataHealthControl: (id: number, input: DataHealthControlInput, concurrencyToken: string) =>
    request<DataHealthControl>('PUT', `/data-health/controls/${id}`, { ...input, concurrencyToken }),
  setDataHealthControlEnabled: (id: number, isEnabled: boolean) =>
    request<DataHealthControl>('POST', `/data-health/controls/${id}/enabled`, { isEnabled }),
  retireDataHealthControl: (id: number) => request<DataHealthControl>('POST', `/data-health/controls/${id}/retire`),
  getDataHealthControlAudit: (id: number) => request<AuditEntry[]>('GET', `/data-health/controls/${id}/audit`),
  updateDataHealthSourceFormat: (id: number, input: DataHealthSourceFormatInput) =>
    request<ApprovedSource>('PUT', `/data-health/sources/${id}/format`, input),
  getDataHealthSourceAudit: (id: number) => request<AuditEntry[]>('GET', `/data-health/sources/${id}/audit`),
}

export type DataHealthStatus = 'Missing' | 'Decrease' | 'Increase' | 'Stable' | 'NoHistory' | 'Error'
export type DataHealthOffsetType = 'CalendarDays' | 'Weekdays'

export interface ApprovedSource {
  id: number
  sourceKey: string
  displayName: string
  schemaName: string
  objectName: string
  dateColumn: string
  dateColumnType: string
  // Comma-separated convert styles in the order they are tried, for text columns only. Null for date columns.
  dateStyles: string | null
  concurrencyToken: string
}

export interface DataHealthSourceFormatInput {
  dateColumnType: string
  dateStyles: string | null
  concurrencyToken: string
}

export interface DataHealthControl {
  id: number
  name: string
  displayOrder: number
  isEnabled: boolean
  approvedSourceId: number
  sourceKey: string
  sourceName: string
  offsetDays: number
  offsetType: DataHealthOffsetType
  historyDays: number
  missingCheck: boolean
  decreaseCheck: boolean
  decreaseTolerancePercent: number
  isRetired: boolean
  concurrencyToken: string
}

export type DataHealthControlInput = Omit<DataHealthControl, 'id' | 'sourceKey' | 'sourceName' | 'isRetired' | 'concurrencyToken'>

export interface TrendPoint {
  date: string
  count: number
}

export interface DataHealthControlResult {
  controlId: number
  name: string
  sourceName: string
  controlDate: string
  controlCount: number
  comparisonDate: string | null
  previousCount: number | null
  percentChange: number | null
  latestVisibleDate: string | null
  latestVisibleCount: number | null
  status: DataHealthStatus
  attention: boolean
  message: string | null
  trend: TrendPoint[]
  // Rows in the source whose text date matches none of the configured styles. Counted over the whole table.
  unreadableCount: number
}

export interface DataHealthResults {
  today: string
  refreshedAtUtc: string
  enabledSources: number
  missingCount: number
  decreaseCount: number
  errorCount: number
  healthy: boolean
  controls: DataHealthControlResult[]
}

export const auditExportUrl = '/api/export/audit.xlsx'
