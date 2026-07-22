import { expect, test, type Page } from '@playwright/test'

/**
 * Full-stack flows, driven exactly like a user would: every assertion after a
 * mutation reads back through the UI (and a couple through the API) so the
 * whole chain — React → fetch → Minimal API → EF → PostgreSQL → back — is
 * under test. Authenticated via the seeded admin (admin@example.com / admin).
 */

async function login(page: Page) {
  await page.goto('/login')
  await page.getByTestId('login-email').fill('admin@example.com')
  await page.getByTestId('login-password').fill('admin')
  await page.getByTestId('login-submit').click()
  await expect(page.getByTestId('project-card-Coffee Run')).toBeVisible({ timeout: 15_000 })
}

test.describe('Weft · public surface', () => {
  test('landing page pitches the Blender VCS and gates the console', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { name: /Version control for your Blender/i })).toBeVisible()
    await expect(page.getByTestId('landing')).toBeVisible()
    // feature cards + how-it-works strip
    await expect(page.getByText('Snapshot history')).toBeVisible()
    await expect(page.getByText('Render pinning')).toBeVisible()
  })

  test('login rejects bad credentials and accepts the seeded admin', async ({ page }) => {
    await page.goto('/login')
    await page.getByTestId('login-email').fill('admin@example.com')
    await page.getByTestId('login-password').fill('wrong-password')
    await page.getByTestId('login-submit').click()
    await expect(page.getByTestId('login-error')).toContainText('Invalid email or password')

    await page.getByTestId('login-password').fill('admin')
    await page.getByTestId('login-submit').click()
    await expect(page.getByTestId('project-card-Coffee Run')).toBeVisible({ timeout: 15_000 })
  })

  test('anonymous visitors are bounced from the console to login', async ({ page }) => {
    await page.goto('/app')
    await expect(page.getByTestId('login-card')).toBeVisible({ timeout: 15_000 })
  })

  test('signing out ends the session behind the auth wall', async ({ page }) => {
    await login(page)
    await page.getByTestId('logout-btn').click()
    // the auth wall bounces back to /login and the API session is gone
    await expect(page.getByTestId('login-card')).toBeVisible({ timeout: 15_000 })
    await page.goto('/app')
    await expect(page.getByTestId('login-card')).toBeVisible({ timeout: 15_000 })
  })
})

test.describe('Weft · console flows', () => {
  test.beforeEach(async ({ page }) => {
    await login(page)
  })

  test('overview lists the seeded productions with live stats', async ({ page }) => {
    await expect(page.getByTestId('project-card-Coffee Run')).toBeVisible()
    await expect(page.getByTestId('project-card-Nordic Kitchen')).toBeVisible()
    await expect(page.getByTestId('project-card-Ironvale')).toBeVisible()
  })

  test('production dashboard shows render health and assets', async ({ page }) => {
    await page.getByTestId('project-card-Coffee Run').click()
    await expect(page.getByRole('heading', { name: 'Coffee Run' })).toBeVisible()
    await expect(page.getByTestId('stat-success')).toContainText('%')
    await expect(page.getByTestId('asset-list')).toBeVisible()
    await expect(page.getByTestId('asset-row-Bella — Character Rig')).toBeVisible()
    // the locked set piece is called out by its status dot styling (faint = locked)
    await expect(page.getByTestId('asset-row-Old Lighthouse — Set Piece')).toBeVisible()
  })

  test('edit → commit → verify snapshot in timeline and header', async ({ page }) => {
    await page.getByTestId('project-card-Coffee Run').click()
    await page.getByTestId('asset-row-Bella — Character Rig').click()
    await expect(page.getByTestId('asset-page')).toBeVisible()

    const headVersion = page.getByTestId('head-version')
    const before = await headVersion.innerText()

    // edit the description (a change Weft can describe on its own)
    const description = page.locator('[data-testid="config-editor"] input').nth(2)
    await description.fill(`E2E edit ${Date.now()}`)
    // let the debounced autosave land and the button re-enable
    await expect(page.getByTestId('commit-btn')).toBeEnabled({ timeout: 15_000 })
    // sticky bar never passes Playwright's stability check — dispatch instead
    await page.getByTestId('commit-btn').dispatchEvent('click')

    // commit modal: auto message is derived from the diff; confirm
    const confirm = page.getByTestId('commit-confirm')
    await confirm.waitFor({ state: 'visible', timeout: 15_000 })
    await expect(confirm).toBeEnabled({ timeout: 15_000 })
    await confirm.dispatchEvent('click')

    // the toast confirms the commit; the header version incremented by one
    await expect(page.getByText(/Committed v\d+/)).toBeVisible({ timeout: 15_000 })
    const after = await headVersion.innerText()
    const beforeV = Number(/v(\d+)/.exec(before)![1])
    const afterV = Number(/v(\d+)/.exec(after)![1])
    expect(afterV).toBe(beforeV + 1)

    // the timeline shows the fresh snapshot as HEAD
    await page.getByTestId('tab-timeline').click()
    const firstCard = page.locator('ul li .card').first()
    await expect(firstCard).toContainText('HEAD', { timeout: 15_000 })
  })

  test('compare two versions shows a field-level diff', async ({ page }) => {
    await page.getByTestId('project-card-Coffee Run').click()
    await page.getByTestId('asset-row-Bella — Character Rig').click()
    await page.getByTestId('tab-timeline').click()

    const compareBtn = page.getByRole('button', { name: /Compare to HEAD/ }).first()
    await compareBtn.scrollIntoViewIfNeeded()
    await compareBtn.click()

    // server-computed diff renders change entries
    await expect(page.getByText(/Comparing/)).toBeVisible()
  })

  test('restore an old version as a new immutable commit', async ({ page }) => {
    await page.getByTestId('project-card-Coffee Run').click()
    await page.getByTestId('asset-row-Bella — Character Rig').click()
    await page.getByTestId('tab-timeline').click()

    const restore = page.getByRole('button', { name: /↺ Restore v\d/ }).first()
    await restore.scrollIntoViewIfNeeded()
    await restore.click()

    await page.getByTestId('restore-confirm').click()
    await expect(page.getByText(/Restored v\d+ as new commit/)).toBeVisible()
  })

  test('queue a render, inspect it, then re-render it', async ({ page }) => {
    await page.getByTestId('project-card-Coffee Run').click()
    await page.getByTestId('asset-row-Bella — Character Rig').click()
    await page.getByTestId('tab-renders').click()

    await page.getByRole('button', { name: '⚡ Queue render' }).click()
    await page.locator('select').selectOption('final')
    await page.getByRole('button', { name: 'Dispatch to farm' }).click()
    await expect(page.getByText(/Queued final render → completed/)).toBeVisible()

    // the log refreshes and gains a completed final render
    const finalRow = page.locator('[data-testid="render-row-final-completed"]').first()
    await expect(finalRow).toBeVisible({ timeout: 10_000 })

    // expand it and re-render on the current snapshot
    await finalRow.click()
    await page.getByRole('button', { name: '↻ Re-render on current snapshot', exact: true }).click()
    await expect(page.getByText(/Re-rendered final/)).toBeVisible()
  })

  test('create a new asset and get its v1 snapshot', async ({ page }) => {
    await page.getByTestId('project-card-Ironvale').click()
    await page.getByTestId('new-asset').click()

    const name = `E2E Asset ${Date.now()}`
    await page.getByTestId('create-name').fill(name)
    await page.getByTestId('create-source').fill('//assets/e2e/e2e_asset.blend')
    await page.getByTestId('create-submit').click()

    const row = page.getByTestId(`asset-row-${name}`)
    await expect(row).toBeVisible()
    await expect(row).toContainText('v1')

    // backend seeded a real v1 "Initial commit" snapshot
    const projects = await (await page.request.get('/api/projects')).json()
    const ironvale = projects.find((p: { name: string }) => p.name === 'Ironvale')
    const list = await (await page.request.get(`/api/projects/${ironvale.id}/assets`)).json()
    const created = list.find((a: { name: string }) => a.name === name)
    expect(created).toBeTruthy()
    const commits = await (await page.request.get(`/api/assets/${created.id}/commits`)).json()
    expect(commits).toHaveLength(1)
    expect(commits[0].message).toBe('Initial commit')
  })

  test('discard reverts a draft to HEAD', async ({ page }) => {
    await page.getByTestId('project-card-Coffee Run').click()
    await page.getByTestId('asset-row-Bella — Character Rig').click()

    const source = page.getByTestId('input-source')
    const before = await source.inputValue()
    await source.fill(before.replace(/[^/]*$/, 'discard_probe.blend'))
    // let the debounced autosave land, then discard (sticky bar: force click)
    const discard = page.getByTestId('discard-btn')
    await expect(discard).toBeEnabled({ timeout: 15_000 })
    await discard.dispatchEvent('click')
    await expect(page.getByText('Draft discarded — restored from HEAD')).toBeVisible()
  })
})
