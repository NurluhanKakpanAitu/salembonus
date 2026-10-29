import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import { posLogin, useLanguage } from './pos'

/**
 * Уақыт барлық жерде 24 сағаттық (Қазақстан: 14:30, AM/PM жоқ). Браузердің type="time" / datetime-local өрістері мен
 * локальсыз toLocaleString() пайдаланушының Windows/Chrome тіліне қарай AM/PM көрсетеді — оларды кодқа қайтармаймыз.
 */
function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    return statSync(path).isDirectory() ? sources(path) : /\.(tsx?|jsx?)$/.test(name) ? [path] : []
  })
}

test('no browser-locale time pickers or 12-hour formatting in the source', () => {
  const offenders: string[] = []
  for (const root of ['src', '../frontend/src']) {
    for (const file of sources(root)) {
      readFileSync(file, 'utf8').split('\n').forEach((line, i) => {
        if (/type=["'](time|datetime-local)["']/.test(line) || /hour12:\s*true/.test(line)
          || /toLocale(Time)?String\(\s*\)/.test(line) || /toLocale(Time)?String\(\s*undefined/.test(line))
          offenders.push(`${file}:${i + 1}: ${line.trim()}`)
      })
    }
  }
  expect(offenders, offenders.join('\n')).toEqual([])
})

test('time field takes 24-hour time only', async ({ page }) => {
  await page.goto('/login')
  const cases: [string, string | null][] = [['09:30', '09:30'], ['930', '09:30'], ['9:05', '09:05'], ['23:59', '23:59'], ['00:00', '00:00'], ['24:00', null], ['12:60', null], ['ab', null]]
  const parsed = await page.evaluate(async (list) => {
    const { parseTime } = await import('/src/components/ui/TimeInput.tsx' as string)
    return list.map(([raw]) => parseTime(raw) as string | null)
  }, cases)
  expect(parsed).toEqual(cases.map((c) => c[1]))
})

test('birthday bonus time is shown as 24-hour HH:MM', async ({ page }) => {
  await useLanguage(page, 'ru')
  await posLogin(page)
  await page.goto('/bonus/birthday')
  const field = page.locator('input[inputmode=numeric][maxlength="5"]').first()
  await expect(field).toHaveValue(/^([01]\d|2[0-3]):[0-5]\d$/)
  await expect(page.locator('input[type=time], input[type="datetime-local"]')).toHaveCount(0)
  await field.fill('1745')
  await expect(field).toHaveValue('17:45')
})
