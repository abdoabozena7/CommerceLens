import { ArrowDownUp, CircleAlert, CopyCheck, PackageSearch, Play, RefreshCw, Search, Tags } from 'lucide-react'
import { useState } from 'react'
import { KpiCard } from '../components/KpiCard'
import { EmptyState, ErrorState, Loading, PageHeading, Panel } from '../components/Shared'
import { dateTime, errorMessage, money, notifyDataChanged, post } from '../services/api'
import type { ScrapeDetail, ScrapeSummary } from '../services/types'
import { useResource } from '../services/useResource'

function CollectionResults({ id }: { id: number }) {
  const run = useResource<ScrapeDetail>(`/scrapes/${id}`)
  const [search, setSearch] = useState('')
  const [comparison, setComparison] = useState('all')
  const [descending, setDescending] = useState(false)
  const filtered = run.data?.comparisons.filter(row => row.name.toLowerCase().includes(search.toLowerCase())
    && (comparison === 'all' || comparison === row.status
      || comparison === 'price' && row.priceDifference !== null && row.priceDifference !== 0))
    .sort((a, b) => descending ? b.name.localeCompare(a.name) : a.name.localeCompare(b.name))
  const completed = run.data?.status === 'Completed'
  return <>{run.error && <ErrorState message={run.error} retry={run.refresh} />}{run.loading && <Loading />}
    {run.data && <>{run.data.error && <ErrorState message={run.data.error} />}
      <div className="kpi-grid">
        <KpiCard label="Collected products" value={String(run.data.recordCount)} note={`Run #${run.data.id} / ${run.data.status}`} icon={CopyCheck} testId="collected-products-kpi" />
        <KpiCard label="Price differences" value={completed ? String(run.data.priceMismatchCount) : 'N/A'} note="Collected price differs from the current catalog" icon={Tags} tone="sand" testId="price-differences-kpi" />
        <KpiCard label="Missing products" value={completed ? String(run.data.missingCount) : 'N/A'} note="Catalog products absent from a completed collection" icon={PackageSearch} tone="blue" testId="missing-products-kpi" />
        <KpiCard label="Changed products" value={completed ? String(run.data.mismatchedCount) : 'N/A'} note="Any price, name, category, availability or ad difference" icon={CircleAlert} tone="mint" />
      </div>
      <Panel title={`Collection #${run.data.id}`} subtitle={`${run.data.sourceUrl} / ${dateTime(run.data.startedAt)} / ${run.data.sourceType}`}>
        {!completed ? <EmptyState title="Comparison unavailable">Only a completed collection can establish missing products or catalog differences. This run is {run.data.status.toLowerCase()}.</EmptyState> : <>
          <div className="table-toolbar"><label className="search-field"><Search size={15} /><input aria-label="Search collected products" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search products..." /></label>
            <select aria-label="Comparison filter" value={comparison} onChange={event => setComparison(event.target.value)}>
              <option value="all">All products</option><option value="price">Price differences</option><option value="Mismatched">Changed information</option>
              <option value="Missing">Missing products</option><option value="Extra">Extra products</option><option value="Matched">Matching products</option>
            </select></div>
          <div className="table-scroll"><table className="data-table"><caption>Current catalog versus saved rendered evidence. {run.data.matchedCount} matched; {run.data.extraCount} extra.</caption>
            <thead><tr><th><button onClick={() => setDescending(!descending)}>Product<ArrowDownUp size={11} /></button></th><th>Catalog / collected price</th><th>Price delta</th>
              <th>Catalog / collected category</th><th>Catalog / collected availability</th><th>Comparison</th><th>Collected evidence</th></tr></thead>
            <tbody>{filtered?.map(row => <tr key={row.productId}><td><strong>{row.name}</strong>{row.collectedName && row.name !== row.collectedName && <small className="block muted">Collected: {row.collectedName}</small>}</td>
              <td>{row.storefrontPrice === null ? 'Not in catalog' : money(row.storefrontPrice)} / {row.collectedPrice === null ? 'Not collected' : money(row.collectedPrice)}</td>
              <td>{row.priceDifference === null ? 'N/A' : `${row.priceDifference > 0 ? '+' : ''}${money(row.priceDifference)}`}</td>
              <td>{row.storefrontCategory ?? 'Not in catalog'} / {row.collectedCategory ?? 'Not collected'}</td>
              <td>{row.catalogAvailability ?? 'Not in catalog'} / {row.collectedAvailability ?? 'Not collected'}</td>
              <td><span className={`badge ${row.status === 'Matched' ? 'mint' : 'rose'}`}>{row.status}</span>{row.differences.length > 0 && <small className="block muted">{row.differences.join(', ')}</small>}</td>
              <td>{row.extractedAt ? <details><summary>{dateTime(row.extractedAt)}</summary><p><a href={row.sourceUrl!} target="_blank" rel="noreferrer">Source storefront</a></p>
                <p>{row.adText ? `Public ad: ${row.adText}` : 'No public ad on this card.'}</p></details> : 'No collection evidence'}</td></tr>)}</tbody></table></div>
          {filtered?.length === 0 && <EmptyState title="No matching products">Change the search or comparison filter.</EmptyState>}
        </>}
      </Panel>
    </>}
  </>
}

export function ProductMonitoring() {
  const runs = useResource<ScrapeSummary[]>('/scrapes')
  const [selected, setSelected] = useState<number>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const selectedId = selected ?? runs.data?.[0]?.id
  async function collect() {
    setBusy(true); setError('')
    try { const result = await post<ScrapeDetail>('/scrapes'); setSelected(result.id) }
    catch (error) { setError(errorMessage(error)); notifyDataChanged() }
    finally { setBusy(false) }
  }
  return <><PageHeading eyebrow="PRODUCT MONITORING & DATA COLLECTION" title="Product monitoring"
    description="Inspect your rendered storefront, compare product information, and review collection history."
    actions={<><button className="button secondary" onClick={() => notifyDataChanged()}><RefreshCw size={15} />Refresh</button>
      <button className="button primary" disabled={busy || runs.data?.some(run => run.status === 'Running')} onClick={() => void collect()}>
        {busy ? <RefreshCw className="spin" size={15} /> : <Play size={15} />}{busy ? 'Collecting product data...' : 'Collect product data'}</button></>} />
    <div className="notice"><span className="badge blue">RENDERED STOREFRONT</span><span>Playwright visits your own React store and extracts actual product names, prices, categories, availability and public ad text. Collection traffic is excluded from customer metrics. It does not read product API payloads as collected evidence.</span></div>
    {error && <ErrorState message={error} />}{runs.error && <ErrorState message={runs.error} retry={runs.refresh} />}{runs.loading && <Loading />}
    {runs.data && runs.data.length === 0 && <Panel title="Product collection history"><EmptyState title="No collections yet">Start a collection while the storefront is running. Completed and failed attempts will appear here.</EmptyState></Panel>}
    {selectedId !== undefined && <CollectionResults key={selectedId} id={selectedId} />}
    {runs.data && runs.data.length > 0 && <Panel title="Collection history" subtitle="Latest 20 attempts. Select a run to inspect its saved evidence and current-catalog comparison.">
      <div className="table-scroll"><table className="data-table"><thead><tr><th>Run</th><th>Status</th><th>Started</th><th>Completed</th><th>Products</th><th>Failure details</th></tr></thead>
        <tbody>{runs.data.map(run => <tr key={run.id}><td><button className="text-button" aria-pressed={selectedId === run.id} onClick={() => setSelected(run.id)}>View run #{run.id}</button></td>
          <td><span className={`badge ${run.status === 'Completed' ? 'mint' : 'sand'}`}>{run.status}</span></td><td>{dateTime(run.startedAt)}</td>
          <td>{run.completedAt ? dateTime(run.completedAt) : 'In progress'}</td><td>{run.recordCount}</td><td>{run.error ?? 'None'}</td></tr>)}</tbody></table></div>
    </Panel>}
    <div className="notice"><span className="badge lilac">COMPARISON SCOPE</span><span>Price delta is collected price minus the current database price. Missing means a current catalog product was absent from a completed collection. Later catalog edits can change comparisons; this is not a historical price-trend model. Collected product data never becomes sales or CTR training data.</span></div>
  </>
}
