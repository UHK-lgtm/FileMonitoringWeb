import { useState, type FormEvent, type ReactNode } from 'react'
import { ApiError, api, messagesFrom, type FilenameDateMode, type FrequencyName, type MonitoringDefinition, type MonitoringDefinitionInput } from '../api'
import { validateDefinition, toPayload } from '../definitionRules'
import { ErrorList } from './ErrorList'

interface DefinitionFormProps {
  // null creates a new definition. The parent gives a key per definition, so the draft survives a reload of the same one.
  definition: MonitoringDefinition | null
  readOnly: boolean
  onSaved: (saved: MonitoringDefinition) => void
  onConflict: () => void
  onCancel: () => void
}

const frequencies: FrequencyName[] = ['Daily', 'Weekly', 'EveryNDays', 'Monthly', 'Quarterly']
const dateModes: FilenameDateMode[] = ['NoDate', 'SameDay', 'PreviousDay', 'MonthYear']
const weekdays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

function emptyDefinition(): MonitoringDefinitionInput {
  return {
    name: '',
    provider: '',
    relativePath: '',
    fileNamePattern: '',
    expectedFileCount: 1,
    frequency: 'Daily',
    isoWeekday: null,
    intervalDays: null,
    anchorDate: null,
    dayOfMonth: null,
    monthOfQuarter: null,
    expectedDeliveryTime: '09:00',
    bufferMinutes: 0,
    timeZoneId: 'Europe/Luxembourg',
    filenameDateMode: 'NoDate',
    filenameDateFormat: null,
    sizeCheckEnabled: false,
    referenceSizeBytes: null,
    sizeTolerancePercent: null,
    isEnabled: true,
  }
}

function toInput(definition: MonitoringDefinition): MonitoringDefinitionInput {
  const { id: _id, isRetired: _isRetired, concurrencyToken: _token, ...input } = definition
  return input
}

function numberOrNull(value: string): number | null {
  return value.trim() === '' ? null : Number(value)
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="field">
      {label}
      {children}
    </label>
  )
}

export function DefinitionForm({ definition, readOnly, onSaved, onConflict, onCancel }: DefinitionFormProps) {
  const [draft, setDraft] = useState<MonitoringDefinitionInput>(() =>
    definition ? toInput(definition) : emptyDefinition(),
  )
  const [errors, setErrors] = useState<string[]>([])
  const [saving, setSaving] = useState(false)

  function update<K extends keyof MonitoringDefinitionInput>(key: K, value: MonitoringDefinitionInput[K]) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const problems = validateDefinition(draft)
    if (problems.length > 0) {
      setErrors(problems)
      return
    }

    const payload = toPayload(draft)
    setSaving(true)
    setErrors([])
    try {
      const saved = definition
        ? await api.updateDefinition(definition.id, payload, definition.concurrencyToken)
        : await api.createDefinition(payload)
      onSaved(saved)
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setErrors([
          'Someone else changed this definition. It has been reloaded. Your changes are still here: review them and save again.',
        ])
        onConflict()
      } else {
        setErrors(messagesFrom(error))
      }
    } finally {
      setSaving(false)
    }
  }

  const isWeekly = draft.frequency === 'Weekly'
  const isEveryN = draft.frequency === 'EveryNDays'
  const isMonthly = draft.frequency === 'Monthly' || draft.frequency === 'Quarterly'
  const isQuarterly = draft.frequency === 'Quarterly'
  const hasDate = draft.filenameDateMode !== 'NoDate'
  const disabled = readOnly || saving

  return (
    <form className="definition-form" onSubmit={save}>
      <fieldset disabled={disabled}>
        <h3>{definition ? `Edit: ${definition.name}` : 'New definition'}</h3>
        {readOnly && <p className="muted">This definition is retired. It cannot be changed.</p>}

        <div className="form-grid">
          <Field label="Name">
            <input value={draft.name} onChange={(e) => update('name', e.target.value)} maxLength={200} />
          </Field>
          <Field label="Provider">
            <input value={draft.provider} onChange={(e) => update('provider', e.target.value)} maxLength={100} />
          </Field>
          <Field label="Relative path (inside IN/PRD, empty for share root)">
            <input value={draft.relativePath} onChange={(e) => update('relativePath', e.target.value)} />
          </Field>
          <Field label="File name pattern (* and ? allowed)">
            <input value={draft.fileNamePattern} onChange={(e) => update('fileNamePattern', e.target.value)} maxLength={260} />
          </Field>
          <Field label="Expected file count">
            <input
              type="number"
              min={1}
              value={draft.expectedFileCount}
              onChange={(e) => update('expectedFileCount', Number(e.target.value))}
            />
          </Field>
          <Field label="Frequency">
            <select value={draft.frequency} onChange={(e) => update('frequency', e.target.value as FrequencyName)}>
              {frequencies.map((frequency) => (
                <option key={frequency} value={frequency}>
                  {frequency}
                </option>
              ))}
            </select>
          </Field>

          {isWeekly && (
            <Field label="Weekday">
              <select
                value={draft.isoWeekday ?? ''}
                onChange={(e) => update('isoWeekday', numberOrNull(e.target.value))}
              >
                <option value="">Choose…</option>
                {weekdays.map((name, index) => (
                  <option key={name} value={index + 1}>
                    {name}
                  </option>
                ))}
              </select>
            </Field>
          )}

          {isEveryN && (
            <>
              <Field label="Every N days">
                <input
                  type="number"
                  min={1}
                  value={draft.intervalDays ?? ''}
                  onChange={(e) => update('intervalDays', numberOrNull(e.target.value))}
                />
              </Field>
              <Field label="Anchor date (the cycle counts from here)">
                <input type="date" value={draft.anchorDate ?? ''} onChange={(e) => update('anchorDate', e.target.value || null)} />
              </Field>
            </>
          )}

          {isMonthly && (
            <Field label="Day of month (31 = last day in shorter months)">
              <input
                type="number"
                min={1}
                max={31}
                value={draft.dayOfMonth ?? ''}
                onChange={(e) => update('dayOfMonth', numberOrNull(e.target.value))}
              />
            </Field>
          )}

          {isQuarterly && (
            <Field label="Month of quarter (1 = first month of the quarter)">
              <select
                value={draft.monthOfQuarter ?? ''}
                onChange={(e) => update('monthOfQuarter', numberOrNull(e.target.value))}
              >
                <option value="">Choose…</option>
                <option value={1}>1</option>
                <option value={2}>2</option>
                <option value={3}>3</option>
              </select>
            </Field>
          )}

          <Field label="Expected delivery time (local)">
            <input type="time" value={draft.expectedDeliveryTime} onChange={(e) => update('expectedDeliveryTime', e.target.value)} />
          </Field>
          <Field label="Buffer (minutes)">
            <input
              type="number"
              min={0}
              max={1440}
              value={draft.bufferMinutes}
              onChange={(e) => update('bufferMinutes', Number(e.target.value))}
            />
          </Field>
          <Field label="Time zone (IANA)">
            <input value={draft.timeZoneId} onChange={(e) => update('timeZoneId', e.target.value)} />
          </Field>
          <Field label="Filename date">
            <select
              value={draft.filenameDateMode}
              onChange={(e) => update('filenameDateMode', e.target.value as FilenameDateMode)}
            >
              {dateModes.map((mode) => (
                <option key={mode} value={mode}>
                  {mode}
                </option>
              ))}
            </select>
          </Field>
          {hasDate && (
            <Field label="Date format (for example yyyyMMdd)">
              <input value={draft.filenameDateFormat ?? ''} onChange={(e) => update('filenameDateFormat', e.target.value)} />
            </Field>
          )}
        </div>

        <div className="form-grid">
          <label className="checkbox">
            <input
              type="checkbox"
              checked={draft.sizeCheckEnabled}
              onChange={(e) => update('sizeCheckEnabled', e.target.checked)}
            />
            Size check
          </label>
          {draft.sizeCheckEnabled && (
            <>
              <Field label="Reference size (bytes)">
                <input
                  type="number"
                  min={1}
                  value={draft.referenceSizeBytes ?? ''}
                  onChange={(e) => update('referenceSizeBytes', numberOrNull(e.target.value))}
                />
              </Field>
              <Field label="Tolerance (%)">
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  value={draft.sizeTolerancePercent ?? ''}
                  onChange={(e) => update('sizeTolerancePercent', numberOrNull(e.target.value))}
                />
              </Field>
            </>
          )}
        </div>

        <ErrorList messages={errors} />

        <div className="actions">
          {!readOnly && (
            <button type="submit" className="button">
              {saving ? 'Saving…' : 'Save'}
            </button>
          )}
          <button type="button" className="button secondary" onClick={onCancel}>
            Close
          </button>
        </div>
      </fieldset>
    </form>
  )
}
