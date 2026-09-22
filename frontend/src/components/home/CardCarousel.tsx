import { useEffect, useRef, useState } from 'react'
import type { BonusCard } from '../../lib/api'
import { HeroCard } from './HeroCard'

const PADDING = 16

export function CardCarousel({ cards, onShowQr, onActiveChange }: {
  cards: BonusCard[]
  onShowQr?: (card: BonusCard) => void
  onActiveChange?: (card: BonusCard) => void
}) {
  const [active, setActive] = useState(0)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const c = cards[active]
    if (c) onActiveChange?.(c)
  }, [active, cards, onActiveChange])

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
          <div key={c.storeId} className="w-full shrink-0 snap-start">
            <HeroCard card={c} onShowQr={() => onShowQr?.(c)} />
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
