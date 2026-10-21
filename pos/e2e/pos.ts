import { expect, type Page } from '@playwright/test'

/** Демо иесі — тек Development пен тестте себіледі (StaffSeeder.cs). */
export const OWNER = { phone: '+77000000001', password: 'Demo-Owner-2026' }

/**
 * Интерфейс тілі. Кіргеннен кейін қосымша сервердегі қызметкер тілін қолданады (me.language),
 * сондықтан серверді өзгертпей, auth жауаптарындағы тілді тест ішінде ауыстырамыз.
 */
export async function useLanguage(page: Page, lang: 'ru' | 'kk') {
  await page.addInitScript((l) => { try { localStorage.setItem('salempos.language', l) } catch { /* жеке режим */ } }, lang)
  await page.route(/\/api\/staff\/v1\/auth\/(login|refresh|me)$/, async (route) => {
    const res = await route.fetch()
    const body = await res.json().catch(() => null)
    if (body?.me) body.me.language = lang
    else if (body && typeof body.language === 'string') body.language = lang
    await route.fulfill({ response: res, json: body })
  })
}

/**
 * Телефон мен құпиясөз арқылы кіру (тілге тәуелсіз: өрістерді түрі бойынша табамыз). Кіру минутына 10 рет қана — тілге бір рет.
 * Кіру жауабындағы access token қайтарылады — тест деректерін API арқылы дайындау үшін.
 */
export async function posLogin(page: Page): Promise<string> {
  await page.goto('/login')
  await page.locator('input[inputmode=tel]').fill(OWNER.phone)
  await page.locator('input[type=password]').fill(OWNER.password)
  const login = page.waitForResponse((r) => new URL(r.url()).pathname.endsWith('/api/staff/v1/auth/login'))
  await page.locator('button[type=submit]').click()
  const { accessToken } = (await (await login).json()) as { accessToken: string }
  await expect(page).not.toHaveURL(/\/login/)
  await page.waitForLoadState('networkidle', { timeout: 5_000 }).catch(() => {})
  return accessToken
}

/** Демо дүкен (StaffSeeder.DemoStoreId). */
const DEMO_STORE = '11111111-1111-1111-1111-111111111111'

/**
 * Таза базада (CI) каталогта тауар жоқ — касса, қойма, тауар беттері бос болып, тексерілмей қалады.
 * Тауар жоқ болса ғана бағасы мен қалдығы бар екі демо тауар қосамыз (жергілікті дерекке тимейді).
 */
export async function ensureDemoProducts(page: Page, token: string) {
  const headers = { Authorization: `Bearer ${token}`, 'X-Store-Id': DEMO_STORE, 'Accept-Language': 'ru' }
  const catalog = await page.request.get('/api/pos/v1/cashier/catalog', { headers })
  expect(catalog.ok(), await catalog.text()).toBe(true)
  if (((await catalog.json()) as { items: unknown[] }).items.length > 0) return
  const units = (await (await page.request.get('/api/pos/v1/catalog/units', { headers })).json()) as { id: string }[]
  for (const [name, price, purchase] of [['Масло моторное 5W-40, 4 л', 12_400, 9_000], ['Фильтр масляный', 2_600, 1_700]] as const) {
    const res = await page.request.post('/api/pos/v1/products', {
      headers, data: { name, unitId: units[0].id, opening: { quantity: 20, purchasePrice: purchase, salePrice: price } },
    })
    expect(res.ok(), await res.text()).toBe(true)
  }
}

