import { expect, test } from '@playwright/test'
import { expectNothingClipped } from './audit'
import { posLogin, useLanguage } from './pos'

/**
 * Дәптердегі қарыздар мен қалдық түзетуі:
 * - «Долги клиентов» → «Добавить долг»: клиент табылмаса, осы жерде қосылады; қарыз «Из тетради» болып шығады, «Закрыть» — себеппен, ақшасыз;
 * - «Долги поставщикам» → «Добавить долг» (ДЛГ-…) және «Закрыть»;
 * - «Счета» → «Корректировка остатка»: нақты қалдықты енгізгенде айырма өзі есептеледі, тарихы — журналда.
 */
const DEMO_STORE = '11111111-1111-1111-1111-111111111111'
const tag = Math.random().toString(36).slice(2, 7)

test('notebook debts can be added and closed, account balance corrected to the counted amount', async ({ page }) => {
  await useLanguage(page, 'ru')
  const token = await posLogin(page)
  const headers = { Authorization: `Bearer ${token}`, 'X-Store-Id': DEMO_STORE, 'Accept-Language': 'ru' }

  // ---- Клиент: дәптерден қарыз, клиент осы жерде қосылады ----
  await page.goto('/finance/customer-debts')
  await page.getByRole('button', { name: 'Добавить долг' }).click()
  const add = page.getByRole('dialog')
  await expect(add.getByRole('heading', { name: 'Добавить долг клиента' })).toBeVisible()
  await add.getByRole('textbox', { name: 'Имя или телефон клиента' }).fill(`Тетрадь${tag}`)
  await add.getByRole('button', { name: 'Не найден — добавить клиента' }).click()
  await add.getByRole('textbox', { name: 'Фамилия' }).fill('Долгов')
  await add.getByRole('textbox', { name: 'Телефон' }).fill(`+7707${String(Date.now()).slice(-7)}`)
  await add.getByRole('button', { name: 'Добавить клиента' }).click()
  await expect(add.getByText(`Тетрадь${tag} Долгов`)).toBeVisible()
  await add.getByRole('textbox', { name: 'Сумма долга' }).fill('7000')
  await add.getByRole('textbox', { name: 'Комментарий' }).fill('Тетрадь, стр. 4')
  await expectNothingClipped(page, 'ru /finance/customer-debts › добавить долг', '[role=dialog]')
  await add.getByRole('button', { name: 'Добавить долг' }).click()

  const card = page.getByRole('dialog')
  await expect(card.getByRole('heading', { name: `Тетрадь${tag} Долгов` })).toBeVisible()
  await expect(card.getByText('Из тетради')).toBeVisible()
  await expect(card.getByText('Тетрадь, стр. 4')).toBeVisible()
  await card.getByRole('button', { name: 'Закрыть', exact: true }).click()
  const close = page.getByRole('dialog').last()
  await expect(close.getByText('Долг закрывается без денег')).toBeVisible()
  await close.getByRole('button', { name: 'Оплачен по тетради' }).click()
  await expectNothingClipped(page, 'ru /finance/customer-debts › закрыть долг', '[role=dialog]')
  await close.getByRole('button', { name: 'Закрыть долг' }).click()
  await expect(page.getByText('Долг закрыт', { exact: true })).toBeVisible()
  await card.getByRole('tab', { name: /^Оплаты/ }).click()
  await expect(card.getByText('Закрыт без оплаты')).toBeVisible()
  await expect(card.getByText('Оплачен по тетради')).toBeVisible()
  await page.keyboard.press('Escape')

  // ---- Жеткізуші: дәптерден қарыз (ДЛГ-…) және жабу ----
  const supplierName = `Поставщик тетрадь ${tag}`
  const created = await page.request.post('/api/pos/v1/inventory/suppliers', { headers, data: { name: supplierName, type: 'Company' } })
  expect(created.ok(), await created.text()).toBe(true)
  const supplierId = ((await created.json()) as { id: string }).id
  try {
    await page.goto('/finance/supplier-debts')
    await page.getByRole('button', { name: 'Добавить долг' }).click()
    const sAdd = page.getByRole('dialog')
    await sAdd.getByRole('combobox', { name: 'Поставщик' }).selectOption({ label: supplierName })
    await sAdd.getByRole('textbox', { name: 'Сумма долга' }).fill('12000')
    await sAdd.getByRole('textbox', { name: 'Номер накладной или записи' }).fill('Накл. 15')
    await expectNothingClipped(page, 'ru /finance/supplier-debts › добавить долг', '[role=dialog]')
    await sAdd.getByRole('button', { name: 'Добавить долг' }).click()
    const sCard = page.getByRole('dialog')
    await expect(sCard.getByRole('heading', { name: supplierName })).toBeVisible()
    await expect(sCard.getByText(/^ДЛГ-\d+/)).toBeVisible()
    await sCard.getByRole('button', { name: 'Закрыть', exact: true }).click()
    const sClose = page.getByRole('dialog').last()
    await sClose.getByRole('button', { name: 'Рассчитались товаром' }).click()
    await sClose.getByRole('button', { name: 'Закрыть долг' }).click()
    await expect(page.getByText('Долг закрыт', { exact: true })).toBeVisible()
    await sCard.getByRole('tab', { name: /^Оплаты/ }).click()
    await expect(sCard.getByText('Закрыт без оплаты')).toBeVisible()
    await page.keyboard.press('Escape')
  } finally {
    await page.request.post(`/api/pos/v1/inventory/suppliers/${supplierId}/archive`, { headers, data: {} })
  }

  // ---- Шот: нақты қалдыққа түзету ----
  const accounts = (await (await page.request.get('/api/pos/v1/finance/accounts', { headers })).json()) as { accounts: { id: string; name: string; balance: number; isActive: boolean }[] }
  const account = accounts.accounts.find((a) => a.isActive)!
  await page.goto('/finance/accounts')
  await page.getByRole('button', { name: 'Корректировка остатка' }).first().click()
  const adj = page.getByRole('dialog')
  await adj.getByRole('combobox', { name: 'Счёт' }).selectOption(account.id)
  await adj.getByRole('textbox', { name: 'Сколько денег на самом деле' }).fill(String(account.balance + 150))
  await expect(adj.getByText('Разница:')).toBeVisible()
  await expect(adj.getByText('+150 ₸')).toBeVisible()
  await adj.getByRole('textbox', { name: 'Причина' }).fill('Пересчёт кассы e2e')
  await expectNothingClipped(page, 'ru /finance/accounts › корректировка', '[role=dialog]')
  await adj.getByRole('button', { name: 'Корректировка остатка' }).click()
  await page.getByRole('alertdialog').getByRole('button', { name: 'Корректировка остатка' }).click()
  await expect(page.getByText('Остаток скорректирован')).toBeVisible()
  const after = (await (await page.request.get('/api/pos/v1/finance/accounts', { headers })).json()) as typeof accounts
  expect(after.accounts.find((a) => a.id === account.id)!.balance).toBe(account.balance + 150)
  // Кері қайтару: айырманы алып тастау — тест деректерді өзгертпей қалдырады.
  await page.request.post('/api/pos/v1/finance/adjustments', { headers, data: { accountId: account.id, amount: -150, reason: 'Откат e2e' } })

  await page.getByRole('link', { name: 'История корректировок' }).click()
  await expect(page).toHaveURL(/type=Adjustment/)
})
