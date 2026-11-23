import { expect, test } from '@playwright/test'
import { OWNER, posLogin, useLanguage } from './pos'

/**
 * Касса ретінде қосылған құрылғы: кабинет сеансы жоқ болса, кіру беті PIN сұрайды (кассаға). Бірақ одан
 * «Войти в кабинет по паролю» арқылы кабинетке (тауарлар) өтуге болады; кері — «Вход в кассу по PIN-коду».
 * Касса бос болмаса (жергілікті ортада басқа құрылғыға байланған) — тест өткізіледі.
 */
test('a device connected as a register can still open the cabinet with a password', async ({ page, context }) => {
  await useLanguage(page, 'ru')
  const token = await posLogin(page)
  const listed = page.waitForResponse((r) => new URL(r.url()).pathname.endsWith('/api/pos/v1/registers') && r.request().method() === 'GET')
  await page.goto('/cashier')
  const registers = (await (await listed).json()) as { id: string; isBound: boolean }[]
  const index = registers.findIndex((r) => !r.isBound || !!process.env.CI)
  test.skip(index < 0, 'касса занята другим устройством')
  await page.getByRole('button', { name: 'Подключить', exact: true }).nth(index).click()
  await expect(page.getByText(/^Корзина \(\d+\)$/).first()).toBeVisible()

  try {
    // Кабинет сеансы бітті (шықты не мерзімі өтті): тек касса cookie-і қалады.
    await context.clearCookies({ name: 'salem_staff_rt' })
    const tab = await context.newPage()
    await tab.goto('/products')
    await expect(tab).toHaveURL(/\/login/)
    await expect(tab.locator('input[type=password]')).toHaveCount(0) // касса құрылғысы — PIN
    await tab.getByRole('button', { name: 'Войти в кабинет по паролю' }).click()
    await expect(tab.getByText(/подключено как касса/)).toBeVisible()
    await tab.getByRole('button', { name: 'Вход в кассу по PIN-коду' }).click()
    await expect(tab.locator('input[type=password]')).toHaveCount(0)

    // Иесі PIN-мен кассаға кірді (StaffSeeder.OwnerPin) → «Выйти» → PIN экраны емес, кабинеттің кіру беті.
    await tab.getByRole('button', { name: /Владелец/ }).first().click()
    await tab.keyboard.type('3691')
    await expect(tab).toHaveURL(/\/cashier/)
    await tab.getByRole('button', { name: /Владелец/ }).first().click()
    await tab.getByRole('button', { name: 'Выйти' }).click()
    await expect(tab).toHaveURL(/\/login\?cabinet=1/)
    await expect(tab.locator('input[type=password]')).toBeVisible()
    await tab.locator('input[inputmode=tel]').fill(OWNER.phone)
    await tab.locator('input[type=password]').fill(OWNER.password)
    await tab.locator('button[type=submit]').click()
    await expect(tab).not.toHaveURL(/\/login|\/cashier/)
    await tab.goto('/products')
    await expect(tab).toHaveURL(/\/products/)
    await expect(tab.getByRole('link', { name: 'Склад' }).first()).toBeVisible()
    await tab.close()
  } finally {
    // Касса босатылады (access токен әлі жарамды).
    await page.request.post(`/api/pos/v1/settings/registers/${registers[index].id}/unbind`, {
      headers: { Authorization: `Bearer ${token}`, 'X-Store-Id': '11111111-1111-1111-1111-111111111111' }, data: {},
    })
  }
})
