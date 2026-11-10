import { useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { CartContext, type CartLine, type CartState } from '../services/CartContext'

export function CartProvider({ children }: { children: ReactNode }) {
  const [lines, setLines] = useState<CartLine[]>([])
  const [open, setOpen] = useState(false)
  const key = useRef(crypto.randomUUID())
  const value: CartState = {
    lines, open, setOpen, requestKey: () => key.current,
    add(product) {
      key.current = crypto.randomUUID()
      setLines(previous => {
        const existing = previous.find(line => line.product.id === product.id)
        if (existing) return previous.map(line => line.product.id === product.id
          ? { ...line, quantity: Math.min(line.quantity + 1, product.stock, 25) } : line)
        return product.stock > 0 ? [...previous, { product, quantity: 1 }] : previous
      })
      setOpen(true)
    },
    update(id, quantity) {
      key.current = crypto.randomUUID()
      setLines(previous => previous.map(line => line.product.id === id
        ? { ...line, quantity: Math.min(quantity, line.product.stock, 25) } : line).filter(line => line.quantity > 0))
    },
    clear() { key.current = crypto.randomUUID(); setLines([]) },
  }
  return <CartContext.Provider value={value}>{children}</CartContext.Provider>
}
