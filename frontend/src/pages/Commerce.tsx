import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Banknote, PackageCheck, Receipt, ShoppingBag } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { KpiCard } from '../components/KpiCard'
import { EmptyState, ErrorState, Loading, PageHeading, Panel } from '../components/Shared'
import { dateTime, money, number } from '../services/api'
import type { CommerceAnalytics } from '../services/types'
import { useResource } from '../services/useResource'

export function Commerce() {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const query = new URLSearchParams()
  if (from) query.set('from', from)
  if (to) query.set('to', to)
  const sales = useResource<CommerceAnalytics>(`/analytics/commerce?${query}`)
  return <><PageHeading eyebrow="LIVE APPLICATION / DEMO CHECKOUT" title="Sales analytics" description="Saved orders and exact totals from your local storefront. No fabricated sales."
    actions={<Link className="button primary" to="/store"><ShoppingBag size={15} />Open storefront</Link>} />
    <div className="filter-bar"><span className="filter-label">Order dates (UTC)</span>
      <label className="date-filter">From<input aria-label="Orders from" type="date" value={from} onChange={event => setFrom(event.target.value)} /></label>
      <label className="date-filter">To<input aria-label="Orders to" type="date" value={to} onChange={event => setTo(event.target.value)} /></label>
      <button className="text-button" onClick={() => { setFrom(''); setTo('') }}>All time</button></div>
    {sales.error && <ErrorState message={sales.error} retry={sales.refresh} />}{sales.loading && <Loading />}
    {sales.data && <><div className="kpi-grid">
      <KpiCard label="Total orders" value={number(sales.data.totalOrders)} note="Persisted demo checkouts" icon={Receipt} />
      <KpiCard label="Order revenue" value={money(sales.data.revenue)} note="Simulated payments, real local records" icon={Banknote} tone="mint" />
      <KpiCard label="Average order value" value={money(sales.data.averageOrderValue)} note="Order revenue / saved orders" icon={ShoppingBag} tone="blue" />
      <KpiCard label="Units sold" value={number(sales.data.unitsSold)} note="Quantities on saved order lines" icon={PackageCheck} tone="sand" />
    </div>{sales.data.totalOrders === 0 ? <Panel title="Your sales story starts here"><EmptyState title="No orders in this period">
      Complete a demo checkout to see real saved activity. <Link className="text-button" to="/store">Browse the collection</Link></EmptyState></Panel> : <>
      <div className="two-column"><Panel title="Sales over time" subtitle="UTC dates from actual CreatedAt order timestamps."><div className="chart-area"><ResponsiveContainer width="100%" height={250} minWidth={0}>
        <LineChart data={sales.data.dailySales} margin={{ top: 25, right: 20, bottom: 10, left: 0 }}><CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 4" />
          <XAxis dataKey="date" tickFormatter={value => String(value).split('T')[0]} tick={{ fontSize: 10 }} axisLine={false} tickLine={false} />
          <YAxis tickFormatter={value => `$${value}`} tick={{ fontSize: 10 }} axisLine={false} tickLine={false} />
          <Tooltip formatter={value => [money(Number(value)), 'Order revenue']} labelFormatter={value => String(value).split('T')[0]} />
          <Line type="linear" dataKey="revenue" stroke="#7d62ed" strokeWidth={3} dot={{ r: 5, fill: '#7d62ed' }} isAnimationActive={false} /></LineChart></ResponsiveContainer></div></Panel>
        <Panel title="Revenue by category" subtitle="Snapshot categories saved on each order line."><div className="chart-area"><ResponsiveContainer width="100%" height={250} minWidth={0}>
          <BarChart data={sales.data.byCategory} margin={{ top: 25, left: 0 }} barSize={35}><CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 4" />
            <XAxis dataKey="name" tick={{ fontSize: 10 }} axisLine={false} tickLine={false} /><YAxis tickFormatter={value => `$${value}`} tick={{ fontSize: 10 }} axisLine={false} tickLine={false} />
            <Tooltip formatter={value => [money(Number(value)), 'Revenue']} /><Bar dataKey="revenue" fill="#9d84ed" radius={[5, 5, 0, 0]} isAnimationActive={false} /></BarChart></ResponsiveContainer></div></Panel></div>
      <Panel title="Top-selling products" subtitle="Ranked by units sold, not by historical ad clicks."><div className="table-scroll"><table className="data-table"><thead><tr><th>Product</th><th>Units sold</th><th>Revenue</th><th>Source</th></tr></thead>
        <tbody>{sales.data.topProducts.map(product => <tr key={product.name}><td>{product.name}</td><td>{product.units}</td><td>{money(product.revenue)}</td><td><span className="badge mint">LOCAL DEMO CHECKOUT</span></td></tr>)}</tbody></table></div></Panel>
      <Panel title="Recent orders" subtitle="The ten most recent orders within the selected period."><div className="table-scroll"><table className="data-table"><thead><tr><th>Order</th><th>Created</th><th>Items</th><th>Total</th><th>Source</th></tr></thead>
        <tbody>{sales.data.recentOrders.map(order => <tr key={order.id}><td>#{order.id}</td><td>{dateTime(order.createdAt)}</td><td>{order.items.reduce((sum, item) => sum + item.quantity, 0)}</td>
          <td>{money(order.total)}</td><td><span className="badge mint">{order.sourceType}</span></td></tr>)}</tbody></table></div></Panel>
    </>}
      <div className="notice"><span className="badge mint">SOURCE NOTE</span><span>All checkout activity is explicitly simulated. No real money is charged. Historical clicks are never counted as orders, conversions, or ad ROI.</span></div>
    </>}
  </>
}
