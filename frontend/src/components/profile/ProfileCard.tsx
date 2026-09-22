import { Calendar, Camera, Coins, Pencil, Store } from 'lucide-react'
import type { Customer } from '../../lib/api'
import { formatBonus, formatDate, initials } from '../../lib/format'

export function ProfileCard({ me }: { me: Customer }) {
  return (
    <section className="rounded-card bg-surface p-4">
      <div className="flex items-center gap-3.5">
        <div className="relative shrink-0">
          <div className="flex size-[72px] items-center justify-center rounded-full bg-violet-soft text-xl font-bold text-violet">
            {initials(me.fullName)}
          </div>
          <button
            type="button"
            aria-label="Фото өзгерту"
            className="absolute -bottom-0.5 -right-0.5 flex size-[26px] items-center justify-center rounded-full bg-surface text-ink shadow"
          >
            <Camera size={14} />
          </button>
        </div>
        <div className="min-w-0 flex-1">
          <div className="text-[15px] font-bold leading-tight">{me.fullName}</div>
          <div className="mt-1 text-xs text-ink-2">{formatPhone(me.phone)}</div>
          {me.email && <div className="truncate text-xs text-ink-2">{me.email}</div>}
          {me.birthDate && (
            <div className="mt-0.5 flex items-center gap-1.5 text-xs text-ink-2">
              <Calendar size={13} /> {formatDate(me.birthDate)}
            </div>
          )}
        </div>
        <button
          type="button"
          aria-label="Өңдеу"
          className="flex size-10 shrink-0 items-center justify-center rounded-full bg-violet-soft text-violet"
        >
          <Pencil size={18} />
        </button>
      </div>

      <div className="mt-4 grid grid-cols-2 gap-2">
        <Stat icon={<Coins size={18} className="text-violet" />} iconBg="#EEEBFF" value={formatBonus(me.totalBalance)} label="Барлық бонус" />
        <Stat icon={<Store size={18} className="text-violet" />} iconBg="#EEEBFF" value={String(me.storeCount)} label="Дүкендер" />
      </div>
    </section>
  )
}

function Stat({ icon, iconBg, value, label }: { icon: React.ReactNode; iconBg: string; value: string; label: string }) {
  return (
    <div className="rounded-xl border border-line px-2.5 py-3">
      <div className="flex size-[34px] items-center justify-center rounded-[9px]" style={{ background: iconBg }}>
        {icon}
      </div>
      <div className="mt-2 whitespace-nowrap text-[13px] font-bold">{value}</div>
      <div className="text-[10px] text-ink-2">{label}</div>
    </div>
  )
}

function formatPhone(p: string) {
  const d = p.replace(/\D/g, '')
  if (d.length === 11) return `+${d[0]} ${d.slice(1, 4)} ${d.slice(4, 7)} ${d.slice(7, 9)} ${d.slice(9)}`
  return p
}
