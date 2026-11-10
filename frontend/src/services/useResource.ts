import { useEffect, useState } from 'react'
import { errorMessage, request } from './api'

export function useResource<T>(path: string) {
  const [revision, setRevision] = useState(0)
  const key = `${path}:${revision}`
  const [result, setResult] = useState<{ path: string; key: string; data?: T; error?: string }>({ path: '', key: '' })
  useEffect(() => {
    const controller = new AbortController()
    request<T>(path, { signal: controller.signal }).then(
      data => { if (!controller.signal.aborted) setResult({ path, key, data }) },
      error => {
        if (!controller.signal.aborted) setResult(previous => ({
          path, key, data: previous.path === path ? previous.data : undefined, error: errorMessage(error),
        }))
      },
    )
    return () => controller.abort()
  }, [path, key])
  useEffect(() => {
    const refresh = (event: Event) => {
      if (event instanceof CustomEvent && event.detail === 'events' && path !== '/analytics/overview') return
      setRevision(value => value + 1)
    }
    window.addEventListener('commercelens-data', refresh)
    window.addEventListener('focus', refresh)
    return () => {
      window.removeEventListener('commercelens-data', refresh)
      window.removeEventListener('focus', refresh)
    }
  }, [path])
  return {
    data: result.path === path ? result.data : undefined,
    error: result.key === key ? result.error : undefined,
    loading: result.key !== key && !(result.path === path && result.data !== undefined),
    refresh: () => setRevision(value => value + 1),
  }
}
