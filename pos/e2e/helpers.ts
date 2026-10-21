import { expect, type Locator, type Page } from '@playwright/test'
import { expectNothingClipped } from './audit'

/** Әзірлеу/тест демо аккаунттары — тек Development пен тестте себіледі (AdminSeeder.cs, StaffSeeder.cs). */
export const ADMIN = { email: 'admin@salemtech.kz', password: 'Demo-Admin-2026' }
export const ADMIN_URL = 'http://admin.localhost:5174'

export async function adminLogin(page: Page) {
  await page.goto(`${ADMIN_URL}/`)
  await page.getByLabel('E-mail').or(page.locator('input[type=email]')).first().fill(ADMIN.email)
  await page.locator('input[type=password]').fill(ADMIN.password)
  await page.getByRole('button', { name: 'Войти' }).click()
  await expect(page.getByRole('link', { name: 'Обзор' }).first()).toBeVisible()
}

/** Батырманы басып, ашылған диалогтың (модаль/шторка) ішін тексеріп, Escape-пен жабу. */
export async function checkDialog(page: Page, opener: Locator, where: string) {
  await opener.click()
  const dialog = page.getByRole('dialog').last()
  await expect(dialog).toBeVisible()
  await expectNothingClipped(page, where, '[role=dialog]')
  await page.keyboard.press('Escape')
  await expect(page.getByRole('dialog')).toHaveCount(0)
}

/** Бет ішіндегі қойындыларды (батырма-қойынды) кезекпен ашып, әрқайсысын тексеру. */
export async function checkTabs(page: Page, labels: string[], where: string, onTab?: (label: string) => Promise<void>) {
  for (const label of labels) {
    // Қойынды атауында сан болуы мүмкін: «Оплаты (2)».
    const name = new RegExp(`^${label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}( \\(\\d+\\))?$`)
    await page.getByRole('button', { name }).or(page.getByRole('tab', { name })).first().click()
    await expectNothingClipped(page, `${where} › ${label}`)
    if (onTab) await onTab(label)
  }
}
