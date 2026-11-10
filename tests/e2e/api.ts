import { expect } from '@playwright/test'
import type { APIRequestContext } from '@playwright/test'

export async function getApi<T>(request: APIRequestContext, path: string): Promise<T> {
  const response = await request.get(`/api${path}`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as T
}
