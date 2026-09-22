import { ChevronRight, Crown } from 'lucide-react'
import type { BonusCard } from '../../lib/api'
import { levelThresholdLabel } from '../../lib/format'

const THRESHOLDS: Record<string, number> = {
  'Жаңа клиент': 50_000,
  'Тұрақты клиент': 150_000,
  'Сүйікті клиент': 500_000,
  'VIP клиент': 500_000,
}

export function LevelCard({ card }: { card: BonusCard }) {
  const threshold = THRESHOLDS[card.level] ?? 500_000
  const progress = Math.min(100, Math.max(0, ((threshold - card.amountToNextLevel) / threshold) * 100))

  return (
    <section className="flex items-center gap-3 rounded-card bg-surface p-4">
      <div className="flex size-14 shrink-0 items-center justify-center rounded-[14px] bg-brand-soft text-brand">
        <Crown size={26} />
      </div>
      <div className="min-w-0 flex-1 whitespace-nowrap">
        <div className="text-xs text-ink-2">Сіздің деңгейіңіз</div>
        <div className="text-[17px] font-bold leading-tight">{card.level}</div>
        <div className="text-xs text-ink-2">Бонус есептеу: {card.cashbackPercent}%</div>
      </div>
      <div className="w-[128px] shrink-0 whitespace-nowrap">
        <div className="text-[11px] text-ink-2">Келесі деңгейге дейін</div>
        <div className="my-2 h-1.5 rounded-full bg-line">
          <div className="h-full rounded-full bg-brand" style={{ width: `${progress}%` }} />
        </div>
        <div className="flex items-center justify-between text-xs font-semibold">
          {levelThresholdLabel(card.amountToNextLevel)}
          <ChevronRight size={16} className="text-brand" />
        </div>
      </div>
    </section>
  )
}
