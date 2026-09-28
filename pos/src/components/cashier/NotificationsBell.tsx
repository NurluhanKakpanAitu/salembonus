import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, Bell, HandCoins, PackageX, Undo2, UserRoundCheck } from 'lucide-react'
import { activeStore, can, useAuth } from '../../lib/auth'
import { cashierApi } from '../../lib/cashierApi'
import type { NotificationType, StoreNotification } from '../../lib/cashierTypes'
import { dateTime } from '../../lib/dates'
import { num, tenge } from '../../lib/money'
import { useT, type Translator } from '../../lib/i18n'

const ICON: Record<NotificationType, { icon: typeof Bell; tone: string }> = {
  OutOfStock: { icon: PackageX, tone: 'bg-danger-soft text-danger' },
  LowStock: { icon: AlertTriangle, tone: 'bg-amber-100 text-amber-700' },
  DebtCreated: { icon: HandCoins, tone: 'bg-amber-100 text-amber-700' },
  DebtRepaid: { icon: HandCoins, tone: 'bg-success-soft text-success' },
  SaleReturn: { icon: Undo2, tone: 'bg-danger-soft text-danger' },
  CashierChange: { icon: UserRoundCheck, tone: 'bg-brand-soft text-brand' },
}

/** Хабарлама мәтіні сервердегі деректен қызметкердің тілінде құрастырылады. */
function text(n: StoreNotification, t: Translator) {
  const amount = n.amount ?? 0
  switch (n.type) {
    case 'OutOfStock': return t('notify.OutOfStock', { name: n.subject ?? '' })
    case 'LowStock': return t('notify.LowStock', { name: n.subject ?? '', n: num(amount) })
    case 'DebtCreated': return t('notify.DebtCreated', { sum: tenge(amount), name: n.subject ?? '', n: n.number ?? '' })
    case 'DebtRepaid': return t('notify.DebtRepaid', { sum: tenge(amount), name: n.subject ?? '' })
    case 'SaleReturn': return t('notify.SaleReturn', { sum: tenge(amount), n: n.number ?? '' })
    case 'CashierChange': return t('notify.CashierChange', { name: n.subject ?? '', register: n.staffName ?? '' })
  }
}

const seenKey = (storeId: string) => `salempos.notifications.seen.${storeId}`

/**
 * Дүкен хабарламалары (ТЗ «Касса» §19): тауар таусылды/аз қалды, қарыз, қайтару, кассир ауысуы.
 * Минут сайын жаңарады; оқылмағаны — осы құрылғыда соңғы ашылғаннан кейінгілер.
 */
export function NotificationsBell() {
  const t = useT()
  const store = useAuth(activeStore)
  const enabled = !!store && can(store, 'sales.create')
  const [open, setOpen] = useState(false)
  const [seen, setSeen] = useState<string>(() => {
    try { return store ? localStorage.getItem(seenKey(store.id)) ?? '' : '' } catch { return '' }
  })
  const ref = useRef<HTMLDivElement>(null)
  const { data = [] } = useQuery({
    queryKey: ['cashier', 'notifications', store?.id],
    queryFn: cashierApi.notifications,
    enabled,
    refetchInterval: 60_000,
  })
  const unread = data.filter((n) => n.createdAt > seen).length

  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [open])

  if (!enabled) return null

  const toggle = () => {
    setOpen((o) => !o)
    if (!open && data[0] && store) {
      setSeen(data[0].createdAt)
      try { localStorage.setItem(seenKey(store.id), data[0].createdAt) } catch { /* келесіде қайта «оқылмаған» болып көрінеді */ }
    }
  }

  return (
    <div ref={ref} className="relative">
      <button type="button" aria-label={t('top.notifications')} onClick={toggle}
        className="relative flex size-11 items-center justify-center rounded-xl border border-line bg-surface text-ink-2 hover:bg-field">
        <Bell size={19} />
        {unread > 0 && (
          <span className="absolute -right-1 -top-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-danger px-1 text-[11px] font-bold text-white">
            {unread > 9 ? '9+' : unread}
          </span>
        )}
      </button>
      {open && (
        <div className="absolute right-0 z-30 mt-2 w-[360px] overflow-hidden rounded-2xl border border-line bg-surface shadow-xl">
          <div className="border-b border-line px-4 py-3 text-[15px] font-bold">{t('top.notifications')}</div>
          <ul className="max-h-[420px] overflow-y-auto">
            {data.length === 0 && <li className="px-4 py-8 text-center text-[14px] text-ink-3">{t('notify.empty')}</li>}
            {data.map((n) => {
              const { icon: Icon, tone } = ICON[n.type]
              return (
                <li key={n.id} className="flex gap-3 border-b border-line px-4 py-3 last:border-0">
                  <span className={`flex size-9 shrink-0 items-center justify-center rounded-full ${tone}`}><Icon size={17} /></span>
                  <div className="min-w-0">
                    <div className="text-[14px] leading-5">{text(n, t)}</div>
                    <div className="mt-0.5 text-[12px] text-ink-3">
                      {dateTime(n.createdAt)}{n.staffName && n.type !== 'CashierChange' && ` · ${n.staffName}`}
                    </div>
                  </div>
                </li>
              )
            })}
          </ul>
        </div>
      )}
    </div>
  )
}
