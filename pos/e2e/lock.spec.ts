import { expect, test } from '@playwright/test'
import { posLogin, useLanguage } from './pos'

/**
 * Касса бұғаты (әрекетсіздік не «Заблокировать кассу») тек касса бетін жабады: сол браузердегі кабинет (тауар, қойма, есептер)
 * жұмыс істей береді. Касса бос болмаса (жергілікті ортада басқа құрылғыға байланған) — тест өткізіледі.
 */
test('locking the cashier does not lock the cabinet', async ({ page, context }) => {
  await useLanguage(page, 'ru')
  await posLogin(page)
  const listed = page.waitForResponse((r) => new URL(r.url()).pathname.endsWith('/api/pos/v1/registers') && r.request().method() === 'GET')
  await page.goto('/cashier')
  const registers = (await (await listed).json()) as { id: string; isBound: boolean }[]
  const index = registers.findIndex((r) => !r.isBound || !!process.env.CI)
  test.skip(index < 0, 'касса занята другим устройством')
  await page.getByRole('button', { name: 'Подключить', exact: true }).nth(index).click()
  await expect(page.getByRole('button', { name: 'Добавить в корзину' }).first()).toBeVisible()

  await page.getByRole('button', { name: /Владелец/ }).click()
  await page.getByText('Заблокировать кассу').click()
  await expect(page.getByRole('heading', { name: 'Касса заблокирована' })).toBeVisible()

  // Сол браузердегі кабинет — бұғатсыз, бет жаңартылса да.
  const cabinet = await context.newPage()
  await cabinet.goto('/products')
  await expect(cabinet.getByRole('link', { name: 'Склад' }).first()).toBeVisible()
  await expect(cabinet.getByRole('heading', { name: 'Касса заблокирована' })).toHaveCount(0)
  await cabinet.reload()
  await expect(cabinet.getByRole('link', { name: 'Склад' }).first()).toBeVisible()
  await expect(cabinet.getByRole('heading', { name: 'Касса заблокирована' })).toHaveCount(0)
  // Кабинетте кассаны бұғаттау батырмасы жоқ — ол тек касса бетінде.
  await cabinet.getByRole('button', { name: /Владелец/ }).click()
  await expect(cabinet.getByText('Заблокировать кассу')).toHaveCount(0)
  await cabinet.close()

  // Касса беті бұғатта қалады; PIN-мен ашылады (StaffSeeder.OwnerPin), соңында касса босатылады.
  await page.bringToFront()
  await expect(page.getByRole('heading', { name: 'Касса заблокирована' })).toBeVisible()
  await page.keyboard.type('3691')
  await expect(page.getByRole('heading', { name: 'Касса заблокирована' })).toHaveCount(0)
  await page.getByRole('button', { name: 'Menu' }).first().click()
  await page.getByRole('menuitem', { name: 'Отключить это устройство' }).click()
  await page.getByRole('alertdialog').getByRole('button', { name: 'Отключить', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Подключить', exact: true }).first()).toBeVisible()
})
