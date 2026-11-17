import { useState } from 'react'
import { FileText } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { analyticsApi } from '../../lib/analyticsApi'
import type { AnalyticsFilters, AnalyticsTab, AnalyticsTabData, Overview, SalesTrend } from '../../lib/analyticsTypes'
import { dateTime } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT, type Translator, type TranslationKey } from '../../lib/i18n'
import { formatKind, periodText } from './statsFormat'

type Block = 'kpis' | 'trend' | 'payments' | 'products' | 'customers' | 'summary' | 'tab'
const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!)

/**
 * «Настройки PDF-отчёта» (ТЗ «Статистика» §6): атауы, кезең, дүкендер, блоктар, A4. «Скачать PDF» есепті A4 бетке жинап,
 * басып шығару терезесін ашады (онда «Сохранить как PDF»); «Отмена» ештеңе жасамайды.
 */
export function PdfReportModal({ filters, tab, extra, stores, onClose }: {
  filters: AnalyticsFilters
  tab: AnalyticsTab
  extra: Record<string, string>
  stores: { id: string; name: string }[]
  onClose: () => void
}) {
  const t = useT()
  const [title, setTitle] = useState(t('stats.pdf.defaultTitle'))
  const blocks: Block[] = ['kpis', 'trend', 'payments', 'products', 'customers', 'summary', ...(tab !== 'overview' ? ['tab' as const] : [])]
  const [picked, setPicked] = useState<Record<Block, boolean>>(() => Object.fromEntries(blocks.map((b) => [b, true])) as Record<Block, boolean>)
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)
  const storeNames = filters.storeIds.map((id) => stores.find((s) => s.id === id)?.name ?? '').filter(Boolean)

  const generate = async () => {
    setBusy(true)
    setFailed(false)
    try {
      const [overview, trend, tabData] = await Promise.all([
        analyticsApi.overview(filters),
        picked.trend ? analyticsApi.trend(filters) : Promise.resolve(null),
        tab !== 'overview' && picked.tab ? analyticsApi.tab(tab, filters, { ...extra, page: 1, pageSize: 100 }) : Promise.resolve(null),
      ])
      print(reportHtml({ title, overview, trend, tabData, tab, picked, storeNames, t }))
      toast(t('stats.pdf.ready'))
      onClose()
    } catch {
      setFailed(true)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title={t('stats.pdf.title')} onClose={onClose} width={520}>
      <div className="flex flex-col gap-4">
        <Field label={t('stats.pdf.name')}><Input value={title} onChange={(e) => setTitle(e.target.value)} maxLength={120} /></Field>
        <div className="grid grid-cols-2 gap-3 text-[13px]">
          <div><div className="text-ink-2">{t('stats.period')}</div><b>{periodText(filters.from, filters.to)}</b></div>
          <div><div className="text-ink-2">{t('stats.stores')}</div><b>{storeNames.join(', ') || '—'}</b></div>
        </div>
        <fieldset>
          <legend className="mb-1.5 text-[13px] font-medium text-ink-2">{t('stats.pdf.blocks')}</legend>
          <div className="grid gap-1.5 sm:grid-cols-2">
            {blocks.map((b) => (
              <label key={b} className="flex cursor-pointer items-center gap-2 text-[14px]">
                <input type="checkbox" checked={picked[b]} onChange={(e) => setPicked((p) => ({ ...p, [b]: e.target.checked }))} />
                {b === 'tab' ? t(`stats.tab.${tab}` as TranslationKey) : t(`stats.pdf.block.${b}` as TranslationKey)}
              </label>
            ))}
          </div>
        </fieldset>
        <p className="text-[12px] text-ink-3">{t('stats.pdf.hint')}</p>
        {failed && (
          <div role="alert" className="flex items-center justify-between rounded-xl bg-danger-soft px-4 py-3 text-[13px] text-danger">
            {t('stats.pdf.failed')}<button type="button" className="font-semibold underline" onClick={() => void generate()}>{t('common.retry')}</button>
          </div>
        )}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button icon={<FileText size={17} />} loading={busy} disabled={!Object.values(picked).some(Boolean)} onClick={() => void generate()}>{t('stats.pdf.download')}</Button>
        </div>
      </div>
    </Modal>
  )
}

function print(html: string) {
  const frame = document.createElement('iframe')
  frame.style.cssText = 'position:fixed;width:0;height:0;border:0;right:0;bottom:0'
  document.body.appendChild(frame)
  const doc = frame.contentDocument!
  doc.open()
  doc.write(html)
  doc.close()
  setTimeout(() => {
    frame.contentWindow!.focus()
    frame.contentWindow!.print()
    setTimeout(() => frame.remove(), 1500)
  }, 250)
}

function reportHtml({ title, overview: o, trend, tabData, tab, picked, storeNames, t }: {
  title: string; overview: Overview; trend: SalesTrend | null; tabData: AnalyticsTabData | null; tab: AnalyticsTab
  picked: Record<Block, boolean>; storeNames: string[]; t: Translator
}) {
  const kpiName: Record<string, TranslationKey> = {
    revenue: 'stats.kpi.revenueSales', salesCount: 'stats.kpi.salesCount', averageCheck: 'stats.kpi.averageCheck', returns: 'stats.kpi.returns',
    netSales: 'stats.kpi.netSales', customersInBase: 'stats.kpi.customersInBase', repeatCustomers: 'stats.kpi.repeatCustomers',
    bonusRedeemed: 'stats.kpi.bonusRedeemed', stock: 'stats.kpi.stock', customerDebt: 'stats.kpi.customerDebt',
  }
  const kpiKind: Record<string, 'money' | 'count' | 'points' | 'qty'> = { salesCount: 'count', returns: 'count', customersInBase: 'count', repeatCustomers: 'count', bonusRedeemed: 'points', stock: 'qty' }
  const table = (head: string[], rows: string[][], right: number[] = []) =>
    `<table><thead><tr>${head.map((h, i) => `<th class="${right.includes(i) ? 'r' : ''}">${esc(h)}</th>`).join('')}</tr></thead><tbody>${
      rows.map((r) => `<tr>${r.map((c, i) => `<td class="${right.includes(i) ? 'r' : ''}">${esc(c)}</td>`).join('')}</tr>`).join('')}</tbody></table>`
  const parts: string[] = []
  if (picked.kpis) {
    parts.push(`<h2>${esc(t('stats.pdf.block.kpis'))}</h2><div class="kpis">${o.kpis.map((k) => `
      <div class="kpi"><div class="l">${esc(t(kpiName[k.key] ?? (k.key as TranslationKey)))}</div>
      <div class="v">${!k.available || k.value === null ? esc(t('stats.unavailable')) : esc(formatKind(k.value, kpiKind[k.key] ?? 'money'))}${k.amount !== null ? ` · ${esc(tenge(k.amount))}` : ''}</div>
      ${k.changePercent !== null ? `<div class="c ${k.changePercent >= 0 ? 'up' : 'down'}">${k.changePercent > 0 ? '+' : ''}${num(k.changePercent)}%</div>` : ''}</div>`).join('')}</div>`)
  }
  if (picked.trend && trend) {
    parts.push(`<h2>${esc(t('stats.trend'))} <span class="m">(${esc(t(`stats.gran.${trend.granularity}` as TranslationKey))})</span></h2>` + table(
      [t('stats.c.bucketStart'), t('stats.s.revenue'), t('stats.s.salesCount'), t('stats.s.returns'), t('stats.k.netSales')],
      trend.data.map((d) => [periodText(d.bucketStart, d.bucketEnd), tenge(d.revenue), num(d.salesCount), tenge(d.returns), tenge(d.netSales)]), [1, 2, 3, 4]))
  }
  if (picked.payments) {
    parts.push(`<h2>${esc(t('stats.payments'))}</h2>` + table([t('stats.col.method'), '%', t('stats.col.sum')],
      o.paymentMethods.items.map((m) => [t(`pay.${m.method}` as TranslationKey), `${num(m.percent)}%`, tenge(m.amount)]), [1, 2]))
  }
  if (picked.products) {
    parts.push(`<h2>${esc(t('stats.topProducts'))}</h2>` + table([t('stats.col.product'), t('stats.col.qty'), t('stats.col.sum')],
      o.topProducts.byAmount.map((p) => [p.name, num(p.quantity), tenge(p.amount)]), [1, 2]))
  }
  if (picked.customers) {
    const c = o.customers
    parts.push(`<h2>${esc(t('stats.customersBlock'))}</h2>` + table([t('stats.col.metric'), t('stats.col.value')], [
      [t('stats.newCustomers'), num(c.new)], [t('stats.repeatCustomers'), num(c.repeat)],
      [t('stats.returningRate'), c.returningRate === null ? '—' : `${num(c.returningRate)}% (${num(c.repeat)} / ${num(c.buyers)})`],
      [t('stats.unidentifiedShort'), num(c.unidentifiedReceipts)],
    ], [1]))
  }
  if (picked.summary) {
    const rows: string[][] = [
      [t('stats.inv.withStock'), num(o.inventory.withStock)], [t('stats.inv.withoutStock'), num(o.inventory.withoutStock)], [t('stats.inv.belowMin'), num(o.inventory.belowMin)],
      [t('stats.sup.purchases'), tenge(o.suppliers.purchases)], [t('stats.sup.paid'), tenge(o.suppliers.paid)], [t('stats.sup.debt'), tenge(o.suppliers.debt)],
    ]
    if (o.finance) rows.push([t('stats.fin.inflow'), tenge(o.finance.inflow)], [t('stats.fin.outflow'), tenge(o.finance.outflow)], [t('stats.fin.net'), tenge(o.finance.netFlow)])
    parts.push(`<h2>${esc(t('stats.pdf.block.summary'))}</h2>` + table([t('stats.col.metric'), t('stats.col.value')], rows, [1]))
  }
  if (picked.tab && tabData?.table) {
    const cols = tabData.table.columns
    const numeric = cols.map((c, i) => (['text', 'period', 'enum', 'datetime', 'date'].includes(c.kind) ? -1 : i)).filter((i) => i >= 0)
    parts.push(`<h2>${esc(t(`stats.tab.${tab}` as TranslationKey))}</h2>` + table(cols.map((c) => t(`stats.c.${c.key}` as TranslationKey)),
      tabData.table.rows.map((r) => cols.map((c) => c.kind === 'period' ? periodText(String(r.bucketStart), String(r.bucketEnd))
        : c.kind === 'datetime' ? (r[c.key] ? dateTime(String(r[c.key])) : '—')
        : c.kind === 'enum' ? (r[c.key] ? t((c.key === 'method' ? `pay.${r[c.key]}` : `stats.enum.${r[c.key]}`) as TranslationKey) : '—')
        : c.kind === 'text' ? String(r[c.key] ?? '—') : formatKind(r[c.key] as number | null, c.kind))), numeric))
  }
  return `<!doctype html><html><head><meta charset="utf-8"><title>${esc(title)}</title><style>
    @page { size: A4; margin: 14mm }
    body { font: 11px/1.4 Inter, -apple-system, Segoe UI, Roboto, sans-serif; color: #0b1f44; margin: 0 }
    h1 { font-size: 20px; margin: 0 } h2 { font-size: 14px; margin: 18px 0 6px; border-bottom: 1px solid #e4ecf7; padding-bottom: 4px }
    .m, .meta { color: #66758f; font-weight: 400 } .meta { margin: 4px 0 8px }
    table { width: 100%; border-collapse: collapse } th { text-align: left; color: #66758f; font-weight: 600; background: #f4f8fd }
    th, td { padding: 4px 6px; border-bottom: 1px solid #eef2f8 } .r { text-align: right; white-space: nowrap } tr { page-break-inside: avoid }
    .kpis { display: grid; grid-template-columns: repeat(5, 1fr); gap: 6px } .kpi { border: 1px solid #e4ecf7; border-radius: 8px; padding: 6px 8px }
    .kpi .l { color: #66758f; font-size: 10px } .kpi .v { font-size: 13px; font-weight: 800 } .up { color: #12a86b } .down { color: #e5484d } .c { font-size: 10px; font-weight: 600 }
  </style></head><body>
    <h1>${esc(title)}</h1>
    <div class="meta">${esc(periodText(o.meta.from, o.meta.to))} · ${esc(storeNames.join(', '))} · ${esc(t('stats.updatedAt', { time: dateTime(o.meta.generatedAt) }))}</div>
    ${parts.join('')}
  </body></html>`
}
