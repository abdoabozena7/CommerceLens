import { Activity, ArrowRight, ArrowUpRight, Database, FileSpreadsheet, Layers3, MousePointer2, Package, Radio, RefreshCw, ShoppingBag, Wallet } from 'lucide-react'
import { Link } from 'react-router-dom'
import { ClickDistribution, CtrChart } from '../charts/AnalyticsCharts'
import { KpiCard } from '../components/KpiCard'
import { ErrorState, Loading, PageHeading, Panel } from '../components/Shared'
import { dateTime, money, notifyDataChanged, number, percent } from '../services/api'
import type { AdAnalytics, Overview as OverviewData } from '../services/types'
import { useResource } from '../services/useResource'

export function Overview() {
  const overview = useResource<OverviewData>('/analytics/overview')
  const advertising = useResource<AdAnalytics>('/analytics/advertising')
  return <><PageHeading eyebrow="YOUR WORKSPACE, AT A GLANCE" title="Your commerce, in focus."
    description="Sales performance, advertising activity, and product monitoring in one e-commerce workspace."
    actions={<><span className="source-tag"><span className="status-dot" />Historical + local data</span>
      <button className="button secondary" onClick={() => notifyDataChanged()}><RefreshCw size={14} />Refresh</button></>} />
    {overview.error && <ErrorState message={overview.error} retry={overview.refresh} />}
    {advertising.error && <ErrorState message={advertising.error} retry={advertising.refresh} />}
    {(overview.loading || advertising.loading) && <Loading />}
    {overview.data && advertising.data && <>
      <div className="kpi-grid">
        <KpiCard label="Local order revenue" value={money(overview.data.revenue)} note="Simulated payments, persisted orders" icon={Wallet} tone="sand" testId="revenue-kpi" />
        <KpiCard label="Storefront orders" value={number(overview.data.totalOrders)} note="Saved local demo checkouts" icon={ShoppingBag} tone="mint" testId="orders-kpi" />
        <KpiCard label="Ad observations" value={number(overview.data.totalObservations)} note="Imported historical observations" icon={Layers3} testId="observations-kpi" />
        <KpiCard label="Historical click-through rate" value={percent(overview.data.historicalCtr)} note={`${number(overview.data.totalHistoricalClicks)} clicks / ${number(overview.data.totalObservations)} observations`} icon={MousePointer2} tone="blue" testId="ctr-kpi" />
      </div>
      <div className="source-strip"><div><FileSpreadsheet size={16} /><strong>Historical CSV</strong><span>Observations, not purchases</span></div>
        <span className="strip-divider" /><div><Package size={16} /><strong>{overview.data.totalProducts} demo products</strong><span>Independent catalog</span></div>
        <span className="strip-divider" /><div><Activity size={16} /><strong>{number(overview.data.totalEvents)} live events</strong><span>{overview.data.liveAdClicks} ad clicks / {overview.data.productViews} product views</span></div></div>
      <div className="kpi-grid">
        <KpiCard label="Live ad impressions" value={number(overview.data.liveAdImpressions)} note="Visible ad cards, once per page visit" icon={Radio} tone="blue" testId="live-impressions-kpi" />
        <KpiCard label="Live ad clicks" value={number(overview.data.liveAdClicks)} note="Public ad-card interactions, not CSV labels" icon={MousePointer2} testId="live-clicks-kpi" />
        <KpiCard label="Live storefront CTR" value={overview.data.liveCtr === null ? 'N/A' : percent(overview.data.liveCtr)} note="Recorded ad clicks / impressions; N/A if none" icon={Activity} tone="mint" testId="live-ctr-kpi" />
        <KpiCard label="Product views" value={number(overview.data.productViews)} note="Loaded product-detail pages, including direct links" icon={Package} tone="sand" testId="product-views-kpi" />
      </div>
      <div className="notice"><span className="badge blue">LIVE APPLICATION</span><span>An impression means at least 50% of an ad card is in the viewport of a foreground tab, with no dwell-time requirement. Each ad is counted once per page visit. Storefront collection traffic is excluded. Historical CTR remains separate; old unpaired click events may exceed newer impression counts.</span></div>
      <div className="overview-charts">
        <Panel title="Click-through rate by device" subtitle="Historical observations grouped by device, including missing values.">
          <div className="chart-key"><i />Observed CTR<span>Dashed line: overall baseline</span></div>
          <CtrChart groups={advertising.data.byDevice} baseline={overview.data.historicalCtr} />
          <div className="panel-bottom"><span className="badge lilac">HISTORICAL CSV</span><Link to="/advertising">Explore advertising <ArrowUpRight size={13} /></Link></div>
        </Panel>
        <Panel title="Click distribution" subtitle="An observation is not a purchase.">
          <ClickDistribution clicks={overview.data.totalHistoricalClicks} total={overview.data.totalObservations} />
          <div className="panel-bottom"><span>{number(overview.data.totalObservations)} total observations</span><span className="badge lilac">ALL IMPORTS</span></div>
        </Panel>
      </div>
      <div className="overview-lower">
        <Panel title="A closer look at your audience" subtitle="Observation-based CTR. Repeated user IDs are preserved.">
          <div className="table-scroll"><table className="data-table"><thead><tr><th>Device</th><th>Observations</th><th>Clicks</th><th>Click-through rate</th></tr></thead>
            <tbody>{advertising.data.byDevice.map(group => <tr key={group.name}><td><span className="table-device"><span className="device-symbol"><Layers3 size={14} /></span>{group.name}</span></td>
              <td>{number(group.observations)}</td><td>{number(group.clicks)}</td><td><div className="rate-cell"><span className="progress"><i style={{ width: `${group.ctr * 100}%` }} /></span><strong>{percent(group.ctr)}</strong></div></td></tr>)}</tbody></table></div>
          <div className="panel-bottom"><span>{number(advertising.data.distinctUsers)} distinct users</span><Link to="/explorer">Open data explorer <ArrowRight size={13} /></Link></div>
        </Panel>
        <Panel title="Pipeline health" subtitle="Collection and evaluation status, straight from the API.">
          <div className="health-list">
            <div><span className="health-icon lilac"><Database size={16} /></span><div><strong>Historical dataset</strong><p>{overview.data.lastImportedAt ? dateTime(overview.data.lastImportedAt) : 'No successful import'}</p></div><span className="badge mint">IMPORTED</span></div>
            <div><span className="health-icon blue"><Radio size={16} /></span><div><strong><Link to="/monitoring">Product monitoring</Link></strong><p>{overview.data.lastScrape ? `${overview.data.lastScrape.recordCount} records / ${dateTime(overview.data.lastScrape.startedAt)}` : 'No collection runs yet'}</p></div>
              <span className={`badge ${overview.data.lastScrape?.status === 'Completed' ? 'mint' : 'sand'}`}>{overview.data.lastScrape?.status ?? 'NOT RUN'}</span></div>
            <div><span className="health-icon rose"><Activity size={16} /></span><div><strong>ML model evaluation</strong><p>{overview.data.latestModel ? dateTime(overview.data.latestModel.trainedAt) : 'No evaluated model yet'}</p></div>
              <span className={`badge ${overview.data.latestModel && !overview.data.latestModel.isStale ? 'mint' : 'sand'}`}>{overview.data.latestModel?.status ?? 'NOT TRAINED'}</span></div>
          </div>
        </Panel>
      </div>
      <section className="pipeline-banner"><div className="pipeline-banner-icon"><Layers3 size={24} /></div><div><h2>Commerce data, connected.</h2><p>Saved demo orders, live advertising, and rendered product monitoring. Historical advertising stays a separate source. <Link to="/commerce">Explore sales performance.</Link></p></div>
        <div className="pipeline-numbers"><div><strong>{number(overview.data.totalObservations)}</strong><small>historical rows</small></div><ArrowRight size={16} /><div><strong>{number(overview.data.totalOrders)}</strong><small>local orders</small></div>
          <ArrowRight size={16} /><div><strong>{number(overview.data.totalScrapedRecords)}</strong><small>scraped records</small></div></div></section>
    </>}
  </>
}
