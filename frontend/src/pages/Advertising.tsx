import { Layers3, MousePointer2, Target, Users } from 'lucide-react'
import { useState } from 'react'
import { ClickDistribution, CtrChart, MissingTable } from '../charts/AnalyticsCharts'
import { AdFilters } from '../components/AdFilters'
import { adQuery, type AdFiltersState } from '../services/filters'
import { KpiCard } from '../components/KpiCard'
import { ErrorState, Loading, PageHeading, Panel } from '../components/Shared'
import { number, percent } from '../services/api'
import type { AdAnalytics } from '../services/types'
import { useResource } from '../services/useResource'

export function Advertising() {
  const [filters, setFilters] = useState<AdFiltersState>({})
  const analytics = useResource<AdAnalytics>(`/analytics/advertising?${adQuery(filters)}`)
  return <><PageHeading eyebrow="HISTORICAL CSV" title="Advertising analytics" description="Understand who clicks, where, and in which context. Combine filters to explore a segment."
    actions={<span className="badge lilac">OBSERVATIONS, NOT PURCHASES</span>} />
    <AdFilters value={filters} onChange={setFilters} />
    {analytics.error && <ErrorState message={analytics.error} retry={analytics.refresh} />}{analytics.loading && <Loading />}
    {analytics.data && <><div className="kpi-grid">
      <KpiCard label="Filtered observations" value={number(analytics.data.totalObservations)} note="All matching CSV rows" icon={Layers3} />
      <KpiCard label="Historical clicks" value={number(analytics.data.totalClicks)} note="Rows with click = 1" icon={MousePointer2} tone="blue" />
      <KpiCard label="Click-through rate" value={percent(analytics.data.ctr)} note="Clicks / observations in this segment" icon={Target} tone="mint" />
      <KpiCard label="Distinct users" value={number(analytics.data.distinctUsers)} note="User IDs can repeat across rows" icon={Users} tone="sand" />
    </div>
      <div className="two-column"><Panel title="CTR by device type" subtitle="Includes an Unknown group for missing features."><CtrChart groups={analytics.data.byDevice} /></Panel>
        <Panel title="CTR by ad position" subtitle="Top, side, and bottom placements are categories."><CtrChart groups={analytics.data.byPosition} /></Panel>
        <Panel title="CTR by time of day" subtitle="Categorical context, not a chronological trend."><CtrChart groups={analytics.data.byTimeOfDay} /></Panel>
        <Panel title="CTR by browsing history" subtitle="Missing values remain visible as Unknown."><CtrChart groups={analytics.data.byBrowsingHistory} /></Panel></div>
      <div className="overview-charts reverse"><Panel title="Click vs. non-click" subtitle="Distribution within the active filters."><ClickDistribution clicks={analytics.data.totalClicks} total={analytics.data.totalObservations} /></Panel>
        <Panel title="Missing-value analysis" subtitle={`${number(analytics.data.rowsWithMissingValues)} rows have at least one missing value. Nothing is silently imputed in the explorer.`}>
          <MissingTable values={analytics.data.missingValues} /></Panel></div>
      <div className="notice"><span className="badge lilac">DATA NOTE</span><span>No event dates, order values, ad spend, or purchases exist in this dataset. Revenue and ROI cannot be derived from these clicks.</span></div>
    </>}
  </>
}
