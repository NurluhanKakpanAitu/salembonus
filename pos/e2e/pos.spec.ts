import { expect, test, type Page } from '@playwright/test'
import { expectNothingClipped } from './audit'
import { ensureDemoProducts, posLogin, useLanguage } from './pos'

/** SalemPos-тың барлық бөлімі мен ішкі беттері (URL арқылы ашылатындары), жаңа құжат формаларымен бірге. */
const PAGES = [
  '/products', '/products/categories', '/products/groups', '/products/brands', '/products/units', '/products/characteristics', '/products/new',
  '/warehouse', '/warehouse/warehouses', '/warehouse/receipts', '/warehouse/receipts/new', '/warehouse/receipts/quick', '/warehouse/transfers', '/warehouse/transfers/new',
  '/warehouse/write-offs', '/warehouse/write-offs/new', '/warehouse/suppliers', '/warehouse/movements',
  '/sales',
  '/statistics', '/statistics/sales', '/statistics/products', '/statistics/inventory', '/statistics/suppliers', '/statistics/finance', '/statistics/bonus',
  '/statistics/customers', '/statistics/employees', '/statistics/registers',
  '/finance', '/finance/pnl', '/finance/cash-flow', '/finance/income-expense', '/finance/transactions', '/finance/accounts', '/finance/product-profit',
  '/finance/customer-debts', '/finance/supplier-debts', '/finance/documents', '/finance/documents?view=reports',
  '/bonus', '/bonus/statuses', '/bonus/expiry', '/bonus/birthday', '/bonus/campaigns', '/bonus/statistics', '/bonus/history', '/bonus/rules',
  '/clients', '/clients/overview', '/clients/segments', '/clients/debts', '/clients/activity', '/clients/birthdays', '/clients/exchange', '/clients/settings',
  '/staff', '/staff?new=1',
  '/settings', '/settings/stores', '/settings/registers', '/settings/payments', '/settings/notifications', '/settings/security', '/settings/audit', '/settings/plan',
]

type L = { ru: string; kk: string }
type Opener = {
  path: string
  /** Батырма (не мәзір ашатын батырма) атауы. */
  open: L
  /** Мәзірдегі тармақ (open — мәзірді ашады). */
  item?: L
  /** dialog — модаль/шторка ашылады; panel — беттегі панель/сүзгі жолағы ауысады. */
  kind?: 'dialog' | 'panel'
  /** Деректерге тәуелді (кесте жолы керек) — болмаса өткізіп жібереміз. */
  optional?: boolean
}
const l = (ru: string, kk = ru): L => ({ ru, kk })
const EDIT = l('Редактировать', 'Өңдеу')

/** Әр форма, модаль, шторка мен шебер ашатын батырма (иесі көретін барлығы). */
const OPENERS: Opener[] = [
  { path: '/products', open: l('Импорт') },
  { path: '/products', open: l('Категории', 'Санаттар'), kind: 'panel' },
  { path: '/products', open: l('Menu'), item: l('Изменить классификацию', 'Классификацияны өзгерту'), optional: true },
  { path: '/products/categories', open: l('Добавить категорию', 'Санат қосу') },
  { path: '/products/categories', open: EDIT, optional: true },
  { path: '/products/groups', open: l('Добавить группу', 'Топ қосу') },
  { path: '/products/groups', open: EDIT, optional: true },
  { path: '/products/brands', open: l('Добавить бренд', 'Бренд қосу') },
  { path: '/products/brands', open: EDIT, optional: true },
  { path: '/products/units', open: l('Добавить единицу', 'Бірлік қосу') },
  { path: '/products/units', open: EDIT, optional: true },
  { path: '/products/characteristics', open: l('Добавить характеристику', 'Сипаттама қосу') },
  { path: '/products/characteristics', open: EDIT, optional: true },
  { path: '/warehouse/warehouses', open: l('Добавить склад', 'Қойма қосу') },
  { path: '/warehouse/warehouses', open: EDIT, optional: true },
  { path: '/warehouse/suppliers', open: l('Добавить поставщика', 'Жеткізуші қосу') },
  { path: '/warehouse/suppliers', open: EDIT, optional: true },
  { path: '/warehouse/receipts/new', open: l('Распознать накладную', 'Накладнаяны тану') },
  { path: '/warehouse/receipts/new', open: l('Добавить поставщика', 'Жеткізуші қосу') },
  { path: '/sales', open: l('Фильтры', 'Сүзгілер'), kind: 'panel' },
  { path: '/sales', open: l('Menu'), item: l('Оформить возврат', 'Қайтаруды рәсімдеу'), optional: true },
  { path: '/statistics', open: l('Экспорт'), item: l('PDF-отчёт', 'PDF-есеп') },
  { path: '/finance/cash-flow', open: l('Перевод между счетами', 'Шоттар арасындағы аударым') },
  { path: '/finance/cash-flow', open: l('Добавить операцию', 'Операция қосу') },
  { path: '/finance/income-expense', open: l('Категории', 'Санаттар') },
  { path: '/finance/income-expense', open: l('Поступление', 'Түсім') },
  { path: '/finance/income-expense', open: l('Расход', 'Шығыс') },
  { path: '/finance/transactions', open: l('Новая операция', 'Жаңа операция'), item: l('Новое поступление', 'Жаңа түсім') },
  { path: '/finance/accounts', open: l('Добавить счёт', 'Шот қосу') },
  { path: '/finance/accounts', open: l('Menu'), item: l('Корректировка остатка', 'Қалдықты түзету'), optional: true },
  { path: '/bonus/statuses', open: l('Клиенты', 'Клиенттер') },
  { path: '/bonus/statuses', open: l('Добавить статус', 'Мәртебе қосу') },
  { path: '/bonus/statuses', open: l('Menu'), item: EDIT, optional: true },
  { path: '/bonus/campaigns', open: l('Создать рассылку', 'Тарату жасау') },
  { path: '/bonus/history', open: l('Списать', 'Шегеру') },
  { path: '/bonus/history', open: l('Начислить бонусы', 'Бонус есептеу') },
  { path: '/bonus/rules', open: l('История версий', 'Нұсқалар тарихы') },
  { path: '/bonus/rules', open: l('Предпросмотр', 'Алдын ала қарау') },
  { path: '/clients', open: l('Экспорт') },
  { path: '/clients', open: l('Добавить клиента', 'Клиент қосу') },
  { path: '/clients', open: l('Фильтры', 'Сүзгілер') },
  { path: '/clients/segments', open: l('Новый сегмент', 'Жаңа сегмент') },
  { path: '/staff', open: l('Добавить сотрудника', 'Қызметкер қосу') },
  { path: '/settings/stores', open: l('Добавить магазин', 'Дүкен қосу') },
  { path: '/settings/registers', open: l('Добавить кассу', 'Касса қосу') },
  { path: '/settings/payments', open: l('Добавить реквизит', 'Реквизит қосу') },
  { path: '/settings/security', open: l('Изменить', 'Өзгерту') },
]

/** Мәзір/батырманы басып, ашылғанын тексеріп, жабу. */
async function checkOpener(page: Page, lang: 'ru' | 'kk', o: Opener) {
  const where = `${lang} ${o.path} › ${o.open[lang]}${o.item ? ` › ${o.item[lang]}` : ''}`
  if (new URL(page.url()).pathname + new URL(page.url()).search !== o.path) await page.goto(o.path)
  await page.waitForLoadState('networkidle', { timeout: 5_000 }).catch(() => {})
  const button = page.getByRole('button', { name: o.open[lang], exact: true }).first()
  if (o.optional && !(await button.isVisible().catch(() => false))) {
    test.info().annotations.push({ type: 'skip', description: `${where}: нет данных` })
    console.log(`пропущено (нет данных): ${where}`)
    return
  }
  await expect(button, `${where}: кнопка не найдена`).toBeVisible()
  await button.click()
  if (o.item) await page.getByRole('menuitem', { name: o.item[lang], exact: true }).first().click()
  if (o.kind === 'panel') {
    await expectNothingClipped(page, where)
    await button.click()
    return
  }
  await expect(page.getByRole('dialog').last(), `${where}: окно не открылось`).toBeVisible()
  await expectNothingClipped(page, where, '[role=dialog]')
  await page.keyboard.press('Escape')
  await expect(page.locator('[role=dialog][aria-modal=true]')).toHaveCount(0)
}

/** Бет ішіндегі «бөлім» батырмаларын кезекпен басып тексеру (тауар формасы). */
async function checkSections(page: Page, lang: 'ru' | 'kk', path: string, sections: L[]) {
  await page.goto(path)
  for (const s of sections) {
    await page.getByRole('button', { name: s[lang], exact: true }).first().click()
    await expectNothingClipped(page, `${lang} ${path} › ${s[lang]}`)
  }
}

/** Тест өзі байлаған касса (ru → kk арасында қайта қолданылады). */
let ourRegister: string | null = null

/**
 * Касса экраны: бос кассаны осы браузерге байлап, тауар қосып, төлем мен баптау терезелерін, «Чеки» мен «Финансы»
 * панельдерін тексеріп, соңында құрылғыны ажыратамыз. Бір касса — бір құрылғы: жергілікті ортада касса сіздің
 * браузеріңізге байланған болса, оны тартып алмаймыз — тексеру өткізіледі (CI-да таза база, тексеру әрқашан жүреді).
 */
async function checkCashier(page: Page, lang: 'ru' | 'kk') {
  const listed = page.waitForResponse((r) => new URL(r.url()).pathname.endsWith('/api/pos/v1/registers') && r.request().method() === 'GET')
  await page.goto('/cashier')
  const registers = (await (await listed).json()) as { id: string; isBound: boolean }[]
  const index = registers.findIndex((r) => r.id === ourRegister || !r.isBound || !!process.env.CI)
  if (index < 0) {
    test.info().annotations.push({ type: 'skip', description: 'касса занята другим устройством — проверка кассы пропущена' })
    console.log('пропущено: касса занята другим устройством (локально не отключаем ваш браузер)')
    return
  }
  await expectNothingClipped(page, `${lang} /cashier › подключение кассы`)
  ourRegister = registers[index].id
  await page.getByRole('button', { name: l('Подключить', 'Қосу')[lang], exact: true }).nth(index).click()
  const add = page.getByRole('button', { name: l('Добавить в корзину', 'Себетке қосу')[lang] })
  await expect(add.first()).toBeVisible()
  await expectNothingClipped(page, `${lang} /cashier`)

  await add.first().click()
  await expectNothingClipped(page, `${lang} /cashier › корзина`)
  await page.getByRole('button', { name: new RegExp(`^${lang === 'ru' ? 'Оплатить' : 'Төлеу'} `) }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await expectNothingClipped(page, `${lang} /cashier › оплата`, '[role=dialog]')
  await page.keyboard.press('Escape')

  const menu = () => page.getByRole('button', { name: 'Menu' }).first()
  await menu().click()
  await page.getByRole('menuitem', { name: l('Настройки кассы', 'Касса баптаулары')[lang] }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await expectNothingClipped(page, `${lang} /cashier › настройки кассы`, '[role=dialog]')
  await page.keyboard.press('Escape')

  for (const panel of [l('Чеки', 'Чектер'), l('Финансы', 'Қаржы')]) {
    await page.getByRole('button', { name: panel[lang], exact: true }).first().click()
    await expectNothingClipped(page, `${lang} /cashier › ${panel[lang]}`)
  }

  // Кассаны бос қалдырамыз: келесі іске қосу (не сіздің браузеріңіз) оны қайта байлай алады.
  await menu().click()
  await page.getByRole('menuitem', { name: l('Отключить это устройство', 'Бұл құрылғыны ажырату')[lang] }).click()
  await page.getByRole('alertdialog').getByRole('button', { name: l('Отключить', 'Ажырату')[lang], exact: true }).click()
  await expect(page.getByRole('button', { name: l('Подключить', 'Қосу')[lang], exact: true }).first()).toBeVisible()
}

// Қазақша жазулар әдетте ұзынырақ — бәрін екі тілде тексереміз. Бір тілге бір кіру (кіру шегі минутына 10).
for (const lang of ['ru', 'kk'] as const) {
  // Бір тілге бір тест (бір кіру): тексеру жұмсақ, сондықтан бір қате қалған беттер мен формаларды тоқтатпайды.
  test(`pos ${lang}: every page, section and form is readable`, async ({ page }) => {
    test.setTimeout(20 * 60_000)
    await useLanguage(page, lang)
    await ensureDemoProducts(page, await posLogin(page))
    await expect(page.locator('html')).toHaveAttribute('lang', lang)
    await test.step('pages', async () => {
      for (const path of PAGES) {
        await page.goto(path)
        await expectNothingClipped(page, `${lang} ${path}`)
      }
    })
    await test.step('product form sections', async () => {
      await checkSections(page, lang, '/products/new', [
        l('Основная информация', 'Негізгі ақпарат'), l('Остатки и цены', 'Қалдық және баға'), l('Категория и группа', 'Санат және топ'),
        l('Характеристики', 'Сипаттамалар'), l('Фото и штрихкод', 'Фото және штрихкод'), l('Дополнительно', 'Қосымша'),
      ])
    })
    await test.step('forms, drawers and wizards', async () => {
      for (const o of OPENERS) await checkOpener(page, lang, o)
    })
    // Соңында: касса беті вкладканы киоск режиміне бекітеді.
    await test.step('cashier', async () => { await checkCashier(page, lang) })
  })
}
