import type { MonitoringDefinitionInput } from './api'

// Mirrors the field rules in docs/api-contract.md. The server checks the same rules and is the final authority.

const timePattern = /^([01]\d|2[0-3]):[0-5]\d$/

const weekdayShortNames = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

function isInRange(value: number | null, min: number, max: number): value is number {
  return value !== null && Number.isInteger(value) && value >= min && value <= max
}

function isValidRegex(pattern: string): boolean {
  try {
    return new RegExp(pattern, 'i') instanceof RegExp
  } catch {
    return false
  }
}

/// Short text for the list: "Every day" when no days are chosen, otherwise "Mon Tue Wed".
export function daysLabel(days: number[] | null): string {
  if (days === null || days.length === 0) {
    return 'Every day'
  }

  return [...days]
    .sort((a, b) => a - b)
    .map((day) => weekdayShortNames[day - 1] ?? '?')
    .join(' ')
}

export function validateDefinition(definition: MonitoringDefinitionInput): string[] {
  const errors: string[] = []

  const name = definition.name.trim()
  if (name.length === 0) {
    errors.push('Name is required.')
  } else if (name.length > 200) {
    errors.push('Name must be at most 200 characters.')
  }

  if (definition.provider.trim().length > 100) {
    errors.push('Provider must be at most 100 characters.')
  }

  const path = definition.relativePath.trim()
  const badSegment = path.split('/').some((segment) => segment === '' || segment === '.' || segment === '..')
  if (path.length > 0 && (badSegment || path.includes('\\') || path.includes(':'))) {
    errors.push('Relative path must use "/" only, with no empty, "." or ".." segments, and no ":" or "\\".')
  }

  const pattern = definition.fileNamePattern.trim()
  if (pattern.length === 0) {
    errors.push('File name pattern is required.')
  } else if (pattern.length > 260) {
    errors.push('File name pattern must be at most 260 characters.')
  } else if (definition.matchType === 'Regex') {
    if (pattern.includes('/')) {
      errors.push('A regular expression must not contain "/".')
    } else if (!isValidRegex(pattern)) {
      errors.push('File name pattern is not a valid regular expression.')
    }
  } else if (pattern.includes('/') || pattern.includes('\\')) {
    errors.push('File name pattern must not contain "/" or "\\".')
  }

  if (definition.matchType !== 'Wildcard' && definition.matchType !== 'Regex') {
    errors.push('Match type must be Wildcard or Regex.')
  }

  if (definition.daysOfWeek !== null) {
    const days = definition.daysOfWeek
    const allValid = days.every((day) => isInRange(day, 1, 7))
    if (!allValid || new Set(days).size !== days.length) {
      errors.push('Days must be weekdays 1 to 7, with no duplicates.')
    }
  }

  const hasDateToken = pattern.includes('{date}')
  if (definition.filenameDateMode === 'NoDate' && hasDateToken) {
    errors.push('Remove {date} from the pattern, or choose a filename date mode.')
  }
  if (definition.filenameDateMode !== 'NoDate') {
    if (!hasDateToken) {
      errors.push('The pattern needs a {date} token for this filename date mode.')
    }
    if ((definition.filenameDateFormat ?? '').trim().length === 0) {
      errors.push('A date format is required for this filename date mode.')
    }
  }

  if (!Number.isInteger(definition.expectedFileCount) || definition.expectedFileCount < 1) {
    errors.push('Expected file count must be at least 1.')
  }

  if (!timePattern.test(definition.expectedDeliveryTime)) {
    errors.push('Expected delivery time must be HH:mm.')
  }

  if (!isInRange(definition.bufferMinutes, 0, 1440)) {
    errors.push('Buffer must be between 0 and 1440 minutes.')
  }

  if (definition.timeZoneId.trim().length === 0) {
    errors.push('Time zone is required.')
  }

  switch (definition.frequency) {
    case 'Weekly':
      if (!isInRange(definition.isoWeekday, 1, 7)) {
        errors.push('Choose a weekday.')
      }
      break
    case 'EveryNDays':
      if (!isInRange(definition.intervalDays, 1, Number.MAX_SAFE_INTEGER)) {
        errors.push('Interval must be at least 1 day.')
      }
      if (!definition.anchorDate) {
        errors.push('Anchor date is required.')
      }
      break
    case 'Monthly':
      if (!isInRange(definition.dayOfMonth, 1, 31)) {
        errors.push('Day of month must be between 1 and 31.')
      }
      break
    case 'Quarterly':
      if (!isInRange(definition.dayOfMonth, 1, 31)) {
        errors.push('Day of month must be between 1 and 31.')
      }
      if (!isInRange(definition.monthOfQuarter, 1, 3)) {
        errors.push('Month of quarter must be 1, 2 or 3.')
      }
      break
    case 'Daily':
      break
  }

  if (definition.sizeCheckEnabled) {
    if (definition.referenceSizeBytes === null || definition.referenceSizeBytes <= 0) {
      errors.push('Reference size must be above 0 when the size check is on.')
    }
    if (definition.sizeTolerancePercent === null || definition.sizeTolerancePercent < 0) {
      errors.push('Size tolerance must be 0 or more when the size check is on.')
    }
  }

  return errors
}

/// Clears the fields that do not apply to the chosen frequency, mode and size check, and trims text, before sending.
export function toPayload(definition: MonitoringDefinitionInput): MonitoringDefinitionInput {
  const isWeekly = definition.frequency === 'Weekly'
  const isEveryN = definition.frequency === 'EveryNDays'
  const isMonthly = definition.frequency === 'Monthly' || definition.frequency === 'Quarterly'
  const isQuarterly = definition.frequency === 'Quarterly'
  const hasDate = definition.filenameDateMode !== 'NoDate'

  return {
    ...definition,
    name: definition.name.trim(),
    provider: definition.provider.trim(),
    relativePath: definition.relativePath.trim(),
    fileNamePattern: definition.fileNamePattern.trim(),
    timeZoneId: definition.timeZoneId.trim(),
    daysOfWeek:
      definition.daysOfWeek && definition.daysOfWeek.length > 0
        ? [...definition.daysOfWeek].sort((a, b) => a - b)
        : null,
    isoWeekday: isWeekly ? definition.isoWeekday : null,
    intervalDays: isEveryN ? definition.intervalDays : null,
    anchorDate: isEveryN ? definition.anchorDate : null,
    dayOfMonth: isMonthly ? definition.dayOfMonth : null,
    monthOfQuarter: isQuarterly ? definition.monthOfQuarter : null,
    filenameDateFormat: hasDate ? ((definition.filenameDateFormat ?? '').trim() || null) : null,
    referenceSizeBytes: definition.sizeCheckEnabled ? definition.referenceSizeBytes : null,
    sizeTolerancePercent: definition.sizeCheckEnabled ? definition.sizeTolerancePercent : null,
  }
}
