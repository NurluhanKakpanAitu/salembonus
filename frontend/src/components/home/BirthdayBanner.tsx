import { ChevronRight, Gift } from 'lucide-react'

export function BirthdayBanner() {
  return (
    <section className="flex items-center gap-3 rounded-card bg-brand-soft px-4 py-3.5">
      <div className="flex size-14 shrink-0 items-center justify-center text-brand">
        <Gift size={40} strokeWidth={1.6} />
      </div>
      <div className="min-w-0 flex-1">
        <div className="text-[15px] font-bold leading-tight">Туған күніңіз құтты болсын!</div>
        <div className="mt-1 text-xs text-ink-2">Арнайы бонус сізді күтеді 🎉</div>
      </div>
      <button type="button" className="flex shrink-0 items-center gap-0.5 rounded-[10px] bg-surface px-3 py-2.5 text-[13px] font-semibold text-brand">
        Толығырақ <ChevronRight size={14} />
      </button>
    </section>
  )
}
