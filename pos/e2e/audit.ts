import { expect, type Page } from '@playwright/test'

/**
 * «Бәрі көрінсін» тексеруі: шын браузерде өлшейміз, мәтін өз өрісіне сыя ма.
 *  - select: КЕЗ КЕЛГЕН нұсқаның мәтіні (тек таңдалғаны емес) көрсеткішке арналған орынсыз енге сыюы керек;
 *  - input: мәні не (бос болса) placeholder-і, күн/уақыт өрісінің үлгісі сыюы керек;
 *  - батырма мен сілтеме-батырма: жазуы кесілмеуі керек;
 *  - бет пен модаль көлденеңінен шықпауы керек (көлденең айналдыру жоқ).
 * Қате тізімі адамға түсінікті: өрістің жапсырмасы, мәтіні, керек/бар ені.
 */
export async function findClipped(page: Page, scope = 'body'): Promise<string[]> {
  return page.evaluate((scopeSelector) => {
    const root = document.querySelector(scopeSelector)
    if (!root) return [`scope ${scopeSelector} not found`]
    const problems: string[] = []
    const canvas = document.createElement('canvas')
    const ctx = canvas.getContext('2d')!
    const px = (v: string) => Number.parseFloat(v) || 0

    const visible = (el: Element) => {
      const r = el.getBoundingClientRect()
      if (r.width === 0 || r.height === 0) return false
      for (let n: Element | null = el; n; n = n.parentElement) {
        const s = getComputedStyle(n)
        if (s.display === 'none' || s.visibility === 'hidden' || s.opacity === '0') return false
      }
      return true
    }
    const textWidth = (el: Element, text: string) => {
      const s = getComputedStyle(el)
      ctx.font = `${s.fontStyle} ${s.fontWeight} ${s.fontSize} ${s.fontFamily}`
      const spacing = s.letterSpacing === 'normal' ? 0 : px(s.letterSpacing) * text.length
      return ctx.measureText(text).width + spacing
    }
    const inner = (el: Element) => {
      const s = getComputedStyle(el)
      return (el as HTMLElement).clientWidth - px(s.paddingLeft) - px(s.paddingRight)
    }
    // Өрістің адамға түсінікті атауы: aria-label, <label for>, Field жапсырмасы, жақын тақырып.
    const nameOf = (el: Element) => {
      const aria = el.getAttribute('aria-label')
      if (aria) return aria
      const id = el.getAttribute('id')
      const forLabel = id ? document.querySelector(`label[for="${CSS.escape(id)}"]`)?.textContent : null
      if (forLabel) return forLabel.trim()
      for (let n: Element | null = el.parentElement, depth = 0; n && depth < 4; n = n.parentElement, depth++) {
        const label = n.querySelector(':scope > div.mb-1\\.5, :scope > label, :scope > span')
        if (label && !label.contains(el) && label.textContent?.trim()) return label.textContent.trim().slice(0, 60)
      }
      const ph = el.getAttribute('placeholder')
      return ph ? `placeholder «${ph}»` : el.tagName.toLowerCase()
    }
    const where = () => {
      const dialog = root.closest('[role=dialog]') ?? (scopeSelector === 'body' ? null : root)
      const title = dialog?.querySelector('h2, h3')?.textContent?.trim()
      return title ? ` [${title}]` : ''
    }
    const report = (el: Element, what: string, need: number, has: number) =>
      problems.push(`${nameOf(el)}${where()}: ${what} — нужно ${Math.ceil(need)}px, есть ${Math.floor(has)}px`)

    for (const el of root.querySelectorAll('select')) {
      if (!visible(el)) continue
      const room = inner(el)
      for (const o of el.options) {
        const t = o.text.trim()
        if (!t) continue
        const w = textWidth(el, t)
        if (w > room + 1) { report(el, `вариант «${t}» обрезан`, w, room); break }
      }
    }

    const sample: Record<string, string> = { date: '00.00.0000', time: '00:00', 'datetime-local': '00.00.0000, 00:00', month: 'сентябрь 0000 г.' }
    for (const el of root.querySelectorAll<HTMLInputElement>('input')) {
      if (!visible(el) || ['checkbox', 'radio', 'hidden', 'file', 'range', 'color'].includes(el.type)) continue
      const room = inner(el)
      if (sample[el.type]) {
        const w = textWidth(el, sample[el.type]) + 22 // + календарь/сағат белгісі
        if (w > room + 1) report(el, `дата/время «${el.value || sample[el.type]}» не помещается`, w, room)
      } else if (el.value) {
        if (el.scrollWidth > el.clientWidth + 1 && document.activeElement !== el) report(el, `значение «${el.value}» обрезано`, el.scrollWidth, el.clientWidth)
      } else if (el.placeholder) {
        const w = textWidth(el, el.placeholder)
        if (w > room + 1) report(el, `подсказка «${el.placeholder}» обрезана`, w, room)
      }
    }

    // Батырма: мәтін түйіндерінің шын шекарасы батырма ішінде ме (абсолютті белгілер — мысалы, «9+» санағышы — есептелмейді).
    for (const el of root.querySelectorAll<HTMLElement>('button, a, [role=tab]')) {
      if (!visible(el) || !el.textContent?.trim()) continue
      const box = el.getBoundingClientRect()
      const s = getComputedStyle(el)
      const left = box.left + px(s.borderLeftWidth) - 0.5
      const right = box.right - px(s.borderRightWidth) + 0.5
      const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT)
      let textLeft = Infinity
      let textRight = -Infinity
      for (let n = walker.nextNode(); n; n = walker.nextNode()) {
        if (!n.textContent?.trim()) continue
        let decorative = false
        for (let p = n.parentElement; p && p !== el; p = p.parentElement) if (['absolute', 'fixed'].includes(getComputedStyle(p).position)) decorative = true
        if (decorative) continue
        const range = document.createRange()
        range.selectNodeContents(n)
        const r = range.getBoundingClientRect()
        textLeft = Math.min(textLeft, r.left)
        textRight = Math.max(textRight, r.right)
      }
      // «Керек» — мәтіннің батырма шетінен басталып біткенге дейінгі ені, «бар» — батырманың ішкі ені.
      if (textRight > right + 1 || textLeft < left - 1)
        report(el, `надпись «${el.textContent.trim().slice(0, 60)}» обрезана`, Math.max(textRight, right) - Math.min(textLeft, left), right - left)
    }

    // Көп нүктемен (truncate) кесілген кез келген мәтін — мәзір, тақырып, кесте ұяшығы: дерек толық көрінуі керек.
    for (const el of root.querySelectorAll<HTMLElement>('*')) {
      if (el.children.length > 0 && !el.textContent?.trim()) continue
      const s = getComputedStyle(el)
      if (s.textOverflow !== 'ellipsis' || el.scrollWidth <= el.clientWidth + 1 || !visible(el)) continue
      const text = el.textContent?.trim().replace(/\s+/g, ' ') ?? ''
      if (text) problems.push(`текст «${text.slice(0, 80)}»${where()} обрезан многоточием — нужно ${el.scrollWidth}px, есть ${el.clientWidth}px`)
    }

    const doc = document.documentElement
    if (scopeSelector === 'body' && doc.scrollWidth > window.innerWidth + 1) problems.push(`страница шире экрана: ${doc.scrollWidth}px при ${window.innerWidth}px`)
    for (const d of root.querySelectorAll<HTMLElement>('[role=dialog]')) {
      for (const n of d.querySelectorAll<HTMLElement>('*')) {
        const s = getComputedStyle(n)
        if (['auto', 'scroll'].includes(s.overflowX) && n.scrollWidth > n.clientWidth + 1 && !n.closest('table, [data-scroll-x]'))
          problems.push(`${d.querySelector('h2, h3')?.textContent?.trim() ?? 'диалог'}: содержимое шире окна (${n.scrollWidth}px при ${n.clientWidth}px)`)
      }
    }
    return [...new Set(problems)]
  }, scope)
}

/** Тест ішінде: экранда (не диалогта) кесілген ештеңе жоқ екенін тексеру. */
export async function expectNothingClipped(page: Page, where: string, scope = 'body') {
  // Кейбір беттер деректерді мезгіл-мезгіл сұрайды — желі тынышталуын 5 с-тан артық күтпейміз.
  await page.waitForLoadState('networkidle', { timeout: 5_000 }).catch(() => {})
  await page.evaluate(() => document.fonts.ready)
  const problems = await findClipped(page, scope)
  // Жұмсақ тексеру: бір сынақ барлық бет пен диалогтағы мәселелердің толық тізімін береді.
  expect.soft(problems, `${where}: обрезанный текст\n  ${problems.join('\n  ')}`).toEqual([])
}
