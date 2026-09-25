import { Crown, Percent } from 'lucide-react'
import type { StoreDetail } from '../../lib/api'
import { formatTenge } from '../../lib/format'
import { useT } from '../../lib/i18n'
import type { TranslationKey } from '../../lib/i18n'

/** Клиент мәртебесі, бонус пайызы, келесі мәртебеге дейінгі жол. */
export function StoreStatusCard({ store }: { store: StoreDetail }) {
  const t = useT()
  const levelName = (key: string) => t(`level.${key}` as TranslationKey)
  const shortLevel = (key: string) => t(`level.short.${key}` as TranslationKey)
  const levels = store.levels
  const currentIdx = Math.max(0, levels.findIndex((l) => l.isCurrent))
  const next = levels[currentIdx + 1]
  const toNext = store.amountToNextLevel ?? 0
  const isMax = !next || toNext <= 0

  // Ағымдағы белес ішіндегі прогресс
  const segmentStart = levels[currentIdx]?.fromAmount ?? 0
  const spent = next ? next.fromAmount - toNext : segmentStart
  const span = next ? next.fromAmount - segmentStart : 1
  const progress = isMax ? 100 : Math.min(100, Math.max(2, ((spent - segmentStart) / span) * 100))

  return (
    <section className="overflow-hidden rounded-card bg-surface">
      <div className="flex items-center gap-3 p-4">
        <div className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-gold/15 text-gold">
          <Crown size={24} />
        </div>
        <div className="min-w-0 flex-1">
          <div className="text-xs text-ink-2">{t('cards.clientStatus')}</div>
          <div className="truncate text-lg font-bold leading-tight">{levelName(store.level ?? 'New')}</div>
        </div>
        <div className="shrink-0 text-right">
          <div className="text-[26px] font-extrabold leading-none tracking-tight text-brand">
            {store.cashbackPercent}%
          </div>
          <div className="mt-0.5 text-xs text-ink-2">{t('cards.cashbackWord')}</div>
        </div>
      </div>

      <div className="px-4">
        <div className="flex items-baseline justify-between">
          <span className="text-xs text-ink-2">
            {isMax ? t('cards.maxLevel') : t('cards.toNextLevel', { level: shortLevel(next.name) })}
          </span>
          {!isMax && <span className="text-[15px] font-bold">{formatTenge(toNext)}</span>}
        </div>
        <div className="mt-2 h-2 overflow-hidden rounded-full bg-line">
          <div className="h-full rounded-full bg-brand transition-all" style={{ width: `${progress}%` }} />
        </div>
      </div>

      <div className="mt-4 grid grid-cols-4 gap-1.5 px-4">
        {levels.map((l, i) => {
          const reached = i <= currentIdx
          return (
            <div
              key={l.name}
              className={`rounded-xl px-1.5 py-2 text-center ${
                l.isCurrent ? 'bg-brand text-white' : reached ? 'bg-brand-soft text-brand' : 'bg-bg text-ink-3'
              }`}
            >
              <div className="text-[11px] font-bold leading-tight">{shortLevel(l.name)}</div>
              <div className="mt-0.5 text-[11px] font-bold">{l.cashbackPercent}%</div>
              <div className="text-[10px] opacity-80">
                {l.fromAmount > 0 ? `${Math.round(l.fromAmount / 1000)}K ₸` : '0 ₸'}
              </div>
            </div>
          )
        })}
      </div>

      <div className="mt-4 border-t border-line px-4 py-3.5 text-[13px]">
        <div className="flex items-center gap-2.5 text-ink-2">
          <Percent size={15} className="shrink-0" />
          <span>{t('cards.cashbackNote', { percent: store.cashbackPercent })}</span>
        </div>
      </div>
    </section>
  )
}
