import { useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import type { BonusCard } from '../../lib/api'
import { BonusCardTile } from './BonusCardTile'

const PADDING = 16

/** Карталар карусельі: көлденең сырғиды, астында нүктелер. */
export function CardCarousel({
  cards,
  onShowQr,
  size = 'compact',
}: {
  cards: BonusCard[]
  onShowQr?: (card: BonusCard) => void
  size?: 'full' | 'compact'
}) {
  const [active, setActive] = useState(0)
  const ref = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()

  const slides = () => Array.from(ref.current?.children ?? []) as HTMLElement[]

  const onScroll = () => {
    const el = ref.current
    if (!el) return
    const left = el.scrollLeft + PADDING
    let best = 0
    let bestDist = Infinity
    slides().forEach((s, i) => {
      const d = Math.abs(s.offsetLeft - left)
      if (d < bestDist) {
        bestDist = d
        best = i
      }
    })
    setActive(best)
  }

  const scrollTo = (i: number) => {
    const el = ref.current
    const s = slides()[i]
    if (el && s) el.scrollTo({ left: s.offsetLeft - PADDING, behavior: 'smooth' })
  }

  return (
    <section>
      <div
        ref={ref}
        onScroll={onScroll}
        className="-mx-4 flex snap-x snap-mandatory gap-3 overflow-x-auto scroll-px-4 px-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
      >
        {cards.map((c) => (
          <div
            key={c.storeId}
            role="link"
            tabIndex={0}
            onClick={() => navigate(`/cards/${c.storeId}`)}
            onKeyDown={(e) => e.key === 'Enter' && navigate(`/cards/${c.storeId}`)}
            className="w-full shrink-0 cursor-pointer snap-start active:scale-[0.99]"
          >
            <BonusCardTile card={c} size={size} onAction={() => onShowQr?.(c)} />
          </div>
        ))}
      </div>
      {cards.length > 1 && (
        <div className="mt-3 flex justify-center gap-1.5">
          {cards.map((c, i) => (
            <button
              key={c.storeId}
              type="button"
              aria-label={c.storeName}
              onClick={() => scrollTo(i)}
              className={`h-1.5 rounded-full transition-all ${i === active ? 'w-[18px] bg-brand' : 'w-1.5 bg-gray-300'}`}
            />
          ))}
        </div>
      )}
    </section>
  )
}
