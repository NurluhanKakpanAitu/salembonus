import { test } from '@playwright/test'
import { expectNothingClipped } from './audit'

// Лендинг (salemtech.kz): екі тілде де шапка, тарифтер мен өтінім формасы толық көрінеді (компьютер мен телефон).
for (const lang of ['ru', 'kk'] as const) {
  test(`landing ${lang}: everything readable`, async ({ page }) => {
    await page.addInitScript((l) => { try { localStorage.setItem('salemtech.landing.lang', l) } catch { /* жеке режим */ } }, lang)
    await page.goto('/landing.html')
    const toggle = page.getByRole('group', { name: 'Language' }).getByRole('button', { name: lang === 'ru' ? 'RU' : 'ҚАЗ' })
    await toggle.click()
    await expectNothingClipped(page, `landing ${lang}`)
    await page.locator('#request').scrollIntoViewIfNeeded()
    await expectNothingClipped(page, `landing ${lang} form`, '#request')
  })
}
