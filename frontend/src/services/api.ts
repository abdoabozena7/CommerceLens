const changes = new BroadcastChannel('commercelens-data')
type ChangeScope = 'all' | 'events'
const changed = (scope: ChangeScope) => window.dispatchEvent(new CustomEvent('commercelens-data', { detail: scope }))
changes.onmessage = event => changed(event.data === 'events' ? 'events' : 'all')

export function notifyDataChanged(scope: ChangeScope = 'all') {
  changed(scope)
  changes.postMessage(scope)
}

interface ApiProblem { title?: string; detail?: string; errors?: Record<string, string[]> }

export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, init)
  if (!response.ok) {
    const text = await response.text()
    let problem: ApiProblem
    try { problem = JSON.parse(text) as ApiProblem }
    catch { throw new Error(`Request failed (${response.status}): ${text.slice(0, 180) || response.statusText}`) }
    throw new Error(problem.errors ? Object.values(problem.errors).flat().join(' ')
      : problem.detail || problem.title || `Request failed (${response.status}).`)
  }
  return response.status === 204 ? undefined as T : await response.json() as T
}

export async function post<T>(path: string, body?: unknown) {
  const result = await request<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
  if (path !== '/predictions') notifyDataChanged(path === '/events' ? 'events' : 'all')
  return result
}

export async function uploadCsv(file: File): Promise<ImportResult> {
  const form = new FormData()
  form.append('file', file)
  const response = await fetch('/api/data/imports', { method: 'POST', body: form })
  if (!response.ok && response.status !== 422) {
    const body = await response.text()
    throw new Error(`CSV upload failed (${response.status}): ${body.slice(0, 250)}`)
  }
  const report = await response.json() as ImportResult
  // HTTP 422 is an explicit validation report, not a successful import.
  notifyDataChanged()
  return report
}

export const money = (value: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value)
export const number = (value: number) => new Intl.NumberFormat('en-US').format(value)
export const percent = (value: number) => `${(value * 100).toFixed(1)}%`
export const dateTime = (value: string) => new Date(value).toLocaleString()
export const errorMessage = (error: unknown) => error instanceof Error ? error.message : String(error)
import type { ImportResult } from './types'
