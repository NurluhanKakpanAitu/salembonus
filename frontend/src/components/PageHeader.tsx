import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Brand } from './Brand'
import { useMe } from '../lib/queries'
import { initials } from '../lib/format'
import { useT } from '../lib/i18n'

export function PageHeader({ right }: { right?: ReactNode }) {
  return (
    <header className="flex items-center justify-between gap-3 py-2">
      <Brand />
      <div className="shrink-0">{right}</div>
    </header>
  )
}

/** Профильге апаратын аватар: фото болса фото, әйтпесе инициалдар. */
export function Avatar({ to = '/profile/edit' }: { to?: string }) {
  const t = useT()
  const me = useMe()
  const name = me.data?.fullName ?? ''
  const short = name && !name.startsWith('+') ? initials(name) : ''
  const photo = me.data?.avatarUrl

  return (
    <Link
      to={to}
      aria-label={t('profile.personal')}
      className="flex size-11 items-center justify-center overflow-hidden rounded-full bg-violet-soft text-sm font-bold text-violet active:scale-95"
    >
      {photo ? <img src={photo} alt="" className="size-full object-cover" /> : short || (
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="8" r="4" />
          <path d="M4 21c0-4 4-6 8-6s8 2 8 6" />
        </svg>
      )}
    </Link>
  )
}
