import type { ReactNode } from 'react'
import { Crown, ShieldCheck, UserRound } from 'lucide-react'
import type { StaffRole } from '../../lib/types'
import { useT, type TranslationKey } from '../../lib/i18n'

/** Рөл белгісі: иесі — алтын тәж, әкімші — қалқан, кассир — адам. */
export function RoleBadge({ role, size = 'md' }: { role: StaffRole; size?: 'sm' | 'md' }) {
  const t = useT()
  const cls = role === 'Owner' ? 'bg-amber-50 text-amber-700' : role === 'Admin' ? 'bg-brand-soft text-brand' : 'bg-field text-ink-2'
  const Icon = role === 'Owner' ? Crown : role === 'Admin' ? ShieldCheck : UserRound
  return (
    <span className={`inline-flex items-center gap-1 rounded-full font-semibold ${size === 'sm' ? 'px-2 py-0.5 text-[12px]' : 'px-2.5 py-1 text-[13px]'} ${cls}`}>
      <Icon size={size === 'sm' ? 12 : 14} />{t(`role.${role}` as TranslationKey)}
    </span>
  )
}

/** Құрылғының user agent-інен қысқа атау: браузер + жүйе, кассаның терминал сеансы. */
export function deviceName(ua: string | null): string {
  if (!ua) return '—'
  if (/landing-screenshots|e2e/i.test(ua)) return ua
  const os = /Android/i.test(ua) ? 'Android' : /iPhone|iPad/i.test(ua) ? 'iOS' : /Windows/i.test(ua) ? 'Windows' : /Mac OS/i.test(ua) ? 'macOS' : /Linux/i.test(ua) ? 'Linux' : ''
  const browser = /Edg\//.test(ua) ? 'Edge' : /OPR\//.test(ua) ? 'Opera' : /Chrome\//.test(ua) ? 'Chrome' : /Safari\//.test(ua) ? 'Safari' : /Firefox\//.test(ua) ? 'Firefox' : ''
  const name = [browser, os].filter(Boolean).join(' · ')
  return name || ua.slice(0, 40)
}

/** Атау-мән жолы (карточкадағы «Контакты», «Последний вход» т.б.). */
export function InfoRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 py-2 text-[14px]">
      <span className="shrink-0 text-ink-2">{label}</span>
      <span className="min-w-0 break-words text-right font-medium">{children}</span>
    </div>
  )
}

/** Бөлім тақырыбы бар карточка (формалар мен баптаулар беттері үшін). */
export function Section({ title, hint, action, children }: { title: string; hint?: string; action?: ReactNode; children: ReactNode }) {
  return (
    <section className="rounded-2xl border border-line bg-surface p-5">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-2">
        <div>
          <h2 className="text-[16px] font-bold">{title}</h2>
          {hint && <p className="mt-0.5 text-[13px] text-ink-3">{hint}</p>}
        </div>
        {action}
      </div>
      {children}
    </section>
  )
}

export const ROLES: StaffRole[] = ['Owner', 'Admin', 'Cashier']
