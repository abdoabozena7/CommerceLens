import { collectStorefront } from './adapter.ts'

try {
  const collection = await collectStorefront(process.argv[2] ?? 'http://127.0.0.1:5173/store')
  process.stdout.write(JSON.stringify(collection))
} catch (error) {
  console.error(`Storefront collection failed: ${error instanceof Error ? error.message : String(error)}`)
  process.exitCode = 1
}
