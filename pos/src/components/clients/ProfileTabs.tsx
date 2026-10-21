import { useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { BellOff, FileText, HandCoins, Loader2, MessageSquare, Pencil, Receipt, RotateCcw, ShoppingBag, Smartphone, Trash2, Undo2 } from 'lucide-react'
import { Button } from '../ui/Button'
import { confirmDialog } from '../ui/Confirm'
import { Select, Textarea } from '../ui/Field'
import { toast } from '../ui/Toast'
import { SaleCard } from '../sales/SaleCard'
import { CustomerPaymentModal } from '../finance/CustomerDebtsTab'
import { DebtBadge } from '../finance/financeUi'
import { OpTypeBadge, Pill, Points, StatusBadge } from '../bonus/bonusUi'
import { ApiError } from '../../lib/api'
import { bonusApi } from '../../lib/bonusApi'
import { clientKeys, clientsApi } from '../../lib/clientsApi'
import { NOTE_CATEGORIES, type ClientCard, type ClientMonth, type NoteCategory } from '../../lib/clientsTypes'
import { dateOnly, dateTime } from '../../lib/dates'
import { financeApi } from '../../lib/financeApi'
import type { DebtStatus } from '../../lib/financeTypes'
import { activeStore, can, useAuth } from '../../lib/auth'
import { Permission } from '../../lib/types'
import { num, tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { lastPurchaseText } from './ClientModals'
import { ActivityPill, InfoRow, Section, clientNo } from './clientsUi'

const errText = (err: unknown, fallback: string) => (err instanceof ApiError ? err.message : fallback)
const Spinner = () => <div className="py-12 text-center text-ink-3"><Loader2 className="mx-auto animate-spin" size={22} /></div>
const EmptyText = ({ text }: { text: string }) => <p className="py-10 text-center text-[13px] text-ink-3">{text}</p>

function Stat({ label, value, tone }: { label: string; value: React.ReactNode; tone?: 'danger' }) {
  return (
    <div className="min-w-0 rounded-xl border border-line px-3 py-2">
      <div className="truncate text-[12px] text-ink-2">{label}</div>
      <div className={`mt-0.5 truncate text-[15px] font-bold ${tone === 'danger' ? 'text-danger' : ''}`}>{value}</div>
    </div>
  )
}

/** Чек карточкасы профиль үстінде (оң жақ панель): «Продажи» бөліміндегідей. */
export function SaleOverlay({ saleId, onClose }: { saleId: string; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-[60] flex justify-end bg-black/20" onMouseDown={onClose}>
      <div className="flex h-full w-full max-w-[560px] flex-col p-3" onMouseDown={(e) => e.stopPropagation()}>
        <SaleCard saleId={saleId} canReturn={false} onClose={onClose} onReturn={onClose} />
      </div>
    </div>
  )
}

const RISK_TONE = { none: 'gray', low: 'green', medium: 'amber', high: 'red' } as const

// ---------- Обзор ----------

export function OverviewPane({ card, onSale }: { card: ClientCard; onSale: (id: string) => void }) {
  const t = useT()
  const c = card.client
  const a = card.activitySummary
  const age = c.birthDate ? Math.floor((Date.now() - new Date(c.birthDate).getTime()) / (365.25 * 86400_000)) : null
  return (
    <div className="flex flex-col gap-5">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
        <Stat label={t('cli.col.purchases')} value={num(c.purchases)} />
        <Stat label={t('cli.col.amount')} value={tenge(c.amount)} />
        <Stat label={t('cli.col.average')} value={tenge(c.average)} />
        <Stat label={t('cli.col.lastPurchase')} value={lastPurchaseText(t, c.lastPurchaseAt, c.daysSinceLastPurchase)} />
        <Stat label={t('cli.col.bonus')} value={<Points value={c.bonus} />} />
        <Stat label={t('cli.col.debt')} value={tenge(c.debt)} tone={c.overdue > 0 ? 'danger' : undefined} />
      </div>
      <div className="flex flex-wrap items-center gap-2 rounded-xl bg-field px-3 py-2 text-[13px]">
        <ActivityPill activity={a.segment} />
        <span className="text-ink-2">{a.averageInterval !== null ? t('cli.prof.interval', { n: a.averageInterval }) : t('cli.prof.noInterval')}</span>
        <span className="ml-auto flex items-center gap-1.5 text-ink-2">{t('cli.prof.risk')}: <Pill tone={RISK_TONE[a.churnRisk]}>{t(`cli.risk.${a.churnRisk}` as TranslationKey)}</Pill></span>
      </div>
      <Section title={t('cli.prof.contacts')}>
        <div className="divide-y divide-line rounded-xl border border-line px-3">
          <InfoRow label={t('cli.col.phone')} value={c.phone} />
          <InfoRow label={t('cli.col.email')} value={c.email} />
          <InfoRow label={t('cli.col.birthDate')} value={c.birthDate ? `${dateOnly(c.birthDate)}${age !== null ? ` · ${t('cli.prof.age', { n: age })}` : ''}` : null} />
          <InfoRow label={t('cli.col.gender')} value={t(`cli.gender.${c.gender}` as TranslationKey)} />
          <InfoRow label={t('cli.form.settlement')} value={card.kato.length ? card.kato.map((k) => k.name).join(', ') : null} />
          <InfoRow label={t('cli.form.street')} value={card.address} />
          <InfoRow label={t('cli.col.source')} value={t(`cli.source.${c.source}` as TranslationKey)} />
          <InfoRow label={t('cli.col.registered')} value={`${dateOnly(c.registeredAt)}${card.createdBy ? ` · ${card.createdBy}` : ''}`} />
          <InfoRow label={t('cli.prof.firstPurchase')} value={c.firstPurchaseAt ? dateOnly(c.firstPurchaseAt) : null} />
        </div>
      </Section>
      <Section title={t('cli.form.consents')}>
        <div className="flex flex-wrap gap-2">
          {(['app', 'sms', 'whatsapp', 'email'] as const).map((k) => {
            const on = k === 'app' ? c.consentApp : k === 'sms' ? c.consentSms : k === 'whatsapp' ? c.consentWhatsApp : c.consentEmail
            return <Pill key={k} tone={on ? 'green' : 'gray'}>{t(`cli.channel.${k}` as TranslationKey)}: {on ? t('cli.f.yes') : t('cli.f.no')}</Pill>
          })}
          {c.hasApp && <Pill tone="blue"><Smartphone size={12} className="mr-1 inline" />{t('cli.prof.inApp')}</Pill>}
          {card.bonus.optOut && <Pill tone="gray"><BellOff size={12} className="mr-1 inline" />{t('cli.prof.optOut')}</Pill>}
        </div>
        {card.consentUpdatedAt && <p className="text-[12px] text-ink-3">{t('cli.prof.consentUpdated', { date: dateTime(card.consentUpdatedAt) })}</p>}
      </Section>
      {Object.keys(c.customFields).length > 0 && (
        <Section title={t('cli.form.custom')}>
          <div className="divide-y divide-line rounded-xl border border-line px-3">
            {Object.entries(c.customFields).map(([k, v]) => <InfoRow key={k} label={k} value={v} />)}
          </div>
        </Section>
      )}
      {card.comment && <Section title={t('cli.form.comment')}><p className="whitespace-pre-wrap rounded-xl bg-field px-3 py-2 text-[13px]">{card.comment}</p></Section>}
      <Section title={t('cli.prof.topProducts')}>
        {card.topProducts.length === 0 ? <EmptyText text={t('cli.prof.noPurchases')} /> : (
          <ul className="divide-y divide-line rounded-xl border border-line">
            {card.topProducts.map((p) => (
              <li key={p.productId} className="flex items-center gap-3 px-3 py-2 text-[13px]">
                <span className="min-w-0 flex-1 truncate">{p.name}</span>
                <span className="text-ink-2">{num(p.quantity)} · {t('cli.prof.times', { n: p.purchases })}</span>
                <span className="w-24 text-right font-semibold">{tenge(p.amount)}</span>
              </li>
            ))}
          </ul>
        )}
      </Section>
      <Section title={t('cli.prof.recent')}>
        {card.recentSales.length === 0 ? <EmptyText text={t('cli.prof.noPurchases')} /> : (
          <ul className="divide-y divide-line rounded-xl border border-line">
            {card.recentSales.map((s) => (
              <li key={s.id}>
                <button type="button" onClick={() => onSale(s.id)} className="flex w-full items-center gap-3 px-3 py-2 text-left text-[13px] hover:bg-field">
                  <ShoppingBag size={15} className="text-ink-3" />
                  <span className="min-w-0 flex-1"><span className="block">№{s.number} · {dateTime(s.createdAt)}</span><span className="block truncate text-[12px] text-ink-3">{s.storeName} · {s.cashier}</span></span>
                  <span className="font-semibold">{tenge(s.total)}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </Section>
    </div>
  )
}

// ---------- Покупки ----------

const SALE_TONE = { Completed: 'green', PartiallyReturned: 'amber', Returned: 'red' } as const

export function PurchasesPane({ card, storeIds, onSale }: { card: ClientCard; storeIds?: string; onSale: (id: string) => void }) {
  const t = useT()
  const [page, setPage] = useState(1)
  const q = useQuery({ queryKey: ['clients', 'sales', card.client.id, storeIds, page], queryFn: () => clientsApi.sales(card.client.id, storeIds, page), placeholderData: keepPreviousData })
  if (!q.data) return <Spinner />
  if (q.data.total === 0) return <EmptyText text={t('cli.prof.noPurchases')} />
  const pages = Math.ceil(q.data.total / 20)
  return (
    <div className="flex flex-col gap-3">
      <ul className="divide-y divide-line rounded-xl border border-line">
        {q.data.rows.map((s) => (
          <li key={s.id}>
            <button type="button" onClick={() => onSale(s.id)} className="flex w-full items-center gap-3 px-3 py-2.5 text-left text-[13px] hover:bg-field">
              <span className="min-w-0 flex-1">
                <span className="flex items-center gap-2"><b>№{s.number}</b><Pill tone={SALE_TONE[s.status]}>{t(`cli.sale.${s.status}` as TranslationKey)}</Pill></span>
                <span className="block truncate text-[12px] text-ink-3">{dateTime(s.createdAt)} · {s.storeName} · {s.items} {t('cli.prof.pcs')} · {s.methods.map((m) => t(`pay.${m}` as TranslationKey)).join(', ')}</span>
                {(s.bonusAccrued > 0 || s.bonusRedeemed > 0) && <span className="block text-[12px] text-ink-2">{t('cli.prof.bonusLine', { plus: num(s.bonusAccrued), minus: num(s.bonusRedeemed) })}</span>}
              </span>
              <span className="text-right">
                <span className="block font-semibold">{tenge(s.total)}</span>
                {s.returned > 0 && <span className="block text-[12px] text-danger">−{tenge(s.returned)}</span>}
              </span>
            </button>
          </li>
        ))}
      </ul>
      {pages > 1 && (
        <div className="flex items-center justify-end gap-2 text-[13px]">
          <Button variant="secondary" className="h-9! px-3!" disabled={page <= 1} onClick={() => setPage(page - 1)}>‹</Button>
          <span>{page} / {pages}</span>
          <Button variant="secondary" className="h-9! px-3!" disabled={page >= pages} onClick={() => setPage(page + 1)}>›</Button>
        </div>
      )}
    </div>
  )
}

// ---------- Чеки ----------

export function ReceiptsPane({ card, storeIds, onSale }: { card: ClientCard; storeIds?: string; onSale: (id: string) => void }) {
  const t = useT()
  const q = useQuery({ queryKey: ['clients', 'receipts', card.client.id, storeIds], queryFn: () => clientsApi.receipts(card.client.id, storeIds) })
  if (!q.data) return <Spinner />
  if (q.data.length === 0) return <EmptyText text={t('cli.prof.noReceipts')} />
  return (
    <ul className="divide-y divide-line rounded-xl border border-line">
      {q.data.map((r) => (
        <li key={`${r.kind}-${r.id}`}>
          <button type="button" onClick={() => onSale(r.saleId)} className="flex w-full items-center gap-3 px-3 py-2.5 text-left text-[13px] hover:bg-field">
            {r.kind === 'sale' ? <Receipt size={16} className="text-brand" /> : <Undo2 size={16} className="text-danger" />}
            <span className="min-w-0 flex-1">
              <span className="block font-medium">{r.kind === 'sale' ? t('cli.prof.receipt', { n: r.number }) : t('cli.prof.return', { n: r.number })}</span>
              <span className="block truncate text-[12px] text-ink-3">{dateTime(r.createdAt)} · {r.storeName} · {r.staff}{r.comment ? ` · ${r.comment}` : ''}</span>
            </span>
            <span className={`font-semibold ${r.kind === 'return' ? 'text-danger' : ''}`}>{r.kind === 'return' ? '−' : ''}{tenge(r.amount)}</span>
          </button>
        </li>
      ))}
    </ul>
  )
}

// ---------- Долги и оплаты ----------

export function DebtsPane({ card, storeIds }: { card: ClientCard; storeIds?: string }) {
  const t = useT()
  const qc = useQueryClient()
  const store = useAuth(activeStore)
  const canPay = can(store, Permission.FinanceOperate)
  const canAccounts = can(store, Permission.FinanceView)
  const [paying, setPaying] = useState(false)
  const q = useQuery({ queryKey: ['clients', 'debts', card.client.id, storeIds], queryFn: () => clientsApi.clientDebts(card.client.id, storeIds) })
  const accounts = useQuery({ queryKey: ['finance', 'accounts', false], queryFn: () => financeApi.accounts(), enabled: paying && canAccounts })
  const d = q.data
  if (!d) return <Spinner />
  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-3 gap-2">
        <Stat label={t('cli.prof.debtTotal')} value={tenge(d.total)} />
        <Stat label={t('cli.prof.debtOverdue')} value={tenge(d.overdue)} tone={d.overdue > 0 ? 'danger' : undefined} />
        <Stat label={t('cli.prof.nextDue')} value={d.nextDue ? dateOnly(d.nextDue) : '—'} />
      </div>
      {canPay && d.total > 0 && <Button icon={<HandCoins size={16} />} onClick={() => setPaying(true)} className="self-start">{t('cli.prof.acceptPayment')}</Button>}
      <Section title={t('cli.prof.debtDocs')}>
        {d.documents.length === 0 ? <EmptyText text={t('cli.prof.noDebts')} /> : (
          <ul className="divide-y divide-line rounded-xl border border-line">
            {d.documents.map((x) => (
              <li key={x.debtId} className="flex items-center gap-3 px-3 py-2 text-[13px]">
                <span className="min-w-0 flex-1">
                  <span className="block">№{x.saleNumber ?? '—'} · {dateOnly(x.createdAt)}</span>
                  <span className={`block text-[12px] ${x.overdue ? 'text-danger' : 'text-ink-3'}`}>{t('cli.prof.dueDate', { date: dateOnly(x.dueDate) })}</span>
                </span>
                <span className="text-right"><span className="block font-semibold">{tenge(x.remaining)}</span><span className="block text-[12px] text-ink-3">{t('cli.prof.ofAmount', { amount: tenge(x.amount) })}</span></span>
                <DebtBadge status={x.status as DebtStatus} />
              </li>
            ))}
          </ul>
        )}
      </Section>
      <Section title={t('cli.prof.payments')}>
        {d.payments.length === 0 ? <EmptyText text={t('cli.prof.noPayments')} /> : (
          <ul className="divide-y divide-line rounded-xl border border-line">
            {d.payments.map((p) => (
              <li key={p.id} className="flex items-center gap-3 px-3 py-2 text-[13px]">
                <span className="min-w-0 flex-1">
                  <span className="block">{dateTime(p.createdAt)} · №{p.saleNumber ?? '—'}</span>
                  <span className="block truncate text-[12px] text-ink-3">{p.isReturn ? t('cli.prof.byReturn') : p.method ? t(`pay.${p.method}` as TranslationKey) : ''} · {p.staffName}{p.operationNumber ? ` · ${p.operationNumber}` : ''}</span>
                </span>
                <span className="font-semibold text-success">{tenge(p.amount)}</span>
              </li>
            ))}
          </ul>
        )}
      </Section>
      {paying && (!canAccounts || accounts.data) && (
        <CustomerPaymentModal card={d} accounts={accounts.data?.accounts ?? []} onClose={() => setPaying(false)}
          onDone={() => { setPaying(false); void q.refetch(); void qc.invalidateQueries({ queryKey: clientKeys.all }); void qc.invalidateQueries({ queryKey: ['finance'] }) }} />
      )}
    </div>
  )
}

// ---------- Бонусы ----------

export function BonusPane({ card, onAccrue }: { card: ClientCard; onAccrue?: () => void }) {
  const t = useT()
  const q = useQuery({ queryKey: ['bonus', 'customer', card.client.customerId], queryFn: () => bonusApi.customer(card.client.customerId), enabled: card.bonus.hasCard, retry: false })
  if (!card.bonus.hasCard) return <EmptyText text={t('cli.prof.noCard')} />
  const b = q.data
  if (q.isError) return <EmptyText text={errText(q.error, t('cli.failed'))} />
  if (!b) return <Spinner />
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge name={b.customer.statusName} color={b.customer.statusColor} />
        <span className="text-[13px] text-ink-2">{t('bon.cust.percent', { p: b.percent })}</span>
        {onAccrue && <Button className="ml-auto h-9! px-3!" onClick={onAccrue}>{t('cli.prof.accrue')}</Button>}
      </div>
      <div className="grid grid-cols-2 gap-2">
        <Stat label={t('bon.cust.balance')} value={<Points value={b.customer.balance} />} />
        <Stat label={t('bon.cust.nextExpiry')} value={b.nextExpiryAt ? `${num(b.nextExpiryAmount ?? 0)} · ${dateOnly(b.nextExpiryAt)}` : '—'} />
      </div>
      <Section title={t('bon.cust.history')}>
        {b.operations.length === 0 ? <EmptyText text={t('bon.empty')} /> : (
          <ul className="divide-y divide-line rounded-xl border border-line">
            {b.operations.map((o) => (
              <li key={o.id} className="flex items-center gap-3 px-3 py-2 text-[13px]">
                <OpTypeBadge type={o.type} amount={o.amount} />
                <span className="min-w-0 flex-1"><span className="block truncate">{o.basis}</span><span className="block text-[12px] text-ink-3">{dateTime(o.createdAt)} · {o.author}</span></span>
                <Points value={o.amount} signed muted={o.status === 'reversed'} />
              </li>
            ))}
          </ul>
        )}
      </Section>
    </div>
  )
}

// ---------- Активность ----------

export function MonthBars({ months, metric }: { months: ClientMonth[]; metric: 'count' | 'amount' }) {
  const max = Math.max(1, ...months.map((m) => (metric === 'count' ? m.count : m.amount)))
  return (
    <div className="flex h-36 items-end gap-1.5">
      {months.map((m) => {
        const v = metric === 'count' ? m.count : m.amount
        return (
          <div key={m.month} className="flex min-w-0 flex-1 flex-col items-center gap-1" title={`${m.month}: ${metric === 'count' ? num(v) : tenge(v)}`}>
            <span className="text-[10px] text-ink-3">{v > 0 ? (metric === 'count' ? num(v) : '') : ''}</span>
            <div className="w-full rounded-t-md bg-brand/80" style={{ height: `${Math.max(v > 0 ? 4 : 1, (v / max) * 100)}px` }} />
            <span className="text-[10px] text-ink-3">{m.month.slice(5)}</span>
          </div>
        )
      })}
    </div>
  )
}

export function ActivityPane({ card, storeIds }: { card: ClientCard; storeIds?: string }) {
  const t = useT()
  const q = useQuery({ queryKey: ['clients', 'activity', card.client.id, storeIds], queryFn: () => clientsApi.activity(card.client.id, storeIds) })
  const d = q.data
  if (!d) return <Spinner />
  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-2 text-[13px]">
        <ActivityPill activity={d.summary.segment} />
        <span className="text-ink-2">{d.summary.daysSinceLastPurchase !== null ? t('cli.prof.daysSince', { n: d.summary.daysSinceLastPurchase }) : t('cli.prof.noPurchases')}</span>
        <span className="ml-auto flex items-center gap-1.5 text-ink-2">{t('cli.prof.risk')}: <Pill tone={RISK_TONE[d.summary.churnRisk]}>{t(`cli.risk.${d.summary.churnRisk}` as TranslationKey)}</Pill></span>
      </div>
      <div className="grid grid-cols-3 gap-2">
        <Stat label={t('cli.prof.days30')} value={`${num(d.purchases30)} · ${tenge(d.amount30)}`} />
        <Stat label={t('cli.prof.days90')} value={`${num(d.purchases90)} · ${tenge(d.amount90)}`} />
        <Stat label={t('cli.prof.days365')} value={`${num(d.purchases365)} · ${tenge(d.amount365)}`} />
      </div>
      <Section title={t('cli.prof.byMonth')}><div className="rounded-xl border border-line p-3"><MonthBars months={d.months} metric="count" /></div></Section>
      <p className="text-[12px] text-ink-3">{t('cli.prof.riskHint')}</p>
      <Section title={t('cli.prof.communications')}>
        {d.communications.length === 0 ? <EmptyText text={t('cli.prof.noMessages')} /> : (
          <ul className="divide-y divide-line rounded-xl border border-line">
            {d.communications.map((m) => (
              <li key={m.id} className="flex items-start gap-3 px-3 py-2 text-[13px]">
                <MessageSquare size={15} className="mt-0.5 text-brand" />
                <span className="min-w-0 flex-1">
                  <span className="block whitespace-pre-wrap">{m.text}</span>
                  <span className="block text-[12px] text-ink-3">{dateTime(m.createdAt)} · {t(`cli.channel.${m.channel}` as TranslationKey)} · {m.staff}</span>
                </span>
                <Pill tone="green">{t('cli.msg.delivered')}</Pill>
              </li>
            ))}
          </ul>
        )}
      </Section>
    </div>
  )
}

// ---------- Документы ----------

export function DocumentsPane({ card, storeIds, onSale }: { card: ClientCard; storeIds?: string; onSale: (id: string) => void }) {
  const t = useT()
  const q = useQuery({ queryKey: ['clients', 'documents', card.client.id, storeIds], queryFn: () => clientsApi.documents(card.client.id, storeIds) })
  if (!q.data) return <Spinner />
  if (q.data.length === 0) return <EmptyText text={t('cli.prof.noDocuments')} />
  return (
    <ul className="divide-y divide-line rounded-xl border border-line">
      {q.data.map((d) => (
        <li key={`${d.kind}-${d.id}`}>
          <button type="button" disabled={!d.saleId} onClick={() => d.saleId && onSale(d.saleId)} className="flex w-full items-center gap-3 px-3 py-2.5 text-left text-[13px] enabled:hover:bg-field">
            {d.kind === 'sale' ? <Receipt size={16} className="text-brand" /> : d.kind === 'return' ? <RotateCcw size={16} className="text-danger" /> : <FileText size={16} className="text-violet-600" />}
            <span className="min-w-0 flex-1">
              <span className="block font-medium">{t(`cli.doc.${d.kind}` as TranslationKey)} {d.kind === 'finance' ? d.number : `№${d.number}`}</span>
              <span className="block truncate text-[12px] text-ink-3">{dateTime(d.date)} · {d.storeName}{d.description ? ` · ${d.description}` : ''}</span>
            </span>
            <span className="font-semibold">{tenge(d.amount)}</span>
          </button>
        </li>
      ))}
    </ul>
  )
}

// ---------- Заметки ----------

export function NotesPane({ card, canEdit }: { card: ClientCard; canEdit: boolean }) {
  const t = useT()
  const qc = useQueryClient()
  const key = ['clients', 'notes', card.client.id]
  const q = useQuery({ queryKey: key, queryFn: () => clientsApi.notes(card.client.id) })
  const [text, setText] = useState('')
  const [category, setCategory] = useState<NoteCategory>('General')
  const [editing, setEditing] = useState<{ id: string; text: string; category: NoteCategory } | null>(null)
  const [busy, setBusy] = useState(false)
  const done = (list: Awaited<ReturnType<typeof clientsApi.notes>>) => { qc.setQueryData(key, list); void qc.invalidateQueries({ queryKey: ['clients', 'card', card.client.id] }) }
  const add = async () => {
    if (!text.trim()) return
    setBusy(true)
    try { done(await clientsApi.addNote(card.client.id, text.trim(), category)); setText(''); toast(t('cli.notes.added')) } catch (err) { toast(errText(err, t('cli.failed')), 'error') } finally { setBusy(false) }
  }
  const save = async () => {
    if (!editing || !editing.text.trim()) return
    setBusy(true)
    try { done(await clientsApi.updateNote(editing.id, editing.text.trim(), editing.category)); setEditing(null) } catch (err) { toast(errText(err, t('cli.failed')), 'error') } finally { setBusy(false) }
  }
  const remove = async (id: string) => {
    if (!(await confirmDialog({ message: t('cli.notes.deleteConfirm'), confirmLabel: t('cli.notes.delete'), danger: true }))) return
    try { await clientsApi.deleteNote(id); void q.refetch(); void qc.invalidateQueries({ queryKey: ['clients', 'card', card.client.id] }) } catch (err) { toast(errText(err, t('cli.failed')), 'error') }
  }
  return (
    <div className="flex flex-col gap-4">
      {canEdit && (
        <div className="flex flex-col gap-2 rounded-xl border border-line p-3">
          <Textarea value={text} maxLength={2000} placeholder={t('cli.notes.placeholder')} onChange={(e) => setText(e.target.value)} className="min-h-20" />
          <div className="flex items-center gap-2">
            <Select value={category} onChange={(e) => setCategory(e.target.value as NoteCategory)} className="h-9! w-44! text-[13px]">
              {NOTE_CATEGORIES.map((c) => <option key={c} value={c}>{t(`cli.note.${c}` as TranslationKey)}</option>)}
            </Select>
            <Button className="ml-auto h-9! px-4!" loading={busy && !editing} disabled={!text.trim()} onClick={() => void add()}>{t('cli.notes.add')}</Button>
          </div>
        </div>
      )}
      {!q.data ? <Spinner /> : q.data.length === 0 ? <EmptyText text={t('cli.notes.empty')} /> : (
        <ul className="flex flex-col gap-2">
          {q.data.map((n) => (
            <li key={n.id} className="rounded-xl border border-line p-3 text-[13px]">
              <div className="mb-1 flex items-center gap-2 text-[12px] text-ink-3">
                <Pill tone="blue">{t(`cli.note.${n.category}` as TranslationKey)}</Pill>
                <span>{n.author} · {dateTime(n.createdAt)}{n.updatedAt ? ` · ${t('cli.notes.edited', { date: dateTime(n.updatedAt) })}` : ''}</span>
                {n.canEdit && editing?.id !== n.id && (
                  <span className="ml-auto flex gap-1">
                    <button type="button" aria-label={t('cli.notes.edit')} className="rounded p-1 hover:bg-field" onClick={() => setEditing({ id: n.id, text: n.text, category: n.category })}><Pencil size={14} /></button>
                    <button type="button" aria-label={t('cli.notes.delete')} className="rounded p-1 text-danger hover:bg-danger-soft" onClick={() => void remove(n.id)}><Trash2 size={14} /></button>
                  </span>
                )}
              </div>
              {editing?.id === n.id ? (
                <div className="flex flex-col gap-2">
                  <Textarea value={editing.text} maxLength={2000} onChange={(e) => setEditing({ ...editing, text: e.target.value })} className="min-h-20" />
                  <div className="flex items-center gap-2">
                    <Select value={editing.category} onChange={(e) => setEditing({ ...editing, category: e.target.value as NoteCategory })} className="h-9! w-44! text-[13px]">
                      {NOTE_CATEGORIES.map((c) => <option key={c} value={c}>{t(`cli.note.${c}` as TranslationKey)}</option>)}
                    </Select>
                    <Button variant="secondary" className="ml-auto h-9! px-3!" onClick={() => setEditing(null)}>{t('common.cancel')}</Button>
                    <Button className="h-9! px-3!" loading={busy} onClick={() => void save()}>{t('cli.form.save')}</Button>
                  </div>
                </div>
              ) : <p className="whitespace-pre-wrap">{n.text}</p>}
            </li>
          ))}
        </ul>
      )}
      <p className="text-[12px] text-ink-3">{t('cli.notes.hint', { id: clientNo(card.client.number) })}</p>
    </div>
  )
}
