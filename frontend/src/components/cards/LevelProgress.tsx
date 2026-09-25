import type { BonusCard } from '../../lib/api'
import { formatNumber, formatTenge } from '../../lib/format'
import { useT, type TranslationKey } from '../../lib/i18n'

/** Келесі мәртебеге дейін қанша сатып алу қалғанын көрсететін жолақ. */
export function LevelProgress({ card }: { card: BonusCard }) {
  const t = useT()

  if (!card.nextLevel || card.nextLevelAmount == null) {
    return (
      <section className="rounded-2xl bg-brand-soft px-4 py-3">
        <div className="text-[13px] font-bold text-brand">{t('cards.atMaxLevel')}</div>
      </section>
    )
  }

  const target = card.nextLevelAmount
  const percent = target > 0 ? Math.min(100, Math.round((card.totalSpent / target) * 100)) : 100

  return (
    <section className="rounded-2xl bg-brand-soft px-4 py-3">
      <div className="flex items-baseline justify-between gap-3">
        <div className="min-w-0 truncate text-[13px] font-bold">
          {t('cards.toLevel', { level: t(`level.short.${card.nextLevel}` as TranslationKey) })}
        </div>
        <div className="shrink-0 text-[12px] font-bold">
          {t('cards.progressOf', { spent: formatNumber(card.totalSpent), target: formatTenge(target) })}
        </div>
      </div>

      <div className="mt-2 h-2 overflow-hidden rounded-full bg-surface">
        <div className="h-full rounded-full bg-brand transition-all" style={{ width: `${Math.max(percent, 2)}%` }} />
      </div>

      <div className="mt-2 flex items-baseline justify-between gap-3 text-xs text-ink-2">
        <span className="min-w-0 truncate">
          {t('cards.leftToLevel', { amount: formatTenge(card.amountToNextLevel) })}
        </span>
        <span className="shrink-0 font-semibold">{percent}%</span>
      </div>
    </section>
  )
}
