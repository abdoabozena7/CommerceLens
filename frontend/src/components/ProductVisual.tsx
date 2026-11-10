import { Backpack, BottleWine, Coffee, Headphones, Keyboard, LampDesk, Layers, LayoutGrid } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type { Product } from '../services/types'

const visuals: Record<string, LucideIcon> = {
  headphones: Headphones, lamp: LampDesk, backpack: Backpack, mug: Coffee,
  keyboard: Keyboard, organizer: LayoutGrid, bottle: BottleWine, throw: Layers,
}

export function ProductVisual({ product }: { product: Product }) {
  const Visual = visuals[product.visual] ?? LayoutGrid
  return <div className={`product-visual ${product.accent}`} aria-hidden="true">
    <div className="visual-orbit" /><Visual strokeWidth={1.1} size={86} /></div>
}
