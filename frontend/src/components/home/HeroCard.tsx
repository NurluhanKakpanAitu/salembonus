import { ChevronRight, Crown, QrCode } from 'lucide-react'
import type { BonusCard } from '../../lib/api'
import { formatBonus, formatTenge } from '../../lib/format'
import { LogoMark } from '../Brand'

const NEXT_LEVEL_LABEL: Record<string, string> = {
  'Жаңа клиент': 'Келесі деңгей 50 000 ₸',
  'Тұрақты клиент': 'Келесі деңгей 150 000 ₸',
  'Сүйікті клиент': 'Келесі деңгей 500 000 ₸',
  'VIP клиент': 'Келесі деңгей 500 000 ₸',
}

export function HeroCard({ card, onShowQr }: { card: BonusCard; onShowQr?: () => void }) {
  const isVip = card.level === 'VIP клиент'
  return (
    <article className="relative flex h-[290px] w-full snap-center flex-col justify-between overflow-hidden rounded-[22px] bg-dark p-5 text-white">
      <div
        className="pointer-events-none absolute inset-y-0 right-0 w-2/3 opacity-40"
        style={{ background: 'radial-gradient(120% 80% at 90% 40%, #4a4a52 0%, transparent 60%)' }}
      />
      <div className="relative flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="flex size-11 items-center justify-center rounded-full bg-white">
            <LogoMark size={28} />
          </div>
          <div>
            <div className="text-lg font-bold leading-tight">{card.storeName}</div>
            <div className="text-xs text-gray-400">{card.category}</div>
          </div>
        </div>
        {isVip && (
          <span className="flex items-center gap-1.5 rounded-full border-[1.5px] border-gold px-3 py-1.5 text-[13px] font-bold text-gold">
            <Crown size={14} /> VIP
          </span>
        )}
      </div>

      <div className="relative flex items-end justify-between gap-3">
        <div>
          <div className="text-[13px] text-gray-400">Бонус балансы</div>
          <div className="text-[34px] font-extrabold leading-none tracking-tight">{formatBonus(card.balance)}</div>
          <div className="mt-1 text-sm text-gray-400">≈ {formatTenge(card.balance)}</div>
        </div>
        <div className="rounded-xl bg-white/10 px-3 py-2.5 text-[11px] text-gray-400">
          <div>Есептеу</div>
          <div className="text-base font-bold text-white">{card.cashbackPercent}%</div>
          <div className="my-1.5 h-px bg-white/20" />
          <div>Сіздің деңгейіңіз</div>
          <div className={`text-sm font-bold ${isVip ? 'text-gold' : 'text-white'}`}>{card.level}</div>
          <div className="mt-0.5 text-[10px]">{NEXT_LEVEL_LABEL[card.level] ?? ''}</div>
        </div>
      </div>

      <button
        type="button"
        onClick={onShowQr}
        className="relative flex w-fit items-center gap-2 rounded-xl bg-white px-4 py-3 text-sm font-semibold text-ink active:scale-[0.98]"
      >
        <QrCode size={20} /> QR-код көрсету <ChevronRight size={16} />
      </button>
    </article>
  )
}
