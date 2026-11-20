import { expect, test, type Page } from '@playwright/test'
import { posLogin, useLanguage } from './pos'

/**
 * Проведённый документ сторнируется из мәзір «⋮ → Сторнировать»: себеп жазылады, батырма басылады —
 * құжат «Сторнирован», қалдық бұрынғыға оралады. Себепсіз батырма өшірулі көрінеді.
 */
const DEMO_STORE = '11111111-1111-1111-1111-111111111111'
const tag = 'Сто' + Math.random().toString(36).slice(2, 6)

async function reverseFromPage(page: Page, url: string) {
  await page.goto(url)
  await expect(page.getByText('Проведён', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Menu', exact: true }).first().click()
  await page.getByRole('menuitem', { name: 'Сторнировать', exact: true }).click()
  const dialog = page.getByRole('dialog')
  const confirm = dialog.getByRole('button', { name: 'Сторнировать', exact: true })
  await expect(confirm).toBeDisabled()
  await dialog.getByRole('textbox').fill('Ошиблись, проверка e2e')
  await expect(confirm).toBeEnabled()
  await confirm.click()
  await expect(page.getByText('Документ сторнирован', { exact: true })).toBeVisible()
  await expect(dialog).toBeHidden()
  await expect(page.getByText('Сторнирован', { exact: true }).first()).toBeVisible()
}

test('posted receipt and write-off can be reversed from the document page', async ({ page }) => {
  await useLanguage(page, 'ru')
  const token = await posLogin(page)
  const headers = { Authorization: `Bearer ${token}`, 'X-Store-Id': DEMO_STORE, 'Accept-Language': 'ru' }
  const units = (await (await page.request.get('/api/pos/v1/catalog/units', { headers })).json()) as { id: string }[]
  const res = await page.request.post('/api/pos/v1/products', { headers, data: { name: `${tag} шарнир`, unitId: units[0].id, opening: { quantity: 5, purchasePrice: 800, salePrice: 1000 } } })
  expect(res.ok(), await res.text()).toBe(true)
  const product = ((await res.json()) as { id: string }).id
  const stock = async () => {
    const p = (await (await page.request.get(`/api/pos/v1/products/${product}`, { headers })).json()) as { stock: { quantity: number }[] }
    return p.stock.reduce((s, x) => s + x.quantity, 0)
  }
  try {
    const post = async (path: string, data: object) => {
      const doc = (await (await page.request.post(`/api/pos/v1/inventory/${path}`, { headers, data })).json()) as { id: string }
      const posted = await page.request.post(`/api/pos/v1/inventory/documents/${doc.id}/post`, { headers, data: {} })
      expect(posted.ok(), await posted.text()).toBe(true)
      return doc.id
    }
    const before = await stock()
    const receipt = await post('receipts', { supplierInvoiceNumber: 'E2E', lines: [{ productId: product, quantity: 2, purchasePrice: 800 }] })
    await reverseFromPage(page, `/warehouse/receipts/${receipt}`)
    const writeOff = await post('write-offs', { reason: 'Loss', lines: [{ productId: product, quantity: 1 }] })
    await reverseFromPage(page, `/warehouse/write-offs/${writeOff}`)
    expect(await stock()).toBe(before)
  } finally {
    await page.request.post(`/api/pos/v1/products/${product}/archive`, { headers, data: {} })
  }
})
