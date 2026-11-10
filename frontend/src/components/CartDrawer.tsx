import { CheckCircle2, Minus, Plus, ShoppingBag, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useCart } from '../services/CartContext'
import { errorMessage, money, post } from '../services/api'
import type { Order } from '../services/types'
import { EmptyState, ErrorState } from './Shared'

export function CartDrawer() {
  const cart = useCart()
  const dialog = useRef<HTMLDialogElement>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [order, setOrder] = useState<Order>()
  useEffect(() => {
    if (cart.open) dialog.current?.showModal()
    else dialog.current?.close()
  }, [cart.open])
  async function checkout() {
    setBusy(true); setError('')
    try {
      const placed = await post<Order>('/orders', {
        items: cart.lines.map(line => ({ productId: line.product.id, quantity: line.quantity })),
        requestKey: cart.requestKey(),
      })
      setOrder(placed); cart.clear()
    } catch (error) { setError(errorMessage(error)) }
    finally { setBusy(false) }
  }
  return <dialog ref={dialog} className="cart-dialog" aria-label="Shopping cart"
    onCancel={() => cart.setOpen(false)}>
    <div className="drawer-heading"><h2><ShoppingBag size={21} /> Your cart</h2>
      <button className="icon-button" aria-label="Close cart" disabled={busy} onClick={() => cart.setOpen(false)}><X size={21} /></button></div>
    <p className="muted">Demo checkout only. No payment or personal information.</p>
    {error && <ErrorState message={error} />}
    {cart.lines.length > 0 ? <>
      <div className="cart-lines">{cart.lines.map(({ product, quantity }) => <div className="cart-line" key={product.id}>
        <div><h3>{product.name}</h3><span className="muted">{money(product.price)} each</span></div>
        <div className="quantity"><button aria-label={`Remove one ${product.name}`} disabled={busy} onClick={() => cart.update(product.id, quantity - 1)}><Minus size={14} /></button>
          <span>{quantity}</span><button aria-label={`Add one ${product.name}`} disabled={busy || quantity >= Math.min(product.stock, 25)} onClick={() => cart.update(product.id, quantity + 1)}><Plus size={14} /></button></div>
      </div>)}</div>
      <div className="cart-total"><span>Total</span><strong>{money(cart.lines.reduce((sum, line) => sum + line.product.price * line.quantity, 0))}</strong></div>
      <button className="button primary wide" disabled={busy} onClick={() => void checkout()}>{busy ? 'Creating order...' : 'Place demo order'}</button>
    </> : order ? <div className="order-success" data-testid="order-confirmation"><CheckCircle2 size={42} />
      <h3>Order #{order.id} is saved</h3><p>{money(order.total)} in local demo orders. Your dashboard has been refreshed.</p>
      <span className="badge mint">LocalDemoCheckout</span></div>
      : <EmptyState title="A little empty in here">Find something you love in the demo catalog.</EmptyState>}
  </dialog>
}
