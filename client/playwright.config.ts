import { defineConfig } from '@playwright/test'

/**
 * E2E runs against the real stack: Vite dev server (this webServer) proxying
 * /api to the Weft API on :5015, which serves the seeded PostgreSQL database.
 * Start the API before running:
 *   cd src/Weft.Api && ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:5015
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
    timeout: 60_000,
  },
  projects: [{ name: 'chromium' }],
})
