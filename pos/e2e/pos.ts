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

/** Телефон мен құпиясөз арқылы кіру (тілге тәуелсіз: өрістерді түрі бойынша табамыз). Кіру минутына 10 рет қана — тілге бір рет. */
export async function posLogin(page: Page) {
  await page.goto('/login')
  await page.locator('input[inputmode=tel]').fill(OWNER.phone)
  await page.locator('input[type=password]').fill(OWNER.password)
  await page.locator('button[type=submit]').click()
  await expect(page).not.toHaveURL(/\/login/)
  await page.waitForLoadState('networkidle', { timeout: 5_000 }).catch(() => {})
}

