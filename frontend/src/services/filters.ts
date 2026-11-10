export type AdFiltersState = Partial<Record<'deviceType' | 'adPosition' | 'browsingHistory' | 'timeOfDay' | 'click' | 'importBatchId' | 'search', string>>

export function adQuery(filters: AdFiltersState) {
  return new URLSearchParams(Object.entries(filters).filter(([, value]) => value !== '')).toString()
}
