import { Construction, Lock } from 'lucide-react'
import { activeStore, useAuth } from '../lib/auth'
import { useT } from '../lib/i18n'
import { canSee, type NavItem } from '../layout/nav'

/**
 * Бөлімнің уақытша беті. Рұқсатты тексереді: мәзірде жасырылған бөлімге сілтеме арқылы кірсе де
 * ашылмайды (сервер бәрібір өзі тексереді).
 */
export function SectionPage({ item }: { item: NavItem }) {
  const t = useT()
  const store = useAuth(activeStore)
  const allowed = canSee(item, store)
  const Icon = allowed ? Construction : Lock

  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <div className="max-w-md rounded-2xl border border-line bg-surface px-8 py-10 text-center">
        <span className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-brand-soft text-brand">
          <Icon size={26} />
        </span>
        <h2 className="mt-4 text-[19px] font-bold">{allowed ? t('section.soon') : t('section.noAccess')}</h2>
        {allowed && <p className="mt-2 text-[14px] leading-relaxed text-ink-2">{t('section.soonHint')}</p>}
      </div>
    </div>
  )
}
