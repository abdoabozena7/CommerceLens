import { ArrowUpRight, BarChart3, ChevronDown, ChevronRight, Database, FlaskConical, LayoutDashboard, Menu, Moon, Radio, ShoppingBag, Sun, X } from 'lucide-react'
import { useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useResource } from '../services/useResource'

const navigation = [
  { path: '/', name: 'Overview', icon: LayoutDashboard },
  { path: '/commerce', name: 'Sales analytics', icon: ShoppingBag },
  { path: '/advertising', name: 'Advertising analytics', icon: BarChart3 },
  { path: '/explorer', name: 'Dataset explorer', icon: Database },
  { path: '/monitoring', name: 'Product monitoring', icon: Radio },
  { path: '/predictions', name: 'Prediction Lab', icon: FlaskConical },
]

export function DashboardShell() {
  const [mobile, setMobile] = useState(false)
  const [dark, setDark] = useState(false)
  const location = useLocation()
  const health = useResource<{ status: string }>('/health')
  const title = navigation.find(item => item.path === location.pathname)?.name ?? 'Intelligence'
  return <div className="dashboard-shell" data-theme={dark ? 'dark' : 'light'}>
    {mobile && <button className="sidebar-backdrop" aria-label="Close navigation" onClick={() => setMobile(false)} />}
    <aside className={`sidebar ${mobile ? 'mobile-open' : ''}`}>
      <Link to="/" className="brand"><span className="brand-mark">C<span /></span>CommerceLens</Link>
      <div className="workspace-switch"><span className="workspace-icon">CL</span><div><strong>Demo workspace</strong><small>Local development</small></div><ChevronDown size={14} /></div>
      <div className="nav-heading">WORKSPACE</div>
      <nav aria-label="Dashboard">{navigation.map(item => <NavLink key={item.path} to={item.path} end={item.path === '/'} onClick={() => setMobile(false)}
        className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}><item.icon size={17} strokeWidth={1.7} />{item.name}</NavLink>)}</nav>
      <div className="sidebar-bottom"><div className="pipeline-promo"><span className="promo-icon"><Radio size={20} /></span><h3>One connected story.</h3>
        <p>From historical clicks to local commerce. Every source, clearly labeled.</p><Link to="/explorer">Explore your data <ChevronRight size={14} /></Link></div>
        <Link className="store-link" to="/store"><ShoppingBag size={16} />Visit demo storefront<ArrowUpRight size={15} /></Link>
        <div className="sidebar-footer"><span className={`status-dot ${health.error ? 'offline' : ''}`} />{health.loading ? 'Connecting to API' : health.error ? 'API unavailable' : 'Local API connected'}<span>v1.0</span></div></div>
    </aside>
    <div className="dashboard-body"><header className="topbar"><div className="breadcrumb"><button className="icon-button mobile-menu" aria-label={mobile ? 'Close navigation' : 'Open navigation'} onClick={() => setMobile(!mobile)}>{mobile ? <X size={18} /> : <Menu size={18} />}</button>
      <span>Workspace</span><ChevronRight size={12} /><strong>{title}</strong></div>
      <div className="topbar-actions"><span className="local-badge"><span className="status-dot" />LOCAL WORKSPACE</span>
        <button className="icon-button theme-toggle" aria-label={`Switch to ${dark ? 'light' : 'dark'} theme`} onClick={() => setDark(!dark)}>{dark ? <Sun size={16} /> : <Moon size={16} />}</button>
        <div className="avatar" aria-label="CommerceLens workspace">CL</div></div></header>
      <main className="dashboard-main"><Outlet /></main>
      <footer className="dashboard-footer"><span>CommerceLens <span className="footer-divider">/</span> Commerce, in focus.</span><span>Local data. Transparent sources.</span></footer>
    </div>
  </div>
}
