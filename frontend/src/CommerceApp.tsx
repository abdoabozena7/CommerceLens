import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { CartDrawer } from './components/CartDrawer'
import { CartProvider } from './components/CartProvider'
import { DashboardShell } from './components/DashboardShell'
import { Loading } from './components/Shared'

const Overview = lazy(() => import('./pages/Overview').then(module => ({ default: module.Overview })))
const Advertising = lazy(() => import('./pages/Advertising').then(module => ({ default: module.Advertising })))
const Commerce = lazy(() => import('./pages/Commerce').then(module => ({ default: module.Commerce })))
const DataExplorer = lazy(() => import('./pages/DataExplorer').then(module => ({ default: module.DataExplorer })))
const ProductMonitoring = lazy(() => import('./pages/ProductMonitoring').then(module => ({ default: module.ProductMonitoring })))
const PredictionLab = lazy(() => import('./pages/PredictionLab').then(module => ({ default: module.PredictionLab })))
const Storefront = lazy(() => import('./pages/Storefront').then(module => ({ default: module.Storefront })))
const ProductDetails = lazy(() => import('./pages/Storefront').then(module => ({ default: module.ProductDetails })))

export default function CommerceApp() {
  return <BrowserRouter><CartProvider><Suspense fallback={<Loading />}><Routes>
    <Route element={<DashboardShell />}>
      <Route path="/" element={<Overview />} />
      <Route path="/advertising" element={<Advertising />} />
      <Route path="/commerce" element={<Commerce />} />
      <Route path="/explorer" element={<DataExplorer />} />
      <Route path="/monitoring" element={<ProductMonitoring />} />
      <Route path="/scraper" element={<Navigate to="/monitoring" replace />} />
      <Route path="/predictions" element={<PredictionLab />} />
    </Route>
    <Route path="/store" element={<Storefront />} />
    <Route path="/store/products/:id" element={<ProductDetails />} />
    <Route path="*" element={<Navigate to="/" replace />} />
  </Routes></Suspense><CartDrawer /></CartProvider></BrowserRouter>
}
