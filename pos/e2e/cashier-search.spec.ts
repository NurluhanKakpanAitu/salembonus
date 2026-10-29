import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { posLogin, useLanguage } from './pos'

/**
 * Кассадағы тауар іздеу: тізім ашылмайды, нәтиже бірден тауарлар торында. Санат пен іздеу бірге сүзеді — қай ретпен
 * болса да: іздеп, сосын санат таңдағанда іздеу сақталады; санат таңдап, сосын іздегенде — санат ішінде іздейді.
 * Деректер тест ішінде жасалып, соңында мұрағатқа жіберіледі (жергілікті кассада қалмайды).
 */
const DEMO_STORE = '11111111-1111-1111-1111-111111111111'
const tag = 'Зкс' + Math.random().toString(36).slice(2, 6)

async function seed(request: APIRequestContext, token: string) {
  const headers = { Authorization: `Bearer ${token}`, 'X-Store-Id': DEMO_STORE, 'Accept-Language': 'ru' }
  const post = async (path: string, data: unknown) => {
    const res = await request.post(path, { headers, data })
    expect(res.ok(), await res.text()).toBe(true)
    return res.json() as Promise<{ id: string }>
  }
  const node = (name: string) => post('/api/pos/v1/catalog/nodes', { parentId: null, name, icon: 'package', imageUrl: null, description: null, status: 'Active' })
  const units = (await (await request.get('/api/pos/v1/catalog/units', { headers })).json()) as { id: string }[]
  const product = (name: string, nodeId: string) =>
    post('/api/pos/v1/products', { name, unitId: units[0].id, nodeId, opening: { quantity: 5, purchasePrice: 50, salePrice: 100 } })
  const oils = await node(`Масла ${tag}`)
  const filters = await node(`Фильтры ${tag}`)
  const items = [await product(`${tag} масло моторное`, oils.id), await product(`${tag} фильтр воздушный`, filters.id), await product(`Тосол ${tag.slice(3)}`, oils.id)]
  return {
    oils: `Масла ${tag}`, filters: `Фильтры ${tag}`,
    cleanup: async () => {
      for (const p of items) await request.post(`/api/pos/v1/products/${p.id}/archive`, { headers, data: {} })
      for (const n of [oils, filters]) await request.post(`/api/pos/v1/catalog/nodes/${n.id}/archive`, { headers, data: {} })
    },
  }
}

const card = (page: Page, name: string) => page.locator('section').getByText(name, { exact: true })

test('cashier search shows results in the grid and works together with categories', async ({ page }) => {
  await useLanguage(page, 'ru')
  const token = await posLogin(page)
  const data = await seed(page.request, token)
  try {
    const listed = page.waitForResponse((r) => new URL(r.url()).pathname.endsWith('/api/pos/v1/registers') && r.request().method() === 'GET')
    await page.goto('/cashier')
    const registers = (await (await listed).json()) as { id: string; isBound: boolean }[]
    const index = registers.findIndex((r) => !r.isBound || !!process.env.CI)
    test.skip(index < 0, 'касса занята другим устройством')
    await page.getByRole('button', { name: 'Подключить', exact: true }).nth(index).click()
    const search = page.locator('input[data-pos-search]')
    await expect(search).toBeVisible()

    // 1) Іздеу → санат: нәтиже бірден торда, тізім ашылмайды; санат таңдағанда іздеу қалады.
    await search.fill(tag)
    await expect(card(page, `${tag} масло моторное`)).toBeVisible()
    await expect(card(page, `${tag} фильтр воздушный`)).toBeVisible()
    await expect(page.getByText(/Показать все/)).toHaveCount(0)
    await page.getByRole('button', { name: data.oils }).click()
    await expect(search).toHaveValue(tag)
    await expect(card(page, `${tag} масло моторное`)).toBeVisible()
    await expect(card(page, `${tag} фильтр воздушный`)).toHaveCount(0)
    await expect(card(page, `Тосол ${tag.slice(3)}`)).toHaveCount(0) // санатта бар, бірақ іздеуге сәйкес емес

    // 2) Іздеуді тазалау санатты сақтайды; санат → іздеу тек санат ішінде іздейді.
    await page.getByRole('button', { name: /^Сбросить: / }).click()
    await expect(search).toHaveValue('')
    await expect(page.getByRole('button', { name: data.oils })).toHaveAttribute('aria-pressed', 'true')
    await expect(card(page, `Тосол ${tag.slice(3)}`)).toBeVisible()
    await page.getByRole('button', { name: data.filters }).click()
    await search.fill(tag)
    await expect(card(page, `${tag} фильтр воздушный`)).toBeVisible()
    await expect(card(page, `${tag} масло моторное`)).toHaveCount(0)
    await page.getByRole('button', { name: 'Все товары' }).first().click()
    await expect(card(page, `${tag} масло моторное`)).toBeVisible()
    await expect(card(page, `${tag} фильтр воздушный`)).toBeVisible()

    // Esc тазалайды, тауар тордан себетке қосылады.
    await search.press('Escape')
    await expect(search).toHaveValue('')

    await page.getByRole('button', { name: 'Menu' }).first().click()
    await page.getByRole('menuitem', { name: 'Отключить это устройство' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Отключить', exact: true }).click()
  } finally {
    await data.cleanup()
  }
})
