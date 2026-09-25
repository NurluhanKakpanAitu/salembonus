import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ChevronUp } from 'lucide-react'
import type { BonusCard } from '../../lib/api'
import { BonusCardTile } from './BonusCardTile'
import { useT } from '../../lib/i18n'

const CARD_H = 216
/** Жиналған күйде әр картаның көрініп тұратын жолағы (атауы мен мәртебесі сыяды). */
const PEEK = 66
/** Карта ашылғанда қалғандары төменде осындай жіңішке жолақпен тұрады. */
const FOOTER_PEEK = 30
const GAP = 20

/**
 * Карталар бума болып жиналып тұрады. Біреуін таңдағанда ол QR-імен бірге толық ашылады,
 * қалғандары төменге жиналады.
 */
export function CardStack({ cards }: { cards: BonusCard[] }) {
  const t = useT()
  const navigate = useNavigate()
  const [open, setOpen] = useState<string | null>(null)
  const [openHeight, setOpenHeight] = useState(CARD_H)
  const openRef = useRef<HTMLDivElement>(null)

  const openIndex = cards.findIndex((c) => c.storeId === open)
  const expanded = openIndex >= 0

  // QR суреті кейін жүктелетіндіктен ашылған картаның биіктігі өзгеріп отырады.
  useEffect(() => {
    const el = openRef.current
    if (!expanded || !el) {
      setOpenHeight(CARD_H)
      return
    }
    const observer = new ResizeObserver(() => setOpenHeight(el.offsetHeight))
    observer.observe(el)
    setOpenHeight(el.offsetHeight)
    return () => observer.disconnect()
  }, [expanded, open])

  const offsetOf = (i: number) => {
    if (!expanded) return i * PEEK
    if (i === openIndex) return 0
    const rest = i < openIndex ? i : i - 1
    return openHeight + GAP + rest * FOOTER_PEEK
  }

  // Ашық күйде төмендегі бума жолақ болып қиылады — Wallet-тегідей.
  const height = expanded
    ? openHeight + GAP + Math.max(1, cards.length - 1) * FOOTER_PEEK
    : (cards.length - 1) * PEEK + CARD_H

  return (
    <section>
      <div
        className={`relative transition-[height] duration-300 ease-out ${expanded ? 'overflow-hidden' : ''}`}
        style={{ height }}
      >
        {cards.map((card, i) => {
          const isOpen = card.storeId === open
          return (
            <div
              key={card.storeId}
              ref={isOpen ? openRef : undefined}
              role="button"
              tabIndex={0}
              aria-expanded={isOpen}
              onClick={() => setOpen(isOpen ? null : card.storeId)}
              onKeyDown={(e) => {
                if (e.key !== 'Enter' && e.key !== ' ') return
                e.preventDefault()
                setOpen(isOpen ? null : card.storeId)
              }}
              className="absolute inset-x-0 top-0 cursor-pointer transition-transform duration-300 ease-out"
              style={{ transform: `translateY(${offsetOf(i)}px)`, zIndex: isOpen ? cards.length + 1 : i }}
            >
              <BonusCardTile
                card={card}
                expanded={isOpen}
                onInfo={() => navigate(`/cards/${card.storeId}`)}
              />
            </div>
          )
        })}
      </div>

      {expanded && (
        <button
          type="button"
          onClick={() => setOpen(null)}
          className="mt-3 flex w-full items-center justify-center gap-1.5 text-[13px] font-semibold text-ink-2"
        >
          <ChevronUp size={16} /> {t('cards.showAll')}
        </button>
      )}
    </section>
  )
}
