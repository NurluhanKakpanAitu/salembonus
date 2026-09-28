import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Banknote, BarChart3, CreditCard, Gift, HandCoins, Info, Landmark, Percent, QrCode, ShoppingCart, Shuffle, Undo2, Wallet, X } from 'lucide-react'
import { cashierApi } from '../../lib/cashierApi'
import type { Finance, PaymentMethod } from '../../lib/cashierTypes'
import { addDays, dateOnly, isoDate } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'

type Period = 'today' | 'yesterday' | 'week' | 'month'

function range(p: Period) {
  const today = new Date()
  switch (p) {
    case 'yesterday': return { from: isoDate(addDays(today, -1)), to: isoDate(addDays(today, -1)) }
    case 'week': return { from: isoDate(addDays(today, -6)), to: isoDate(today) }
    case 'month': return { from: isoDate(addDays(today, -29)), to: isoDate(today) }
    default: return { from: isoDate(today), to: isoDate(today) }
  }
}

const ICON: Record<PaymentMethod, typeof Banknote> = { Cash: Banknote, Card: CreditCard, Qr: QrCode, Transfer: Landmark, Debt: HandCoins }
const TONE: Record<PaymentMethod, string> = {
  Cash: 'text-success', Card: 'text-brand', Qr: 'text-violet-600', Transfer: 'text-sky-600', Debt: 'text-amber-600',
}

/**
 * «Финансы» (ТЗ «Касса» §14): корзина мен клиент жоқ, тек қаржы блогы. Сатылым, төлем түрлері,
 * аралас төлем, жеңілдік, бонус, қайтарым, қарыз және итог жеке көрінеді (AC-11).
 */
export function FinancePanel({ onClose }: { onClose: () => void }) {
  const t = useT()
  const [period, setPeriod] = useState<Period>('today')
  const { from, to } = range(period)
  const { data: f, isLoading } = useQuery({
    queryKey: ['cashier', 'finance', from, to],
    queryFn: () => cashierApi.finance(from, to),
    placeholderData: keepPreviousData,
  })

  return (
    <aside className="flex min-h-0 w-full shrink-0 flex-col rounded-2xl bg-surface lg:w-[520px] xl:w-[600px]">
      <div className="flex items-center justify-between border-b border-line px-4 py-3">
        <div>
          <h2 className="text-[18px] font-bold">{t('finance.title')}</h2>
          {f && <p className="text-[12px] text-ink-3">{f.wholeStore ? t('finance.wholeStore') : t('finance.ownOnly')} · {dateOnly(f.from)}{f.to !== f.from && ` — ${dateOnly(f.to)}`}</p>}
        </div>
        <button type="button" aria-label={t('common.close')} onClick={onClose}
          className="flex size-9 items-center justify-center rounded-lg text-ink-2 hover:bg-field"><X size={19} /></button>
      </div>
      <div className="border-b border-line p-3">
        <div className="grid grid-cols-4 gap-1 rounded-xl bg-field p-1">
          {(['today', 'yesterday', 'week', 'month'] as const).map((p) => (
            <button key={p} type="button" onClick={() => setPeriod(p)}
              className={`rounded-lg py-1.5 text-[13px] font-medium ${period === p ? 'bg-brand text-white' : 'text-ink-2 hover:text-ink'}`}>
              {t(`receipts.period.${p}` as TranslationKey)}
            </button>
          ))}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto p-3">
        {isLoading || !f ? <p className="py-10 text-center text-ink-3">{t('common.loading')}</p> : <FinanceBody f={f} />}
      </div>
    </aside>
  )
}

function FinanceBody({ f }: { f: Finance }) {
  const t = useT()
  const methods = f.methods.filter((m) => m.amount > 0 || m.method !== 'Debt')
  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-4 rounded-2xl border border-line p-4">
        <span className="flex size-12 items-center justify-center rounded-xl bg-success-soft text-success"><ShoppingCart size={24} /></span>
        <div>
          <div className="text-[13px] text-ink-2">{t('finance.sales')}</div>
          <div className="text-[26px] font-extrabold tracking-tight">{tenge(f.salesTotal)}</div>
          <div className="text-[12px] text-ink-3">{t('finance.receipts', { n: f.salesCount })}</div>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
        {methods.map((m) => {
          const Icon = ICON[m.method]
          return <Tile key={m.method} icon={<Icon size={18} className={TONE[m.method]} />} label={t(`pay.${m.method}`)} value={tenge(m.amount)} hint={t('finance.receipts', { n: m.count })} />
        })}
        <Tile icon={<Shuffle size={18} className="text-ink-2" />} label={t('pay.Mixed')} value={tenge(f.mixedTotal)} hint={t('finance.receipts', { n: f.mixedCount })} />
        <Tile icon={<Undo2 size={18} className="text-danger" />} label={t('finance.returns')} value={tenge(f.returnsTotal)} hint={t('finance.receipts', { n: f.returnsCount })} />
        <Tile icon={<Percent size={18} className="text-violet-600" />} label={t('finance.discounts')} value={tenge(f.discounts)} hint={t('finance.receipts', { n: f.discountCount })} />
        <Tile icon={<Gift size={18} className="text-danger" />} label={t('finance.bonusUsed')} value={`${num(f.bonusRedeemed)} Б`} hint={`≈ ${tenge(f.bonusRedeemed)}`} />
      </div>

      <div className="flex items-center gap-4 rounded-2xl bg-brand-soft px-4 py-3.5">
        <BarChart3 size={26} className="text-brand" />
        <div>
          <div className="text-[13px] font-medium text-brand">{t('finance.revenue')}</div>
          <div className="text-[24px] font-extrabold tracking-tight">{tenge(f.revenue)}</div>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <Tile icon={<Wallet size={18} className="text-success" />} label={t('finance.cashInDrawer')} value={tenge(f.cashInDrawer)} hint={t('finance.cashHint')} />
        <Tile icon={<HandCoins size={18} className="text-amber-600" />} label={t('finance.debtRepaid')} value={tenge(f.debtRepaid)}
          hint={f.debtRepaidByMethod.filter((m) => m.amount > 0).map((m) => `${t(`pay.${m.method}`)} ${tenge(m.amount)}`).join(' · ') || '—'} />
      </div>

      {f.refundsByMethod.some((m) => m.amount > 0) && (
        <div className="rounded-xl border border-line px-4 py-3 text-[13px]">
          <div className="mb-1 font-semibold">{t('finance.refunds')}</div>
          {f.refundsByMethod.filter((m) => m.amount > 0).map((m) => (
            <div key={m.method} className="flex justify-between text-ink-2"><span>{t(`pay.${m.method}`)}</span><span>− {tenge(m.amount)}</span></div>
          ))}
        </div>
      )}

      <div>
        <div className="mb-2 text-[14px] font-bold">SalemBonus</div>
        <div className="grid grid-cols-2 gap-2">
          <Tile icon={<Gift size={18} className="text-success" />} label={t('finance.bonusAccrued')} value={`${num(f.bonusAccrued)} Б`} />
          <Tile icon={<Gift size={18} className="text-danger" />} label={t('finance.bonusUsed')} value={`${num(f.bonusRedeemed)} Б`} />
        </div>
      </div>

      <p className="flex items-start gap-2 rounded-xl bg-field px-3 py-2.5 text-[12px] text-ink-2">
        <Info size={15} className="mt-0.5 shrink-0" /> {t('finance.note')}
      </p>
    </div>
  )
}

function Tile({ icon, label, value, hint }: { icon: React.ReactNode; label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-xl border border-line px-3 py-2.5">
      <div className="flex items-center gap-1.5 text-[12px] text-ink-2">{icon} {label}</div>
      <div className="mt-1 text-[17px] font-bold">{value}</div>
      {hint && <div className="truncate text-[11px] text-ink-3">{hint}</div>}
    </div>
  )
}
