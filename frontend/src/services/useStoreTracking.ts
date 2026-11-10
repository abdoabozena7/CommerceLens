import { useCallback, useRef, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { post } from './api'

type StoreEventType = 'ProductView' | 'AdImpression' | 'AdClick'

export function useStoreTracking() {
  const location = useLocation()
  const [visitId] = useState(() => crypto.randomUUID())
  const enabled = new URLSearchParams(location.search).get('collection') !== '1'
  const attempts = useRef(new Map<string, Promise<void>>())
  const track = useCallback((productId: number, eventType: StoreEventType): Promise<void> => {
    if (!enabled) return Promise.resolve()
    const eventKey = `${visitId}:${location.key}:${productId}:${eventType}`
    const previous = attempts.current.get(eventKey)
    if (previous) return previous
    const pending = post<void>('/events', { productId, eventType, eventKey }).catch(error => {
      attempts.current.delete(eventKey)
      throw error
    })
    attempts.current.set(eventKey, pending)
    return pending
  }, [enabled, location.key, visitId])
  return { track, enabled }
}
