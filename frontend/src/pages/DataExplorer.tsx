import { ArrowDownUp, ChevronLeft, ChevronRight, FileSpreadsheet, Search, Upload } from 'lucide-react'
import { useState } from 'react'
import { MissingTable } from '../charts/AnalyticsCharts'
import { AdFilters } from '../components/AdFilters'
import { adQuery, type AdFiltersState } from '../services/filters'
import { EmptyState, ErrorState, Loading, PageHeading, Panel } from '../components/Shared'
import { dateTime, errorMessage, number, uploadCsv } from '../services/api'
import type { AdAnalytics, AdRow, ImportResult, Page } from '../services/types'
import { useResource } from '../services/useResource'

export function DataExplorer() {
  const [filters, setFilters] = useState<AdFiltersState>({})
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  const [sort, setSort] = useState('id')
  const [descending, setDescending] = useState(false)
  const [file, setFile] = useState<File>()
  const [report, setReport] = useState<ImportResult>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const query = adQuery(filters)
  const rows = useResource<Page<AdRow>>(`/data/rows?${query}&page=${page}&pageSize=${pageSize}&sort=${sort}&descending=${descending}`)
  const stats = useResource<AdAnalytics>(`/analytics/advertising?${query}`)
  const imports = useResource<ImportResult[]>('/data/imports')
  const updateFilters = (value: AdFiltersState) => { setFilters(value); setPage(1) }
  function changeSort(column: string) {
    setDescending(sort === column ? !descending : false); setSort(column); setPage(1)
  }
  async function upload() {
    if (!file) return
    setBusy(true); setError(''); setReport(undefined)
    try { setReport(await uploadCsv(file)) }
    catch (error) { setError(errorMessage(error)) }
    finally { setBusy(false) }
  }
  return <><PageHeading eyebrow="HISTORICAL DATA / INSPECT & IMPORT" title="Data explorer" description="Every observation has its own database key. The original user ID is a separate, repeatable field."
    actions={<span className="badge lilac">PERSONAL NAMES NOT EXPOSED</span>} />
    <AdFilters value={filters} onChange={updateFilters} />
    <Panel title="Imported observations" subtitle="Search user IDs or categorical values. Missing features are displayed as Unknown.">
      <div className="table-toolbar"><label className="search-field"><Search size={15} /><input aria-label="Search dataset" placeholder="Search user ID or category..." value={filters.search ?? ''} onChange={event => updateFilters({ ...filters, search: event.target.value })} /></label>
        <label className="select-inline">Rows per page<select aria-label="Rows per page" value={pageSize} onChange={event => { setPageSize(Number(event.target.value)); setPage(1) }}><option>10</option><option>25</option><option>50</option><option>100</option></select></label></div>
      {rows.error && <ErrorState message={rows.error} retry={rows.refresh} />}{rows.loading && <Loading />}
      {rows.data && <><div className="table-scroll"><table className="data-table explorer-table"><thead><tr>
        {[['id', 'Observation ID'], ['userId', 'Original user ID'], ['age', 'Age']].map(([key, title]) => <th key={key}><button onClick={() => changeSort(key)}>{title}<ArrowDownUp size={11} /></button></th>)}
        <th>Gender</th><th><button onClick={() => changeSort('device')}>Device<ArrowDownUp size={11} /></button></th><th>Ad position</th><th>Browsing history</th><th>Time of day</th>
        <th><button onClick={() => changeSort('click')}>Clicked<ArrowDownUp size={11} /></button></th><th>Import</th></tr></thead>
        <tbody>{rows.data.items.map(row => <tr key={row.id}><td className="row-id">#{row.id}</td><td>{row.originalUserId}</td><td>{row.age ?? <span className="muted">Missing</span>}</td><td>{row.gender ?? <span className="muted">Unknown</span>}</td>
          <td>{row.deviceType}</td><td>{row.adPosition}</td><td>{row.browsingHistory}</td><td>{row.timeOfDay}</td><td><span className={`badge ${row.click ? 'mint' : 'sand'}`}>{row.click ? 'YES' : 'NO'}</span></td><td>#{row.importBatchId}</td></tr>)}</tbody></table></div>
        {rows.data.total === 0 && <EmptyState title="No matching rows">Reset filters or choose a different search.</EmptyState>}
        <div className="pagination"><span>{rows.data.total ? `${number((page - 1) * pageSize + 1)}-${number(Math.min(page * pageSize, rows.data.total))} of ${number(rows.data.total)} observations` : '0 observations'}</span>
          <div><button className="icon-button" aria-label="Previous page" disabled={page === 1} onClick={() => setPage(page - 1)}><ChevronLeft size={15} /></button>
            <span>Page {page} of {Math.max(1, Math.ceil(rows.data.total / pageSize))}</span><button className="icon-button" aria-label="Next page" disabled={page * pageSize >= rows.data.total} onClick={() => setPage(page + 1)}><ChevronRight size={15} /></button></div></div></>}
    </Panel>
    {stats.error && <ErrorState message={stats.error} retry={stats.refresh} />}
    {stats.data && <Panel title="Column statistics & missing values" subtitle={`${number(stats.data.totalObservations)} matching rows / ${number(stats.data.distinctUsers)} distinct users / nine supplied columns.`}>
      <MissingTable values={stats.data.missingValues} /></Panel>}
    <Panel title="Import an additional CSV" subtitle="The original nine-column schema is supported. Files are validated completely before any observations are committed.">
      <div className="import-box"><span className="import-symbol"><FileSpreadsheet size={28} /></span><div><h3>Keep your sources traceable.</h3><p>Up to 20 MB or 100,000 rows. Identical file contents are never imported twice.</p>
        <label className="file-input"><span className="sr-only">Choose CSV file</span><input aria-label="Choose CSV file" type="file" accept=".csv,text/csv" onChange={event => setFile(event.target.files?.[0])} /></label></div>
        <button className="button primary" disabled={!file || busy} onClick={() => void upload()}><Upload size={15} />{busy ? 'Validating...' : 'Import CSV'}</button></div>
      <div className="schema-note"><code>id,full_name,age,gender,device_type,ad_position,browsing_history,time_of_day,click</code></div>
      {error && <ErrorState message={error} />}
      {report && <div className={`import-report ${report.status === 'Rejected' ? 'rejected' : ''}`} role={report.status === 'Rejected' ? 'alert' : 'status'}>
        <strong>{report.status === 'Rejected' ? 'Import rejected. No observations were saved.' : report.alreadyImported ? 'Already imported. No observations were duplicated.' : `Imported ${number(report.rowCount)} observations.`}</strong>
        {report.errors.map((issue, index) => <p key={index}>Row {issue.row}, {issue.column}: {issue.message}</p>)}</div>}
      <div className="table-scroll"><table className="data-table"><thead><tr><th>Recent import</th><th>Status</th><th>Rows</th><th>Collected</th><th>Validation</th></tr></thead>
        <tbody>{imports.data?.map(batch => <tr key={batch.id}><td>{batch.fileName}</td><td><span className={`badge ${batch.status === 'Imported' ? 'mint' : 'rose'}`}>{batch.status}</span></td>
          <td>{number(batch.rowCount)}</td><td>{dateTime(batch.importedAt)}</td><td>{batch.errors.length ? <details><summary>{batch.errors.length} errors</summary>{batch.errors.map((issue, index) => <p key={index}>Row {issue.row}, {issue.column}: {issue.message}</p>)}</details> : 'Valid'}</td></tr>)}</tbody></table></div>
      {imports.error && <ErrorState message={imports.error} retry={imports.refresh} />}
    </Panel>
  </>
}
