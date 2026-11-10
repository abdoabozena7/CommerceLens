import { ArrowLeft, ArrowRight, Check, Search, ShoppingBag, Sparkles } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ProductVisual } from '../components/ProductVisual'
import { EmptyState, ErrorState, Loading } from '../components/Shared'
import { useCart } from '../services/CartContext'
import { errorMessage, money } from '../services/api'
import type { Product } from '../services/types'
import { useResource } from '../services/useResource'
import { useStoreTracking } from '../services/useStoreTracking'

function PublicAd({ product, tracking, visit, onError }: {
  product: Product; tracking: ReturnType<typeof useStoreTracking>
  visit: () => void; onError: (message: string) => void
}) {
  const button = useRef<HTMLButtonElement>(null)
  const { enabled, track } = tracking
  useEffect(() => {
    if (!enabled || !button.current) return
    const observer = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.intersectionRatio >= 0.5) && !document.hidden)
        void track(product.id, 'AdImpression').catch(error => onError(`Ad impression tracking failed: ${errorMessage(error)}`))
    }, { threshold: 0.5 })
    const observe = () => {
      observer.disconnect()
      if (!document.hidden && button.current) observer.observe(button.current)
    }
    observe()
    document.addEventListener('visibilitychange', observe)
    return () => { observer.disconnect(); document.removeEventListener('visibilitychange', observe) }
  }, [enabled, track, product.id, onError])
  return <button ref={button} className="public-ad" data-testid="ad-text" onClick={visit}>
    <Sparkles size={13} />Featured pick - explore this offer<ArrowRight size={13} /></button>
}

export function Storefront() {
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState('')
  const [sort, setSort] = useState('name')
  const [error, setError] = useState('')
  const products = useResource<Product[]>(`/products?${new URLSearchParams({ search, category, sort })}`)
  const categories = useResource<Product[]>('/products')
  const navigate = useNavigate()
  const cart = useCart()
  const tracking = useStoreTracking()
  async function visit(product: Product, eventType: 'ProductView' | 'AdClick') {
    setError('')
    try {
      if (eventType === 'AdClick') {
        await tracking.track(product.id, 'AdImpression')
        await tracking.track(product.id, 'AdClick')
      }
      navigate(`/store/products/${product.id}`)
    }
    catch (error) { setError(errorMessage(error)) }
  }
  return <div className="store-page">
    <header className="store-header"><Link className="brand" to="/"><span className="brand-mark">C<span /></span>CommerceLens<span className="store-label">STORE</span></Link>
      <div className="store-nav"><Link to="/">Analytics Dashboard <ArrowRight size={15} /></Link>
        <button className="button secondary" onClick={() => cart.setOpen(true)}><ShoppingBag size={17} />Cart <span className="cart-count">{cart.lines.reduce((sum, line) => sum + line.quantity, 0)}</span></button></div></header>
    <section className="store-hero"><div><span className="eyebrow"><Sparkles size={14} /> THE EVERYDAY EDIT</span>
      <h1>Thoughtfully chosen.<br /><span>Simply better.</span></h1>
      <p>A small collection for work, home, and everything in between.<br />A real storefront powering your analytics playground.</p>
      <a className="button primary" href="#catalog">Explore the collection <ArrowRight size={17} /></a></div>
      <div className="hero-art" aria-hidden="true"><div className="hero-art-circle" /><ShoppingBag strokeWidth={0.8} size={155} /><span className="floating-label"><Check size={15} /> Built for the everyday</span></div></section>
    <div className="notice"><span className="badge lilac">DEMO CATALOG</span><span>Seeded products. Real local database records. Simulated checkout. No real payments.</span></div>
    <section id="catalog"><div className="section-title"><div><h2>The collection</h2><p className="muted">Good things, without the noise.</p></div>
      <label className="select-inline">Sort by<select aria-label="Sort products" value={sort} onChange={event => setSort(event.target.value)}><option value="name">Name</option><option value="priceAsc">Price: low to high</option><option value="priceDesc">Price: high to low</option></select></label></div>
      <div className="catalog-controls"><div className="tabs"><button className={!category ? 'active' : ''} onClick={() => setCategory('')}>All products</button>
        {[...new Set(categories.data?.map(product => product.category))].sort().map(value => <button key={value} className={category === value ? 'active' : ''} onClick={() => setCategory(value)}>{value}</button>)}</div>
        <label className="search-field"><Search size={16} /><input aria-label="Search products" placeholder="Search the collection..." value={search} onChange={event => setSearch(event.target.value)} /></label></div>
      {error && <ErrorState message={error} />}
      {categories.error && <ErrorState message={categories.error} retry={categories.refresh} />}
      {products.error && <ErrorState message={products.error} retry={products.refresh} />}
      {products.loading && <Loading />}
      {products.data?.length === 0 && <EmptyState title="No products found">Try another search or category.</EmptyState>}
      {products.data && <div className="product-grid" data-testid="catalog-ready">{products.data.map(product => <article className="product-card" key={product.id} data-testid="product-card" data-product-id={product.id}>
        <ProductVisual product={product} />
        <div className="product-copy"><div className="product-meta"><span data-testid="product-category">{product.category}</span><span data-testid="product-availability" className={product.stock ? 'stock' : 'muted'}>{product.stock ? 'In stock' : 'Sold out'}</span></div>
          <h3 data-testid="product-name">{product.name}</h3><div className="product-bottom"><strong data-testid="product-price">{money(product.price)}</strong>
            <button className="text-button" onClick={() => void visit(product, 'ProductView')}>View details <ArrowRight size={14} /></button></div>
          {product.featured && <PublicAd product={product} tracking={tracking} visit={() => void visit(product, 'AdClick')} onError={setError} />}
          <button className="button secondary wide" disabled={!product.stock} onClick={() => cart.add(product)}><ShoppingBag size={15} />Add to cart</button></div>
      </article>)}</div>}
    </section>
    <footer className="store-footer"><span>CommerceLens / A connected commerce demo</span><Link to="/">See the story behind the data <ArrowRight size={15} /></Link></footer>
  </div>
}

export function ProductDetails() {
  const { id } = useParams()
  const product = useResource<Product>(`/products/${id}`)
  const cart = useCart()
  const { track } = useStoreTracking()
  const [trackingError, setTrackingError] = useState('')
  const productId = product.data?.id
  useEffect(() => {
    if (productId) void track(productId, 'ProductView').catch(error => setTrackingError(errorMessage(error)))
  }, [productId, track])
  return <div className="store-page detail-page"><Link to="/store" className="back-link"><ArrowLeft size={17} />Back to the collection</Link>
    {trackingError && <ErrorState message={`Product view tracking failed: ${trackingError}`} retry={() => {
      if (productId) void track(productId, 'ProductView').then(() => setTrackingError(''), error => setTrackingError(errorMessage(error)))
    }} />}
    {product.loading && <Loading />}{product.error && <ErrorState message={product.error} retry={product.refresh} />}
    {product.data && <div className="product-detail"><ProductVisual product={product.data} /><div>
      <span className="badge lilac">{product.data.category}</span><h1>{product.data.name}</h1>
      <strong className="detail-price">{money(product.data.price)}</strong><p>{product.data.description}</p>
      <p className="stock">{product.data.stock} available in the local demo catalog</p>
      <button className="button primary" disabled={!product.data.stock} onClick={() => cart.add(product.data!)}><ShoppingBag size={17} />Add to cart</button>
      <p className="muted">Demo product. Checkout saves an order locally; no payment is taken.</p></div></div>}
  </div>
}
