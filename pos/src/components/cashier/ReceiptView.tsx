import { useQuery } from '@tanstack/react-query'
import { Loader2, Printer, Undo2 } from 'lucide-react'
import { Button } from '../ui/Button'
import { Modal } from '../ui/Modal'
import { cashierApi } from '../../lib/cashierApi'
import type { Receipt } from '../../lib/cashierTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { formatPhoneInput } from '../../lib/phone'
import { useT, type Translator } from '../../lib/i18n'
import { StatusPill } from './ReceiptsPanel'

const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!)

/**
 * Чекті басып шығару (ТЗ «Касса» §20.8): 80 мм таспаға лайық қарапайым HTML, жасырын iframe арқылы.
 * Фискалдық чек емес — ол кейін ОФД интеграциясымен.
 */
/** Чектің HTML-і (80 мм таспа): басып шығаруға да, бөлек терезеде ашуға да (PDF етіп сақтау). */
export function receiptHtml(r: Receipt, t: Translator) {
  const rows = r.items.map((i, n) => `
    <tr><td colspan="3">${n + 1}. ${esc(i.name)}${i.article ? ` <span class="muted">${esc(i.article)}</span>` : ''}</td></tr>
    <tr><td class="muted">${num(i.quantity)} × ${tenge(i.price)}</td><td></td><td class="r">${tenge(i.lineTotal)}</td></tr>`).join('')
  const line = (label: string, value: string, cls = '') => `<tr class="${cls}"><td colspan="2">${label}</td><td class="r">${value}</td></tr>`
  const html = `<!doctype html><html><head><meta charset="utf-8"><title>${t('pos.receiptNo', { n: r.number })}</title><style>
    @page { size: 80mm auto; margin: 4mm }
    body { font: 12px/1.35 -apple-system, Segoe UI, Roboto, sans-serif; color: #000; width: 72mm; margin: 0 }
    h1 { font-size: 16px; text-align: center; margin: 0 0 2px } .c { text-align: center } .muted { color: #555; font-size: 11px }
    table { width: 100%; border-collapse: collapse } td { padding: 1px 0; vertical-align: top } .r { text-align: right; white-space: nowrap }
    hr { border: 0; border-top: 1px dashed #000; margin: 6px 0 } .total td { font-size: 16px; font-weight: 700; padding-top: 4px }
  </style></head><body>
    <h1>${esc(r.storeName)}</h1>
    <div class="c">${t('pos.receiptNo', { n: r.number })}</div>
    <div class="c muted">${dateTime(r.createdAt)} · ${esc(r.registerName)}</div>
    <div class="c muted">${t('receipt.cashier')}: ${esc(r.cashierName)}</div>
    <hr><table>${rows}</table><hr><table>
    ${r.discountAmount > 0 || r.bonusRedeemed > 0 ? line(t('pos.subtotal'), tenge(r.subtotal)) : ''}
    ${r.discountAmount > 0 ? line(t('pos.discount'), `− ${tenge(r.discountAmount)}`) : ''}
    ${r.bonusRedeemed > 0 ? line(t('pos.bonusUsed'), `− ${tenge(r.bonusRedeemed)}`) : ''}
    ${line(t('receipt.total'), tenge(r.total), 'total')}
    ${r.payments.map((p) => line(t(`pay.${p.method}`), tenge(p.amount))).join('')}
    ${r.payments.filter((p) => p.received != null && p.change).map((p) => line(t('pay.received'), tenge(p.received!)) + line(t('pay.change'), tenge(p.change!))).join('')}
    </table>
    ${r.customer ? `<hr><div>${t('pos.customer')}: ${esc(r.customer.fullName)}</div><div class="muted">${t('pos.accrued')}: +${num(r.bonusAccrued)} Б</div>` : ''}
    ${r.returnedAmount > 0 ? `<hr><div>${t('receipt.returned')}: ${tenge(r.returnedAmount)}</div>` : ''}
    <hr><div class="c">${t('receipt.thanks')}</div>
  </body></html>`
  return html
}

/** «Электронный чек» клиентсіз чекке: бөлек қойындыда ашылады, браузердан PDF етіп сақтауға болады. */
export function openReceipt(r: Receipt, t: Translator) {
  const w = window.open('', '_blank')
  if (!w) return
  w.document.open()
  w.document.write(receiptHtml(r, t))
  w.document.close()
}

export function printReceipt(r: Receipt, t: Translator) {
  const html = receiptHtml(r, t)

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
    setTimeout(() => frame.remove(), 1000)
  }, 150)
}

/** Чекті ашу (ТЗ §12.3): толық құрамы, клиент, жеңілдік, бонус, төлемдер, қарыз, қайтарулар. */
export function ReceiptModal({ saleId, canReturn, onClose, onReturn }: {
  saleId: string
  canReturn: boolean
  onClose: () => void
  onReturn: () => void
}) {
  const t = useT()
  const { data: r, error } = useQuery({ queryKey: ['cashier', 'sale', saleId], queryFn: () => cashierApi.sale(saleId) })

  return (
    <Modal title={r ? t('pos.receiptNo', { n: r.number }) : t('receipts.title')} onClose={onClose} width={520}>
      {error && <p className="text-[14px] text-danger">{error.message}</p>}
      {!r && !error && <div className="flex justify-center py-10 text-ink-3"><Loader2 className="animate-spin" /></div>}
      {r && (
        <div className="flex flex-col gap-4">
          <div className="rounded-2xl border border-line bg-white px-5 py-4 text-[13px] shadow-sm">
            <div className="text-center">
              <div className="text-[17px] font-bold">{r.storeName}</div>
              <div className="mt-1 font-semibold">{t('pos.receiptNo', { n: r.number })} <StatusPill status={r.status} /></div>
              <div className="text-ink-2">{dateTime(r.createdAt)} · {r.registerName}</div>
              <div className="text-ink-2">{t('receipt.cashier')}: {r.cashierName}</div>
            </div>
            <div className="my-3 border-t border-dashed border-ink-3" />
            <table className="w-full">
              <thead className="text-[12px] text-ink-3">
                <tr><th className="pb-1 text-left font-medium">{t('receipt.item')}</th><th className="pb-1 pl-3 text-right font-medium">{t('product.quantity')}</th>
                  <th className="pb-1 pl-3 text-right font-medium">{t('receipt.price')}</th><th className="pb-1 pl-3 text-right font-medium">{t('pos.subtotal')}</th></tr>
              </thead>
              <tbody>
                {r.items.map((i) => (
                  <tr key={i.id} className="align-top">
                    <td className="py-1 pr-2">
                      <div className="font-medium">{i.name}</div>
                      {i.article && <div className="text-[11px] text-ink-3">{t('pos.article', { a: i.article })}</div>}
                      {i.returnedQuantity > 0 && <div className="text-[11px] text-danger">{t('receipt.returnedQty', { n: num(i.returnedQuantity) })}</div>}
                    </td>
                    <td className="py-1 pl-3 text-right">{num(i.quantity)}</td>
                    <td className="py-1 pl-3 text-right whitespace-nowrap">{tenge(i.price)}</td>
                    <td className="py-1 pl-3 text-right font-medium whitespace-nowrap">{tenge(i.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="my-3 border-t border-dashed border-ink-3" />
            <div className="space-y-1">
              {(r.discountAmount > 0 || r.bonusRedeemed > 0) && <Line label={t('pos.subtotal')} value={tenge(r.subtotal)} />}
              {r.discountAmount > 0 && <Line label={`${t('pos.discount')}${r.discountKind === 'Percent' ? ` ${num(r.discountValue ?? 0)}%` : ''}`} value={`− ${tenge(r.discountAmount)}`} />}
              {r.bonusRedeemed > 0 && <Line label={t('pos.bonusUsed')} value={`− ${tenge(r.bonusRedeemed)}`} />}
              <div className="flex justify-between pt-1 text-[18px] font-extrabold"><span>{t('receipt.total')}</span><span>{tenge(r.total)}</span></div>
              {r.payments.map((p) => (
                <Line key={p.method} label={t(`pay.${p.method}`)} value={tenge(p.amount)}
                  hint={p.transferRecipient ?? (p.change ? `${t('pay.received')} ${tenge(p.received!)} · ${t('pay.change')} ${tenge(p.change)}` : undefined)} />
              ))}
            </div>
            {r.customer && (
              <>
                <div className="my-3 border-t border-dashed border-ink-3" />
                <Line label={t('pos.customer')} value={r.customer.fullName} hint={formatPhoneInput(r.customer.phone)} />
                <Line label={t('pos.accrued')} value={`+${num(r.bonusAccrued)} Б`} />
              </>
            )}
            {r.debt && (
              <div className="mt-3 rounded-xl bg-amber-50 px-3 py-2 text-amber-800">
                {t('receipt.debt', { sum: tenge(r.debt.amount), date: dateOnly(r.debt.dueDate) })}
                {' · '}{r.debt.status === 'Paid' ? t('debt.status.Paid') : t('receipt.debtLeft', { sum: tenge(r.debt.remaining) })}
              </div>
            )}
          </div>

          {r.returns.length > 0 && (
            <div className="rounded-xl border border-line p-3 text-[13px]">
              <div className="mb-2 font-semibold">{t('receipt.returns')}</div>
              {r.returns.map((x) => (
                <div key={x.id} className="border-t border-line py-2 first:border-0 first:pt-0">
                  <div className="flex justify-between"><span className="text-ink-2">{dateTime(x.createdAt)} · {x.cashierName}</span><b className="text-danger">− {tenge(x.amount)}</b></div>
                  <div className="text-ink-2">{x.items.map((i) => `${i.name} × ${num(i.quantity)}`).join(', ')}</div>
                  <div className="text-[12px] text-ink-3">
                    {[x.refunded > 0 && x.refundMethod && `${t(`pay.${x.refundMethod}`)}: ${tenge(x.refunded)}`,
                      x.debtReduced > 0 && t('receipt.debtReduced', { sum: tenge(x.debtReduced) }),
                      x.reason].filter(Boolean).join(' · ')}
                  </div>
                </div>
              ))}
            </div>
          )}

          <div className="grid grid-cols-3 gap-2">
            <Button variant="secondary" icon={<Printer size={17} />} onClick={() => printReceipt(r, t)}>{t('receipts.print')}</Button>
            <Button variant="secondary" className="text-danger!" icon={<Undo2 size={17} />}
              disabled={!canReturn || r.status === 'Returned'} onClick={onReturn}>{t('receipts.return')}</Button>
            <Button onClick={onClose}>{t('common.close')}</Button>
          </div>
        </div>
      )}
    </Modal>
  )
}

function Line({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="flex justify-between gap-3">
      <span className="text-ink-2">{label}{hint && <span className="block text-[11px] text-ink-3">{hint}</span>}</span>
      <span className="text-right font-medium">{value}</span>
    </div>
  )
}

