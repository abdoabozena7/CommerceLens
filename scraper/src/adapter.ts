import { chromium } from 'playwright'
import type { Page } from 'playwright'

export interface CollectedProduct {
  storefrontProductId: number
  name: string
  category: string
  priceCents: number
  availability: string
  adText: string | null
  sourceUrl: string
  extractedAt: string
  status: 'Collected'
}

export function localStorefrontUrl(input: string): URL {
  const url = new URL(input)
  if (url.protocol !== 'http:' || !['127.0.0.1', 'localhost'].includes(url.hostname)
    || url.username || url.password) throw new Error('Only the local HTTP storefront is allowed.')
  const storefront = new URL('/store', url.origin)
  // Collection traffic is explicitly excluded from customer event analytics.
  storefront.searchParams.set('collection', '1')
  return storefront
}

export async function extractRenderedProducts(page: Page): Promise<CollectedProduct[]> {
  await page.getByTestId('catalog-ready').waitFor({ state: 'visible', timeout: 30_000 })
  const cards = page.getByTestId('product-card')
  const count = await cards.count()
  if (count === 0 || count > 1000) throw new Error(`Unexpected rendered catalog size: ${count}.`)
  const records: CollectedProduct[] = []
  for (const card of await cards.all()) {
    await card.scrollIntoViewIfNeeded()
    if (!await card.isVisible()) throw new Error('A product card is not publicly visible.')
    const storefrontProductId = Number(await card.getAttribute('data-product-id'))
    const name = (await card.getByTestId('product-name').innerText()).trim()
    const category = (await card.getByTestId('product-category').innerText()).trim()
    const priceText = (await card.getByTestId('product-price').innerText()).trim()
    const price = /^\$([\d,]+)\.(\d{2})$/.exec(priceText)
    const availability = (await card.getByTestId('product-availability').innerText()).trim()
    if (!Number.isSafeInteger(storefrontProductId) || storefrontProductId < 1 || !name || !category || !price
      || !['In stock', 'Sold out'].includes(availability)) throw new Error(`Invalid rendered product fields for card ${storefrontProductId}.`)
    const priceCents = Number(price[1].replaceAll(',', '')) * 100 + Number(price[2])
    if (!Number.isSafeInteger(priceCents) || priceCents < 0) throw new Error('Invalid rendered price.')
    const ad = card.getByTestId('ad-text')
    records.push({
      storefrontProductId, name, category, priceCents, availability,
      adText: await ad.count() ? (await ad.innerText()).replace(/\s+/g, ' ').trim() : null,
      sourceUrl: page.url(), extractedAt: new Date().toISOString(), status: 'Collected',
    })
  }
  if (new Set(records.map(record => record.storefrontProductId)).size !== records.length)
    throw new Error('The rendered storefront contains duplicate product identities.')
  return records
}

export async function collectStorefront(input: string) {
  const source = localStorefrontUrl(input)
  const browser = await chromium.launch({ headless: true })
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1100 }, locale: 'en-US' })
    await context.route('**/*', async route => {
      const url = new URL(route.request().url())
      if (url.origin === source.origin) await route.continue()
      else await route.abort('blockedbyclient')
    })
    const page = await context.newPage()
    page.setDefaultTimeout(10_000)
    await page.goto(source.href, { waitUntil: 'domcontentloaded', timeout: 30_000 })
    if (new URL(page.url()).origin !== source.origin) throw new Error('The storefront navigated away from its local origin.')
    const records = await extractRenderedProducts(page)
    return { sourceUrl: page.url(), completedAt: new Date().toISOString(), records }
  } finally { await browser.close() }
}
