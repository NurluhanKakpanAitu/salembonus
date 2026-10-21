import { expect, test } from '@playwright/test'
import { expectNothingClipped } from './audit'
import { ADMIN_URL, adminLogin, checkDialog, checkTabs } from './helpers'

// Әкімшілік (admin.salemtech.kz): әр бөлім, әр форма (жасау/өзгерту/төлем/тоқтату) мен бизнес карточкасының әр қойындысы толық көрінеді.
test.beforeEach(async ({ page }) => { await adminLogin(page) })

test('admin sections and their forms', async ({ page }) => {
  const sections: [string, string | null][] = [
    ['/', null], ['/businesses', 'Добавить бизнес'], ['/leads', null], ['/payments', 'Принять оплату'], ['/plans', 'Новый тариф'], ['/admins', 'Добавить'],
  ]
  for (const [path, form] of sections) {
    await page.goto(`${ADMIN_URL}${path}`)
    await expectNothingClipped(page, `admin ${path}`)
    if (form) await checkDialog(page, page.getByRole('button', { name: form, exact: true }).first(), `admin ${path} › ${form}`)
  }
  await page.goto(`${ADMIN_URL}/plans`)
  await checkDialog(page, page.getByRole('button', { name: 'Изменить' }).first(), 'admin /plans › Изменить тариф')
  await checkDialog(page, page.getByRole('button', { name: 'Сменить пароль' }), 'admin › Сменить пароль')

  await page.goto(`${ADMIN_URL}/leads`)
  await page.getByRole('button', { name: 'Все', exact: false }).first().click()
  const comment = page.getByRole('button', { name: 'Комментарий' })
  if (await comment.count()) await checkDialog(page, comment.first(), 'admin /leads › Комментарий')
})

test('admin business card: tabs and forms', async ({ page }) => {
  await page.goto(`${ADMIN_URL}/businesses`)
  const row = page.locator('tbody tr.cursor-pointer').first()
  await expect(row, 'нужен хотя бы один бизнес (демо-бизнес из StaffSeeder)').toBeVisible()
  await row.click()
  await expect(page.getByRole('button', { name: 'Редактировать' })).toBeVisible()
  await expectNothingClipped(page, 'admin business')
  await checkDialog(page, page.getByRole('button', { name: 'Редактировать' }), 'admin business › Редактировать')
  await checkDialog(page, page.getByRole('button', { name: 'Принять оплату' }).first(), 'admin business › Принять оплату')
  const suspend = page.getByRole('button', { name: 'Приостановить' })
  if (await suspend.count()) await checkDialog(page, suspend, 'admin business › Приостановить')
  await checkTabs(page, ['Обзор', 'Модули', 'Оплаты', 'Доступы', 'Магазины', 'История'], 'admin business', async (tab) => {
    if (tab === 'Доступы') await checkDialog(page, page.getByRole('button', { name: 'Добавить владельца / админа' }), 'admin business › Добавить владельца / админа')
    if (tab === 'Магазины') await checkDialog(page, page.getByRole('button', { name: 'Добавить магазин' }), 'admin business › Добавить магазин')
  })
})
