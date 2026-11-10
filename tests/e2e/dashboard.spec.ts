import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import path from 'node:path'
import type { AdAnalytics, ModelStatus, Overview, Prediction } from '../../frontend/src/services/types.ts'
import { getApi } from './api.ts'

async function assertReadableNotes(page: Page) {
  const contrast = await page.locator('.kpi-card p').first().evaluate(element => {
    const card = element.closest('.kpi-card')
    if (!card) throw new Error('KPI card is missing.')
    const luminance = (color: string) => {
      const rgb = color.match(/[\d.]+/g)
      if (!rgb || rgb.length < 3) throw new Error(`Unsupported computed color: ${color}`)
      const [r, g, b] = rgb.slice(0, 3).map(Number).map(value => value / 255)
        .map(value => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4)
      return r * 0.2126 + g * 0.7152 + b * 0.0722
    }
    const text = luminance(getComputedStyle(element).color)
    const background = luminance(getComputedStyle(card).backgroundColor)
    return (Math.max(text, background) + 0.05) / (Math.min(text, background) + 0.05)
  })
  expect(contrast, 'KPI explanatory text contrast').toBeGreaterThanOrEqual(4.5)
}

test('overview renders exact historical counts from the live .NET API', async ({ page, request }, info) => {
  const errors: string[] = []
  page.on('pageerror', error => errors.push(error.message))
  const overview = await getApi<Overview>(request, '/analytics/overview')
  expect(overview.totalObservations).toBe(10_000)
  expect(overview.totalHistoricalClicks).toBe(6_500)
  expect(overview.historicalCtr).toBe(0.65)
  await page.goto('/')
  await expect(page.getByTestId('observations-kpi')).toHaveText('10,000')
  await expect(page.getByTestId('ctr-kpi')).toHaveText('65.0%')
  await expect(page.getByRole('navigation', { name: 'Dashboard' }).getByRole('link')).toHaveText([
    'Overview', 'Sales analytics', 'Advertising analytics', 'Dataset explorer', 'Product monitoring', 'Prediction Lab',
  ])
  await expect(page.getByText('6,500', { exact: true })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Pipeline health' })).toBeVisible()
  await expect(page.locator('.recharts-surface').first()).toBeVisible()
  await expect(page.locator('.recharts-pie .recharts-sector')).toHaveCount(2)
  expect(errors).toEqual([])
  await page.screenshot({ path: info.outputPath('dashboard-desktop.png'), fullPage: true })
})

test('advertising combines filters and safely analyses missing categories', async ({ page, request }) => {
  await page.goto('/advertising')
  await page.getByLabel('Device type', { exact: true }).selectOption('Mobile')
  await page.getByLabel('Ad position', { exact: true }).selectOption('Top')
  const expected = await getApi<AdAnalytics>(request, '/analytics/advertising?deviceType=Mobile&adPosition=Top')
  await expect(page.locator('.kpi-card').filter({ hasText: 'Filtered observations' }).locator('.kpi-value'))
    .toHaveText(expected.totalObservations.toLocaleString('en-US'))
  await expect(page.locator('.kpi-card').filter({ hasText: 'Click-through rate' }).locator('.kpi-value'))
    .toHaveText(`${(expected.ctr * 100).toFixed(1)}%`)
  await page.getByRole('button', { name: 'Reset filters' }).click()
  await page.getByLabel('Device type', { exact: true }).selectOption('Unknown')
  await expect(page.locator('.kpi-card').filter({ hasText: 'Filtered observations' }).locator('.kpi-value')).toHaveText('2,000')
  await expect(page.getByText('Categorical context, not a chronological trend.')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Missing-value analysis' })).toBeVisible()
})

test('explorer paginates, searches, sorts, rejects invalid CSV and prevents re-import duplication', async ({ page, request }) => {
  await page.goto('/explorer')
  const rows = page.locator('.explorer-table tbody tr')
  await expect(rows).toHaveCount(25)
  await expect(rows.first().locator('td').first()).toHaveText('#1')
  await page.getByRole('button', { name: 'Next page' }).click()
  await expect(rows.first().locator('td').first()).toHaveText('#26')
  await page.getByRole('button', { name: 'Original user ID' }).click()
  await expect(page.getByText('Page 1 of 400')).toBeVisible()
  await page.getByLabel('Search dataset').fill('Shopping')
  await expect(rows.first()).toContainText('Shopping')
  await page.getByLabel('Choose CSV file').setInputFiles(path.resolve('dataset', 'ad_click_dataset.csv'))
  await page.getByRole('button', { name: 'Import CSV', exact: true }).click()
  await expect(page.getByText('Already imported. No observations were duplicated.')).toBeVisible()
  await page.getByLabel('Choose CSV file').setInputFiles({
    name: 'invalid.csv', mimeType: 'text/csv', buffer: Buffer.from('id,click\n1,2'),
  })
  await page.getByRole('button', { name: 'Import CSV', exact: true }).click()
  await expect(page.getByText('Import rejected. No observations were saved.')).toBeVisible()
  await expect(page.getByText('Rejected', { exact: true })).toBeVisible()
  const actual = await getApi<Overview>(request, '/analytics/overview')
  expect(actual.totalObservations).toBe(10_000)
  expect(actual.totalHistoricalClicks).toBe(6_500)
})

test('commerce has honest empty states and uses only actual order dates', async ({ page }) => {
  await page.goto('/commerce')
  await page.getByLabel('Orders from').fill('2099-01-01')
  await expect(page.getByRole('heading', { name: 'No orders in this period' })).toBeVisible()
  await expect(page.locator('.kpi-card').filter({ has: page.getByText('Order revenue', { exact: true }) }).locator('.kpi-value')).toHaveText('$0.00')
  await expect(page.getByText('No real money is charged.', { exact: false })).toBeVisible()
})

test('Prediction Lab shows an actual probability, baseline and user-disjoint evaluation', async ({ page, request }) => {
  const status = await getApi<ModelStatus>(request, '/model/status')
  expect(status.metrics?.userOverlap).toBe(0)
  expect(status.metrics!.trainRows + status.metrics!.testRows).toBe(10_000)
  await page.goto('/predictions')
  for (const label of ['Predict device type', 'Predict ad position', 'Predict browsing history', 'Predict time of day'])
    await page.getByLabel(label, { exact: true }).selectOption('Unknown')
  const responsePromise = page.waitForResponse(response => response.url().endsWith('/api/predictions') && response.request().method() === 'POST')
  await page.getByRole('button', { name: 'Predict click probability' }).click()
  const response = await responsePromise
  expect(response.ok()).toBe(true)
  const prediction: Prediction = await response.json()
  expect(prediction.probability).toBeGreaterThanOrEqual(0)
  expect(prediction.probability).toBeLessThanOrEqual(1)
  expect(prediction.engine).toBe('ML.NET')
  expect(status.metrics!.truePositives + status.metrics!.falsePositives
    + status.metrics!.trueNegatives + status.metrics!.falseNegatives).toBe(status.metrics!.testRows)
  await expect(page.getByTestId('prediction-probability')).toHaveText(`${(prediction.probability * 100).toFixed(1)}%`)
  await expect(page.getByText('0 overlapping users.', { exact: true })).toBeVisible()
  await expect(page.getByRole('table', { name: 'Held-out confusion matrix (threshold 0.5)' })).toBeVisible()
  expect(Object.keys(response.request().postDataJSON()).sort()).toEqual(['adPosition', 'browsingHistory', 'deviceType', 'timeOfDay'])
  const retrained = page.waitForResponse(response => response.url().endsWith('/api/model/train'))
  await page.getByRole('button', { name: 'Retrain model' }).click()
  expect((await retrained).ok()).toBe(true)
  await expect(page.getByRole('button', { name: 'Retrain model' })).toBeEnabled()
})

test('API failures are visible and retry reloads real results', async ({ page }) => {
  await page.route('**/api/analytics/overview', route => route.fulfill({
    status: 503, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Intentional API failure test' }),
  }))
  await page.goto('/')
  await expect(page.getByRole('alert')).toContainText('Intentional API failure test')
  await page.unroute('**/api/analytics/overview')
  await page.getByRole('button', { name: 'Try again' }).click()
  await expect(page.getByTestId('observations-kpi')).toHaveText('10,000')
})

test('dashboard supports dark mode and a mobile sidebar without page overflow', async ({ page }, info) => {
  await page.goto('/')
  await expect(page.getByTestId('observations-kpi')).toHaveText('10,000')
  await assertReadableNotes(page)
  await page.getByRole('button', { name: 'Switch to dark theme' }).click()
  await expect(page.locator('.dashboard-shell')).toHaveAttribute('data-theme', 'dark')
  await assertReadableNotes(page)
  await page.getByRole('button', { name: 'Switch to light theme' }).click()
  await page.setViewportSize({ width: 390, height: 844 })
  await page.getByRole('button', { name: 'Open navigation' }).click()
  await page.getByRole('link', { name: 'Advertising analytics', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Advertising analytics', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Open navigation' }).click()
  await page.getByRole('link', { name: 'Overview', exact: true }).click()
  await expect(page.getByTestId('observations-kpi')).toHaveText('10,000')
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
  await page.screenshot({ path: info.outputPath('dashboard-mobile.png'), fullPage: true })
})
