import type { ReactNode } from 'react'
import { BarChart3, Boxes, Calculator, Check, Copy, Gift, Package, ShoppingCart, Users, Wallet } from 'lucide-react'
import { Button } from '../components/ui/Button'
import { Modal } from '../components/ui/Modal'
import { toast } from '../components/ui/Toast'
import { AdminApiError, type BillingStatus, type ModuleKey } from './adminApi'

export const MODULES: { key: ModuleKey; label: string; hint: string; icon: ReactNode }[] = [
  { key: 'cashier', label: 'Касса', hint: 'Продажи на кассе, возвраты, долги, смены', icon: <Calculator size={18} /> },
  { key: 'products', label: 'Товары', hint: 'Каталог, категории, штрихкоды, импорт', icon: <Package size={18} /> },
  { key: 'warehouse', label: 'Склад', hint: 'Остатки, приёмка, перемещения, списания, поставщики', icon: <Boxes size={18} /> },
  { key: 'sales', label: 'Продажи', hint: 'История чеков, возвраты из офиса, экспорт', icon: <ShoppingCart size={18} /> },
  { key: 'statistics', label: 'Статистика', hint: 'Отчёты, динамика, экспорт', icon: <BarChart3 size={18} /> },
  { key: 'finance', label: 'Финансы', hint: 'Счета, P&L, Cash Flow, долги', icon: <Wallet size={18} /> },
  { key: 'bonus', label: 'Бонусы', hint: 'Статусы, сгорание, рассылки, правила', icon: <Gift size={18} /> },
  { key: 'clients', label: 'Клиенты', hint: 'CRM: база, сегменты, импорт, долги', icon: <Users size={18} /> },
]
export const moduleLabel = (k: string) => MODULES.find((m) => m.key === k)?.label ?? k

export const STATUS: Record<BillingStatus, { label: string; cls: string }> = {
  trial: { label: 'Пробный период', cls: 'bg-violet-50 text-violet-700' },
  active: { label: 'Оплачено', cls: 'bg-success-soft text-success' },
  dueSoon: { label: 'Скоро оплата', cls: 'bg-amber-50 text-amber-700' },
  overdue: { label: 'Просрочено', cls: 'bg-danger-soft text-danger' },
  unpaid: { label: 'Не оплачено', cls: 'bg-danger-soft text-danger' },
  suspended: { label: 'Приостановлен', cls: 'bg-field text-ink-2' },
}
export const STATUS_ORDER: BillingStatus[] = ['trial', 'active', 'dueSoon', 'overdue', 'unpaid', 'suspended']

export function StatusPill({ status }: { status: BillingStatus }) {
  const s = STATUS[status]
  return <span className={`inline-flex items-center whitespace-nowrap rounded-full px-2.5 py-0.5 text-[12px] font-semibold ${s.cls}`}>{s.label}</span>
}

export const METHODS: Record<string, string> = { kaspi: 'Kaspi', transfer: 'Перевод на счёт', card: 'Карта', cash: 'Наличные', other: 'Другое' }

export const errText = (err: unknown) => (err instanceof AdminApiError ? err.message : 'Не удалось выполнить действие')
export const reportError = (err: unknown) => toast(errText(err), 'error')

export const date = (iso: string | null | undefined) => (iso ? new Date(iso.length === 10 ? `${iso}T00:00:00` : iso).toLocaleDateString('ru-RU') : '—')
export const dateTime = (iso: string | null | undefined) => (iso ? new Date(iso).toLocaleString('ru-RU', { dateStyle: 'short', timeStyle: 'short' }) : '—')
export const isoToday = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` }

export function PageHeader({ title, subtitle, actions }: { title: ReactNode; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="mr-auto min-w-0">
        <h1 className="text-[26px] font-extrabold tracking-tight text-[#0b1f44]">{title}</h1>
        {subtitle && <div className="text-[14px] text-[#66758f]">{subtitle}</div>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}

const TONES = {
  blue: 'bg-brand-soft text-brand', green: 'bg-success-soft text-success', violet: 'bg-violet-50 text-violet-600', amber: 'bg-amber-50 text-amber-600',
  red: 'bg-danger-soft text-danger', gray: 'bg-field text-ink-2',
}

export function Tile({ label, value, hint, icon, tone = 'blue', onClick, active }: {
  label: string; value: ReactNode; hint?: ReactNode; icon: ReactNode; tone?: keyof typeof TONES; onClick?: () => void; active?: boolean
}) {
  return (
    <button type="button" onClick={onClick} disabled={!onClick}
      className={`flex min-w-0 flex-col rounded-2xl border bg-surface p-4 text-left transition-colors disabled:cursor-default ${active ? 'border-brand ring-2 ring-brand/15' : 'border-line enabled:hover:border-brand/50'}`}>
      <span className="flex items-center gap-2.5">
        <span className={`flex size-9 shrink-0 items-center justify-center rounded-xl ${TONES[tone]}`}>{icon}</span>
        <span className="truncate text-[13px] font-medium text-ink-2">{label}</span>
      </span>
      <span className="mt-2 truncate text-[24px] font-extrabold tracking-tight text-[#0b1f44]">{value}</span>
      {hint && <span className="truncate text-[12px] text-ink-3">{hint}</span>}
    </button>
  )
}

export function Panel({ title, action, children, className = '' }: { title?: ReactNode; action?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={`min-w-0 rounded-2xl border border-line bg-surface p-4 ${className}`}>
      {(title || action) && (
        <div className="mb-3 flex items-center gap-2">
          {title && <h2 className="text-[15px] font-bold text-[#0b1f44]">{title}</h2>}
          {action && <div className="ml-auto">{action}</div>}
        </div>
      )}
      {children}
    </section>
  )
}

/** Уақытша құпиясөз бір рет көрсетіледі: көшіріп, иесіне жеке беру керек. */
export function SecretModal({ title, lines, note, onClose }: { title: string; lines: { label: string; value: string; secret?: boolean }[]; note?: string; onClose: () => void }) {
  const copy = (v: string) => { void navigator.clipboard?.writeText(v); toast('Скопировано') }
  return (
    <Modal title={title} onClose={onClose} width={480}>
      <div className="flex flex-col gap-3">
        {lines.map((l) => (
          <div key={l.label} className="flex items-center gap-3 rounded-xl border border-line px-3 py-2">
            <span className="w-28 shrink-0 text-[13px] text-ink-2">{l.label}</span>
            <span className={`min-w-0 flex-1 truncate ${l.secret ? 'font-mono text-[16px] font-bold tracking-wider' : 'font-medium'}`}>{l.value}</span>
            <button type="button" className="rounded-lg p-1.5 text-ink-3 hover:bg-field hover:text-ink" aria-label="Скопировать" onClick={() => copy(l.value)}><Copy size={16} /></button>
          </div>
        ))}
        <p className="rounded-xl bg-amber-50 px-3 py-2 text-[13px] text-amber-800">{note ?? 'Пароль показывается один раз. Передайте его лично — после входа его нужно сменить.'}</p>
        <Button icon={<Check size={16} />} onClick={onClose}>Готово</Button>
      </div>
    </Modal>
  )
}

export function ModuleChips({ modules, max = 8 }: { modules: string[]; max?: number }) {
  const shown = modules.slice(0, max)
  return (
    <span className="flex flex-wrap gap-1">
      {shown.map((m) => <span key={m} className="rounded-md bg-field px-1.5 py-0.5 text-[11px] font-semibold text-ink-2">{moduleLabel(m)}</span>)}
      {modules.length > max && <span className="text-[11px] text-ink-3">+{modules.length - max}</span>}
      {modules.length === 0 && <span className="text-[12px] text-ink-3">нет модулей</span>}
    </span>
  )
}
