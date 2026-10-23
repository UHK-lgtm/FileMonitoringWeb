interface StatCardProps {
  label: string
  value: number | string
  tone: 'ok' | 'pending' | 'attention' | 'warn' | 'neutral'
  hint?: string
}

export function StatCard({ label, value, tone, hint }: StatCardProps) {
  return (
    <div className={`kpi kpi-${tone}`}>
      <span className="kpi-label">{label}</span>
      <span className="kpi-value">{value}</span>
      {hint && <span className="kpi-hint">{hint}</span>}
    </div>
  )
}
