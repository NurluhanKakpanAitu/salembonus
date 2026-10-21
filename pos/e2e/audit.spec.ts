import { expect, test } from '@playwright/test'
import { findClipped } from './audit'

// Тексерудің өзін тексеру: бұрынғы қате (сан өрісі `w-full`-мен бүкіл жолды алып, «Дней/Месяцев» таңдауы 30px-ке сығылған) табылуы керек.
test('audit catches squeezed select, clipped placeholder and button, passes a sound form', async ({ page }) => {
  await page.goto('/landing.html')
  await page.setContent(`
    <div style="width:400px;font:15px Arial">
      <div id="bad" style="display:flex;gap:8px">
        <input style="width:100%;flex-shrink:1" value="12">
        <select style="flex:1 1 0%;width:100%"><option>Месяцев</option><option>Дней</option></select>
        <input style="width:60px" placeholder="Имя, телефон, город">
        <button style="width:50px;overflow:hidden;white-space:nowrap">Сохранить изменения</button>
        <div style="width:80px;overflow:hidden;white-space:nowrap;text-overflow:ellipsis">Бонусы на день рождения</div>
        <div style="height:40px;width:120px;overflow-y:auto"><div style="height:200px">длинное меню</div></div>
      </div>
      <div id="good" style="display:flex;gap:8px">
        <input style="width:96px" value="12">
        <select style="flex:1;padding-right:36px"><option>Месяцев</option><option>Дней</option></select>
      </div>
    </div>`)
  const bad = await findClipped(page, '#bad')
  expect(bad.some((p) => p.includes('«Месяцев» обрезан'))).toBe(true)
  expect(bad.some((p) => p.includes('подсказка «Имя, телефон, город» обрезана'))).toBe(true)
  expect(bad.some((p) => p.includes('надпись «Сохранить изменения» обрезана'))).toBe(true)
  expect(bad.some((p) => p.includes('текст «Бонусы на день рождения» обрезан многоточием'))).toBe(true)
  expect(bad.some((p) => p.includes('полоса прокрутки слишком толстая'))).toBe(true)
  expect(await findClipped(page, '#good')).toEqual([])
})
