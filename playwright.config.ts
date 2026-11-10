import { defineConfig, devices } from '@playwright/test'
import { randomUUID } from 'node:crypto'
import { tmpdir } from 'node:os'
import path from 'node:path'

// Playwright loads config in multiple processes; inherit one run identity for servers and cleanup.
const runId = process.env.COMMERCELENS_E2E_RUN_ID ?? randomUUID()
if (!/^[\da-f-]{36}$/i.test(runId)) throw new Error('Invalid end-to-end test run identity.')
process.env.COMMERCELENS_E2E_RUN_ID = runId
const database = path.join(tmpdir(), `commercelens-e2e-${runId}.db`)
const apiOrigin = 'http://127.0.0.1:5081'
const frontendOrigin = 'http://127.0.0.1:5174'

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: frontendOrigin, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 1000 } } }],
  metadata: { database, models: `${database}.models` },
  globalTeardown: './tests/e2e/cleanup.ts',
  webServer: [
    {
      command: `dotnet run --no-launch-profile --no-build --configuration Release --project "backend\\CommerceLens.Api" -- --urls ${apiOrigin}`,
      url: `${apiOrigin}/api/health`,
      timeout: 120_000,
      reuseExistingServer: false,
      env: {
        ConnectionStrings__CommerceLens: `Data Source=${database};Pooling=False`,
        CommerceLens__ModelDirectory: `${database}.models`, CommerceLens__StorefrontUrl: `${frontendOrigin}/store`,
        ASPNETCORE_ENVIRONMENT: 'Development',
      },
    },
    {
      command: 'npm run preview --workspace frontend -- --port 5174',
      url: `${frontendOrigin}/store`,
      timeout: 30_000,
      reuseExistingServer: false,
      env: { COMMERCELENS_API_URL: apiOrigin },
    },
  ],
})
