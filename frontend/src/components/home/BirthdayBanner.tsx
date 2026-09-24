import { ChevronRight, Gift } from 'lucide-react'
import { useT } from '../../lib/i18n'

export function BirthdayBanner() {
  const t = useT()
  return (
    <section className="flex items-center gap-3 rounded-card bg-brand-soft px-4 py-3.5">
      <div className="flex size-14 shrink-0 items-center justify-center text-brand">
        <Gift size={40} strokeWidth={1.6} />
      </div>
      <div className="min-w-0 flex-1">
        <div className="text-[15px] font-bold leading-tight">{t('home.birthdayTitle')}</div>
        <div className="mt-1 text-xs text-ink-2">{t('home.birthdayBody')}</div>
      </div>
      <button type="button" className="flex shrink-0 items-center gap-0.5 rounded-[10px] bg-surface px-3 py-2.5 text-[13px] font-semibold text-brand">
        {t('common.more')} <ChevronRight size={14} />
      </button>
    </section>
  )
}
