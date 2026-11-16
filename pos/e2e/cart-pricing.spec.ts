import { expect, test, type APIRequestContext } from '@playwright/test'
import { expectNothingClipped } from './audit'
import { posLogin, useLanguage } from './pos'

/**
 * Себеттегі баға мен позиция жеңілдігі: қарындашпен бағаны тек көтеруге болады (төмен — қате хабар), «%» батырмасы позицияға
 * жеңілдік жолын ашады (пайыз не сома, сомасы бірден көрінеді). Проведённая приёмка «Редактировать»-пен түзетіледі, онда төлем жоқ.
 */
const DEMO_STORE = '11111111-1111-1111-1111-111111111111'
const tag = 'Цен' + Math.random().toString(36).slice(2, 6)

async function seed(request: APIRequestContext, token: string) {
  const headers = { Authorization: `Bearer ${token}`, 'X-Store-Id': DEMO_STORE, 'Accept-Language': 'ru' }
  const units = (await (await request.get('/api/pos/v1/catalog/units', { headers })).json()) as { id: string }[]
  const res = await request.post('/api/pos/v1/products', { headers, data: { name: `${tag} антифриз`, unitId: units[0].id, opening: { quantity: 5, purchasePrice: 800, salePrice: 1000 } } })
  expect(res.ok(), await res.text()).toBe(true)
  const product = ((await res.json()) as { id: string }).id
  const doc = await (await request.post('/api/pos/v1/inventory/receipts', { headers, data: { supplierInvoiceNumber: 'E2E', lines: [{ productId: product, quantity: 2, purchasePrice: 800 }] } })).json() as { id: string }
  expect((await request.post(`/api/pos/v1/inventory/documents/${doc.id}/post`, { headers, data: {} })).ok()).toBe(true)
  return {
    receiptId: doc.id,
    productId: product,
    cleanup: async () => { await request.post(`/api/pos/v1/products/${product}/archive`, { headers, data: {} }) },
  }
}

test('cart line price goes only up, each line can get its own discount, posted receipts are editable', async ({ page }) => {
  await useLanguage(page, 'ru')
  const token = await posLogin(page)
  const data = await seed(page.request, token)
  let taken: string | null = null
  try {
    // Проведённая приёмка: «Редактировать» бар, төлем батырмасы жоқ.
    await page.goto(`/warehouse/receipts/${data.receiptId}`)
    await expect(page.getByRole('button', { name: 'Редактировать' })).toBeVisible()
    // Жолдағы тауар атауы — карточкаға сілтеме (қарау режимінде сол бетте ашылады), артқа — приёмкаға қайту.
    await page.getByRole('link', { name: `${tag} антифриз` }).click()
    await expect(page).toHaveURL(new RegExp(`/products/${data.productId}$`))
    await expect.poll(() => page.locator('input').evaluateAll((els) => els.map((e) => (e as HTMLInputElement).value))).toContain(`${tag} антифриз`)
    await page.goBack()
    await expect(page.getByRole('button', { name: 'Редактировать' })).toBeVisible()
    await expect(page.getByRole('button', { name: /оплат/i })).toHaveCount(0)
    await page.getByRole('button', { name: 'Редактировать' }).click()
    await expect(page.getByRole('button', { name: 'Сохранить изменения' })).toBeVisible()
    // Өңдеу кезінде карточка жаңа қойындыда ашылады — сақталмаған жолдар жоғалмайды.
    await expect(page.getByRole('link', { name: `${tag} антифриз` })).toHaveAttribute('target', '_blank')
    await expectNothingClipped(page, 'ru /warehouse/receipts › правка проведённой')
    await page.getByRole('button', { name: 'Отмена' }).click()

    const listed = page.waitForResponse((r) => new URL(r.url()).pathname.endsWith('/api/pos/v1/registers') && r.request().method() === 'GET')
    await page.goto('/cashier')
    const registers = (await (await listed).json()) as { id: string; isBound: boolean }[]
    const index = registers.findIndex((r) => !r.isBound || !!process.env.CI)
    test.skip(index < 0, 'касса занята другим устройством')
    taken = registers[index].id
    const firstSync = page.waitForResponse((r) => new URL(r.url()).pathname.endsWith('/api/pos/v1/cashier/catalog/sync'))
    await page.getByRole('button', { name: 'Подключить', exact: true }).nth(index).click()
    await firstSync
    await page.locator('input[data-pos-search]').fill(tag)
    await expect(page.getByText(/^Найдено: 1$/)).toBeVisible()
    await page.locator('section').getByRole('button', { name: 'Добавить в корзину', disabled: false }).first().click()

    // Бағаны көтеру — болады, төмендету — қате хабар, баға өзгермейді.
    await page.getByRole('button', { name: 'Изменить цену' }).click()
    await page.getByRole('textbox', { name: 'Изменить цену' }).fill('1200')
    await page.getByRole('textbox', { name: 'Изменить цену' }).press('Enter')
    await expect(page.getByText('по каталогу 1 000 ₸')).toBeVisible()
    await page.getByRole('button', { name: 'Изменить цену' }).click()
    await page.getByRole('textbox', { name: 'Изменить цену' }).fill('900')
    await page.getByRole('textbox', { name: 'Изменить цену' }).press('Enter')
    await expect(page.getByText(/Цену можно только повысить/)).toBeVisible()
    await expect(page.getByText('по каталогу 1 000 ₸')).toBeVisible()

    // Позиция жеңілдігі: 10% → −120 ₸, итог 1 080 ₸.
    await page.getByRole('button', { name: 'Скидка на позицию' }).click()
    await page.getByRole('textbox', { name: 'Размер скидки на позицию' }).fill('10')
    await expect(page.getByText('− 120 ₸').first()).toBeVisible()
    await expect(page.getByText('1 080 ₸').first()).toBeVisible()
    await expectNothingClipped(page, 'ru /cashier › скидка на позицию')
    await page.getByRole('button', { name: 'Убрать скидку на позицию' }).click()
    await expect(page.getByRole('textbox', { name: 'Размер скидки на позицию' })).toHaveCount(0)
    await page.getByRole('button', { name: 'Очистить' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Очистить', exact: true }).click()
  } finally {
    if (taken) await page.request.post(`/api/pos/v1/settings/registers/${taken}/unbind`, { headers: { Authorization: `Bearer ${token}`, 'X-Store-Id': DEMO_STORE }, data: {} })
    await data.cleanup()
  }
})
