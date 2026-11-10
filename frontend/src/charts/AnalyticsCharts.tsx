import { Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { GroupCtr, MissingValue } from '../services/types'
import { number, percent } from '../services/api'
import { EmptyState } from '../components/Shared'

const colors = ['#7d62ed', '#aa95f3', '#c5b7f7', '#dfd6fb', '#9bb1eb', '#badbd0']
const tooltip = { borderRadius: 9, border: '1px solid var(--border)', background: 'var(--surface)', color: 'var(--text)', fontSize: 11 }

export function CtrChart({ groups, baseline }: { groups: GroupCtr[]; baseline?: number }) {
  if (groups.length === 0) return <EmptyState title="No matching observations">Adjust your filters to see a breakdown.</EmptyState>
  const data = groups.map(group => ({ ...group, rate: group.ctr * 100 }))
  return <div className="chart-area"><ResponsiveContainer width="100%" height={235} minWidth={0}>
    <BarChart data={data} margin={{ top: 24, right: 12, bottom: 4, left: -20 }} barSize={32}>
      <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 4" />
      <XAxis dataKey="name" axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: 'var(--muted)' }} interval={0} dy={9} />
      <YAxis domain={[0, 100]} axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: 'var(--muted)' }} tickFormatter={value => `${value}%`} />
      <Tooltip contentStyle={tooltip} cursor={{ fill: '#8e75ed0a' }} formatter={value => [`${Number(value).toFixed(2)}%`, 'Click-through rate']} />
      {baseline !== undefined && <ReferenceLine y={baseline * 100} stroke="#b3a5db" strokeDasharray="4 4" />}
      <Bar dataKey="rate" radius={[5, 5, 0, 0]} isAnimationActive={false}>{data.map((group, index) => <Cell key={group.name} fill={colors[index % colors.length]} />)}</Bar>
    </BarChart>
  </ResponsiveContainer></div>
}

export function ClickDistribution({ clicks, total }: { clicks: number; total: number }) {
  if (!total) return <EmptyState title="No observations">Change the selected filters.</EmptyState>
  const data = [{ name: 'Clicked', value: clicks }, { name: 'Not clicked', value: total - clicks }]
  return <div className="distribution"><div className="donut-wrap"><ResponsiveContainer width="100%" height={210} minWidth={0}>
    <PieChart><Pie data={data} dataKey="value" innerRadius={69} outerRadius={87} paddingAngle={3} stroke="none" startAngle={90} endAngle={-270} isAnimationActive={false}>
      <Cell fill="#7d62ed" /><Cell fill="#e9e4fa" /></Pie><Tooltip contentStyle={tooltip} formatter={value => number(Number(value))} /></PieChart>
  </ResponsiveContainer><div className="donut-center"><strong>{percent(clicks / total)}</strong><span>historical CTR</span></div></div>
    <div className="distribution-legend">{data.map((group, index) => <div key={group.name}><span><i style={{ background: index ? '#e9e4fa' : '#7d62ed' }} />{group.name}</span>
      <strong>{number(group.value)}</strong><small>{percent(group.value / total)}</small></div>)}</div></div>
}

export function MissingTable({ values }: { values: MissingValue[] }) {
  return <div className="table-scroll"><table className="data-table"><thead><tr><th>Column</th><th>Missing</th><th>Present</th><th>Missing rate</th></tr></thead>
    <tbody>{values.map(value => <tr key={value.column}><td><code>{value.column}</code></td><td>{number(value.missing)}</td><td>{number(value.present)}</td>
      <td><div className="rate-cell"><span className="progress"><i style={{ width: `${value.missingRate * 100}%` }} /></span><span>{percent(value.missingRate)}</span></div></td></tr>)}</tbody></table></div>
}
