import type { LucideIcon } from 'lucide-react'

export function KpiCard({ label, value, note, icon: Icon, tone = 'lilac', testId }: {
  label: string; value: string; note: string; icon: LucideIcon; tone?: string; testId?: string
}) {
  return <section className="kpi-card"><div className="kpi-top"><span>{label}</span><span className={`kpi-icon ${tone}`}><Icon size={16} strokeWidth={1.7} /></span></div>
    <strong className="kpi-value" data-testid={testId}>{value}</strong><p>{note}</p></section>
}
