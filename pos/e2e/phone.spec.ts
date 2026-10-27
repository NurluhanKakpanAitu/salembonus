import { expect, test } from '@playwright/test'
import { OWNER, posLogin, useLanguage } from './pos'

/**
 * Телефон бойынша іздеу браузердегі тізімдерде («Сотрудники», «Поставщики») сервермен бірдей: соңы, ортасы, «8 …» не «+7 (…)» —
 * бәрі табылады, әріп аралас мәтіндегі сандар телефон деп саналмайды (PhoneSearchTests.cs-тегі жағдайлар).
 */
test.describe('phone search', () => {
  test('matchesPhone follows the same rules as the server', async ({ page }) => {
    await page.goto('/login')
    const cases: [string, string, boolean][] = [
      ['0774', '+77071700774', true],
      ['170 07', '+77071700774', true],
      ['8707170', '+77071700774', true],
      ['8 (707) 170-07-74', '+77071700774', true],
      ['+7 707 170 07 74', '+7 707 170 07 74', true],
      ['8707', '+77011238707', true],
      ['8701', '+77071700774', false],
      ['12', '+77071700774', false],
      ['Айгерим 0774', '+77071700774', false],
    ]
    const results = await page.evaluate(async (list) => {
      const { matchesPhone } = await import('/src/lib/phone.ts' as string)
      return list.map(([q, phone]) => matchesPhone(phone, q) as boolean)
    }, cases)
    expect(results).toEqual(cases.map((c) => c[2]))
  })

  test('staff list finds an employee by the phone typed with 8', async ({ page }) => {
    await useLanguage(page, 'ru')
    await posLogin(page)
    await page.goto('/staff')
    const shown = `+7 ${OWNER.phone.slice(2, 5)} ${OWNER.phone.slice(5, 8)} ${OWNER.phone.slice(8, 10)} ${OWNER.phone.slice(10)}`
    await expect(page.getByText(shown).first()).toBeVisible()
    await page.getByRole('textbox', { name: /Поиск|Имя|телефон/i }).first().fill(`8 ${OWNER.phone.slice(2, 5)} ${OWNER.phone.slice(5, 8)}`)
    await expect(page.getByText(shown).first()).toBeVisible()
    await page.getByRole('textbox', { name: /Поиск|Имя|телефон/i }).first().fill('Нет 0001')
    await expect(page.getByText(shown)).toHaveCount(0)
  })
})
