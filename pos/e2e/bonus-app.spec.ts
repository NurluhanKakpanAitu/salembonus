import { expect, request, test, type Page } from '@playwright/test'
import { expectNothingClipped } from './audit'

/**
 * SalemBonus клиент қосымшасы (frontend/, телефон экраны): әр бет пен профиль формасы екі тілде толық көрінеді.
 * Кіру — демо клиент (DbSeeder.cs), Development-тегі SMS кодымен (ReturnCodeInResponse).
 */
const APP = 'http://localhost:5173'
const DEMO_PHONE = '+77011234567'
type Tokens = { accessToken: string; refreshToken: string }

let tokens: Tokens
let storeId: string
test.beforeAll(async () => {
  const api = await request.newContext({ baseURL: 'http://localhost:5113' })
  const code = await api.post('/api/auth/request-code', { data: { phone: DEMO_PHONE } })
  expect(code.ok(), await code.text()).toBe(true)
  const { devCode } = await code.json()
  const verify = await api.post('/api/auth/verify', { data: { phone: DEMO_PHONE, code: devCode, device: 'e2e' } })
  expect(verify.ok(), await verify.text()).toBe(true)
  tokens = await verify.json()
  const cards = await api.get('/api/cards', { headers: { Authorization: `Bearer ${tokens.accessToken}` } })
  storeId = (await cards.json())[0].storeId
  await api.dispose()
})

async function open(page: Page, lang: 'ru' | 'kk') {
  await page.addInitScript(([l, t]) => {
    localStorage.setItem('salembonus.language', l)
    localStorage.setItem('salembonus.theme', 'light')
    localStorage.setItem('salembonus-auth', JSON.stringify({ state: { accessToken: t.accessToken, refreshToken: t.refreshToken, profileCompleted: true, pendingPhone: null }, version: 0 }))
  }, [lang, tokens] as const)
}

const PAGES = ['/', '/cards', '/transactions', '/stores', '/notifications', '/profile', '/profile/edit', '/about', '/privacy', '/terms', '/delete-account']

for (const lang of ['ru', 'kk'] as const) {
  test(`bonus app ${lang}: every screen readable`, async ({ page }) => {
    await open(page, lang)
    for (const path of PAGES) {
      await page.goto(`${APP}${path}`)
      await expect(page).not.toHaveURL(/\/login/)
      await expectNothingClipped(page, `bonus ${lang} ${path}`)
    }
    // Карта мен дүкен карточкасы (демо клиенттің бірінші картасы).
    for (const path of [`/cards/${storeId}`, `/stores/${storeId}`]) {
      await page.goto(`${APP}${path}`)
      await expectNothingClipped(page, `bonus ${lang} ${path}`)
    }
  })

  test(`bonus app ${lang}: login screen readable`, async ({ page }) => {
    await page.addInitScript((l) => localStorage.setItem('salembonus.language', l), lang)
    await page.goto(`${APP}/login`)
    await expectNothingClipped(page, `bonus ${lang} /login`)
  })
}
