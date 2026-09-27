import { ChevronRight, type LucideIcon } from 'lucide-react'
import { Link } from 'react-router-dom'

/** Профильдегі бір жол: түсті иконка, тақырып, түсіндірме және көрсеткі. */
export function ProfileRow({
  icon: Icon,
  tint,
  title,
  subtitle,
  to,
  onClick,
  danger,
}: {
  icon: LucideIcon
  tint: string
  title: string
  subtitle?: string
  to?: string
  onClick?: () => void
  danger?: boolean
}) {
  const body = (
    <>
      <div
        className="flex size-11 shrink-0 items-center justify-center rounded-2xl"
        style={{ background: `${tint}1F`, color: tint }}
      >
        <Icon size={21} />
      </div>
      <div className="min-w-0 flex-1">
        <div className={`truncate text-[15px] font-bold leading-tight ${danger ? 'text-danger' : ''}`}>{title}</div>
        {subtitle && <div className="mt-0.5 truncate text-xs text-ink-2">{subtitle}</div>}
      </div>
      <ChevronRight size={18} className="shrink-0 text-ink-3" />
    </>
  )

  const cls = 'flex w-full items-center gap-3 rounded-2xl bg-surface p-3.5 text-left active:scale-[0.99]'
  return to ? (
    <Link to={to} className={cls}>{body}</Link>
  ) : (
    <button type="button" onClick={onClick} className={cls}>{body}</button>
  )
}
