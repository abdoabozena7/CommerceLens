import { AlertCircle, LoaderCircle, PackageOpen } from 'lucide-react'
import type { ReactNode } from 'react'

export function ErrorState({ message, retry }: { message: string; retry?: () => void }) {
  return <div className="notice error" role="alert"><AlertCircle size={19} /><span>{message}</span>
    {retry && <button className="button secondary small" onClick={retry}>Try again</button>}</div>
}

export function Loading() {
  return <div className="loading" role="status"><LoaderCircle className="spin" size={22} /> Loading live results...</div>
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return <div className="empty"><PackageOpen size={34} /><h3>{title}</h3><p>{children}</p></div>
}

export function PageHeading({ eyebrow, title, description, actions }: {
  eyebrow: string; title: string; description: string; actions?: ReactNode
}) {
  return <div className="page-heading"><div><div className="eyebrow">{eyebrow}</div><h1>{title}</h1>
    <p>{description}</p></div><div className="heading-actions">{actions}</div></div>
}

export function Panel({ title, subtitle, children, className = '' }: {
  title: string; subtitle?: string; children: ReactNode; className?: string
}) {
  return <section className={`panel ${className}`}><div className="panel-heading"><h2>{title}</h2>
    {subtitle && <p>{subtitle}</p>}</div>{children}</section>
}
