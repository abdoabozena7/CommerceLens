import { createContext, useContext } from 'react'
import type { Product } from './types'

export interface CartLine { product: Product; quantity: number }
export interface CartState {
  lines: CartLine[]; open: boolean; setOpen: (open: boolean) => void; requestKey: () => string
  add: (product: Product) => void; update: (id: number, quantity: number) => void; clear: () => void
}
export const CartContext = createContext<CartState | null>(null)

export function useCart() {
  const value = useContext(CartContext)
  if (!value) throw new Error('CartProvider is required.')
  return value
}
