import { expect, test, type Page } from '@playwright/test'
import { expectNothingClipped } from './audit'
import { posLogin, useLanguage } from './pos'

/**
 * Инкассация: кассаның «Финансы» панелінде инкассация мен «Сейчас в кассе» көрінеді, «Инкассация» батырмасы диалогты
 * касса шотынан «Деньги в пути»-ге дайындап ашады. Диалогта инкассация қосулы кезде «откуда» — тек касса мен сейф,
 * «куда» — сейф, банк, «Деньги в пути» (банктен не Kaspi-ге инкассация жоқ).
 */
const TYPES_FROM = ['Касса', 'Сейф']

async function optionTexts(page: Page, label: string) {
  return page.locator('[role=dialog]').getByRole('combobox').nth(label === 'from' ? 0 : 1).locator('option').allTextContents()
}

test('encashment from the register and in the accounts dialog', async ({ page }) => {
  await useLanguage(page, 'ru')
  await posLogin(page)

  // Финансы → Счета: инкассацияны қосқанда шоттар сүзіледі.
  await page.goto('/finance/accounts')
  await page.getByRole('button', { name: 'Перевод между счетами' }).click()
  const dialog = page.getByRole('dialog')
  await expect(dialog.getByRole('heading', { name: 'Перевод между счетами' })).toBeVisible()
  const allFrom = await optionTexts(page, 'from')
  expect(allFrom.some((o) => o.startsWith('Банковский счёт'))).toBe(true)
  await dialog.getByRole('switch', { name: /Инкассация/ }).click()
  await expect(dialog.getByRole('heading', { name: 'Инкассация' })).toBeVisible()
  const from = await optionTexts(page, 'from')
  const to = await optionTexts(page, 'to')
  expect(from.length).toBeGreaterThan(0)
  expect(from.every((o) => TYPES_FROM.some((t) => o.startsWith(t)))).toBe(true)
  expect(to.some((o) => o.startsWith('Деньги в пути'))).toBe(true)
  expect(to.some((o) => o.startsWith('Kaspi') || o.startsWith('Касса'))).toBe(false)
  await expect(dialog.getByRole('combobox').nth(1)).toHaveValue(await dialog.getByRole('combobox').nth(1).locator('option', { hasText: 'Деньги в пути' }).getAttribute('value') ?? '')
  await expectNothingClipped(page, 'ru /finance/accounts › инкассация', '[role=dialog]')
  await dialog.getByRole('button', { name: 'Отмена' }).click()

  // Касса → Финансы: инкассация жолы, «Сейчас в кассе», «Инкассация» батырмасы.
  const listed = page.waitForResponse((r) => new URL(r.url()).pathname.endsWith('/api/pos/v1/registers') && r.request().method() === 'GET')
  await page.goto('/cashier')
  const registers = (await (await listed).json()) as { id: string; isBound: boolean }[]
  const index = registers.findIndex((r) => !r.isBound || !!process.env.CI)
  test.skip(index < 0, 'касса занята другим устройством')
  await page.getByRole('button', { name: 'Подключить', exact: true }).nth(index).click()
  await expect(page.getByText(/^Корзина \(\d+\)$/).first()).toBeVisible()
  await page.getByRole('button', { name: 'Финансы', exact: true }).first().click()
  await expect(page.getByText(/^Сейчас в кассе: /)).toBeVisible()
  await expect(page.getByText('Инкассация', { exact: true }).first()).toBeVisible()
  await expectNothingClipped(page, 'ru /cashier › Финансы с инкассацией')
  await page.getByRole('button', { name: 'Инкассация', exact: true }).click()
  await expect(dialog.getByRole('heading', { name: 'Инкассация' })).toBeVisible()
  expect((await optionTexts(page, 'from')).every((o) => TYPES_FROM.some((t) => o.startsWith(t)))).toBe(true)
  await dialog.getByRole('button', { name: 'Отмена' }).click()

  await page.getByRole('button', { name: 'Menu' }).first().click()
  await page.getByRole('menuitem', { name: 'Отключить это устройство' }).click()
  await page.getByRole('alertdialog').getByRole('button', { name: 'Отключить', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Подключить', exact: true }).first()).toBeVisible()
})
