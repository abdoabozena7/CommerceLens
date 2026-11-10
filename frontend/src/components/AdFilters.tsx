import { RotateCcw, SlidersHorizontal } from 'lucide-react'
import type { FilterOptions } from '../services/types'
import { useResource } from '../services/useResource'
import { ErrorState } from './Shared'
import type { AdFiltersState } from '../services/filters'
const fields = [
  { key: 'deviceType', label: 'Device type', options: 'deviceTypes' },
  { key: 'adPosition', label: 'Ad position', options: 'adPositions' },
  { key: 'browsingHistory', label: 'Browsing history', options: 'browsingHistories' },
  { key: 'timeOfDay', label: 'Time of day', options: 'timesOfDay' },
] as const

export function AdFilters({ value, onChange }: { value: AdFiltersState; onChange: (value: AdFiltersState) => void }) {
  const options = useResource<FilterOptions>('/analytics/options')
  return <div className="filter-block">{options.error && <ErrorState message={options.error} retry={options.refresh} />}
    <div className="filter-bar"><span className="filter-label"><SlidersHorizontal size={15} />Filters</span>
      {fields.map(field => <label key={field.key}><span className="sr-only">{field.label}</span><select aria-label={field.label} value={value[field.key] ?? ''}
        onChange={event => onChange({ ...value, [field.key]: event.target.value })}><option value="">{field.label}: all</option>
        {options.data?.[field.options].map(option => <option key={option}>{option}</option>)}</select></label>)}
      <select aria-label="Click outcome" value={value.click ?? ''} onChange={event => onChange({ ...value, click: event.target.value })}>
        <option value="">All outcomes</option><option value="true">Clicked</option><option value="false">Not clicked</option></select>
      <select aria-label="Import batch" value={value.importBatchId ?? ''} onChange={event => onChange({ ...value, importBatchId: event.target.value })}>
        <option value="">All imports</option>{options.data?.imports.map(batch => <option key={batch.id} value={batch.id}>{batch.fileName} (#{batch.id})</option>)}</select>
      <button className="icon-button" aria-label="Reset filters" onClick={() => onChange({})}><RotateCcw size={14} /></button>
    </div></div>
}
