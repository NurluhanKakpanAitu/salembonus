import { expect, test } from '@playwright/test'
import { expectNothingClipped } from './audit'
import { posLogin, useLanguage } from './pos'

/**
 * Қарызды бонуспен өтеу: «Долги клиентов» → клиент → «Принять оплату» → способ «Бонусы» — әр қарызға шек көрінеді
 * (қарыздың 30%-ы, баланстан артық емес), «Оплатить всё» шекке дейін толтырады, шот өрісі жоқ. Төлем «Бонусами» болып шығады.
 */
const DEMO_STORE = '11111111-1111-1111-1111-111111111111'
const tag = Math.random().toString(36).slice(2, 7)

test('customer debt can be paid with bonuses from the finance card', async ({ page }) => {
  await useLanguage(page, 'ru')
  const token = await posLogin(page)
  const headers = { Authorization: `Bearer ${token}`, 'X-Store-Id': DEMO_STORE, 'Accept-Language': 'ru' }
  const created = await page.request.post('/api/pos/v1/cashier/customers', {
    headers, data: { phone: `+7707${String(Date.now()).slice(-7)}`, firstName: `Бонусдолг${tag}`, lastName: 'Тест', birthDate: '1990-05-17' },
  })
  expect(created.ok(), await created.text()).toBe(true)
  const customerId = ((await created.json()) as { id: string }).id
  const debt = await page.request.post('/api/pos/v1/finance/customer-debts', { headers, data: { customerId, amount: 10000, comment: 'e2e бонус' } })
  expect(debt.ok(), await debt.text()).toBe(true)
  const accrued = await page.request.post('/api/pos/v1/bonus/operations/accrue', { headers, data: { customerId, amount: 1000, reason: 'e2e', unlimited: true } })
  expect(accrued.ok(), await accrued.text()).toBe(true)

  await page.goto(`/finance/customer-debts?customer=${customerId}`)
  const card = page.getByRole('dialog')
  await card.getByRole('button', { name: 'Принять оплату' }).click()
  const pay = page.getByRole('dialog').last()
  await pay.getByRole('combobox').first().selectOption('Bonus')
  await expect(pay.getByText('бонусами до 3 000 Б')).toBeVisible() // 30% от 10 000
  await expect(pay.getByText(/Бонусов у клиента: 1\s000 Б/)).toBeVisible()
  await expect(pay.getByText('Счёт зачисления')).toHaveCount(0)
  await pay.getByRole('button', { name: 'Оплатить всё' }).click()
  await expect(pay.getByRole('textbox', { name: 'Из тетради' })).toHaveValue('1000') // баланс меньше лимита
  await expectNothingClipped(page, 'ru /finance/customer-debts › оплата бонусами', '[role=dialog]')
  await pay.getByRole('button', { name: 'Принять оплату' }).click()
  await expect(page.getByText(/Списано 1\s000 Б в счёт долга/)).toBeVisible()

  await card.getByRole('tab', { name: /^Оплаты/ }).click()
  await expect(card.getByText('Бонусами')).toBeVisible()
  const after = (await (await page.request.get(`/api/pos/v1/finance/customer-debts/${customerId}`, { headers })).json()) as { total: number; bonus: { balance: number }[] }
  expect(after.total).toBe(9000)
  expect(after.bonus[0].balance).toBe(0)
})
