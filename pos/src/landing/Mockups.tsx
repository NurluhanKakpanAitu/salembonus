import { BarChart3, Boxes, Calculator, Gift, Package, ShoppingCart, Users, Wallet } from 'lucide-react'
import type { Copy } from './copy'

const fmt = (n: number) => new Intl.NumberFormat('ru-RU').format(n)

/** SalemPos экраны (статистика) — HTML-мен салынған, кез келген өлшемде анық, бренд түстерімен. */
export function PosMockup({ c }: { c: Copy }) {
  const bars = [38, 52, 44, 61, 57, 72, 66, 80, 74, 88, 69, 95]
  const nav = [BarChart3, Calculator, Package, Boxes, ShoppingCart, Wallet, Gift, Users]
  return (
    <div className="overflow-hidden rounded-2xl bg-white shadow-[0_30px_80px_-20px_rgba(11,31,68,0.35)] ring-1 ring-black/5">
      <div className="flex items-center gap-2 border-b border-[#e6e9f0] bg-[#f7f8fb] px-4 py-2.5">
        <span className="size-2.5 rounded-full bg-[#ff5f57]" /><span className="size-2.5 rounded-full bg-[#febc2e]" /><span className="size-2.5 rounded-full bg-[#28c840]" />
        <span className="mx-auto rounded-md bg-white px-3 py-0.5 text-[11px] text-[#5b6576] ring-1 ring-[#e6e9f0]">pos.salemtech.kz</span>
      </div>
      <div className="flex">
        <div className="hidden w-12 shrink-0 flex-col items-center gap-3 bg-[#0c1a33] py-4 sm:flex">
          <span className="mb-1 text-[13px] font-extrabold text-white">S<span className="text-[#1570ff]">P</span></span>
          {nav.map((Icon, i) => (
            <span key={i} className={`flex size-7 items-center justify-center rounded-lg ${i === 0 ? 'bg-[#1570ff] text-white' : 'text-[#c3cde0]'}`}><Icon size={14} /></span>
          ))}
        </div>
        <div className="min-w-0 flex-1 bg-[#f3f5f9] p-3 sm:p-4">
          <div className="mb-3 flex items-center justify-between">
            <span className="text-[14px] font-extrabold text-[#0b1f44]">{c.mock.stats}</span>
            <span className="rounded-lg bg-white px-2 py-1 text-[10px] font-semibold text-[#5b6576] ring-1 ring-[#e6e9f0]">{c.mock.period}</span>
          </div>
          <div className="grid grid-cols-3 gap-2">
            {[{ l: c.mock.revenue, v: `${fmt(2485000)} ₸`, d: '+18%' }, { l: c.mock.receipts, v: fmt(612), d: '+9%' }, { l: c.mock.average, v: `${fmt(4060)} ₸`, d: '+6%' }].map((k) => (
              <div key={k.l} className="min-w-0 rounded-xl bg-white p-2.5 ring-1 ring-[#e6e9f0]">
                <div className="truncate text-[9px] text-[#5b6576] sm:text-[10px]">{k.l}</div>
                <div className="truncate text-[12px] font-extrabold text-[#0b1f44] sm:text-[14px]">{k.v}</div>
                <div className="text-[9px] font-semibold text-[#16a34a]">{k.d}</div>
              </div>
            ))}
          </div>
          <div className="mt-2 rounded-xl bg-white p-3 ring-1 ring-[#e6e9f0]">
            <div className="flex h-24 items-end gap-1.5 sm:h-28">
              {bars.map((h, i) => (
                <div key={i} className={`flex-1 rounded-t-[4px] ${i === bars.length - 1 ? 'bg-[#1570ff]' : 'bg-[#1570ff]/25'}`} style={{ height: `${h}%` }} />
              ))}
            </div>
          </div>
          <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
            <div className="rounded-xl bg-white p-2.5 ring-1 ring-[#e6e9f0]">
              <div className="mb-1.5 text-[10px] font-bold text-[#0b1f44]">{c.mock.top}</div>
              {c.mock.products.map((p, i) => (
                <div key={p} className="mb-1.5 last:mb-0">
                  <div className="flex justify-between text-[9px] text-[#5b6576]"><span className="truncate">{p}</span><span>{[34, 27, 19, 12][i]}%</span></div>
                  <div className="mt-0.5 h-1 rounded-full bg-[#eef1f6]"><div className="h-1 rounded-full bg-[#1570ff]" style={{ width: `${[90, 72, 50, 32][i]}%` }} /></div>
                </div>
              ))}
            </div>
            <div className="hidden rounded-xl bg-white p-2.5 ring-1 ring-[#e6e9f0] sm:block">
              <div className="mb-1.5 text-[10px] font-bold text-[#0b1f44]">{c.mock.recent}</div>
              {[[1284, 15000], [1283, 4200], [1282, 27500], [1281, 1850]].map(([n, a]) => (
                <div key={n} className="flex justify-between border-b border-[#f0f2f6] py-1 text-[9px] last:border-0">
                  <span className="text-[#5b6576]">{c.mock.receipt} №{n}</span><span className="font-semibold text-[#0b1f44]">{fmt(a)} ₸</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

/** Телефон жақтауы: SalemBonus қосымшасының нақты экраны. */
export function Phone({ src, alt, className = '' }: { src: string; alt: string; className?: string }) {
  return (
    <div className={`rounded-[2.4rem] bg-[#0b1220] p-[7px] shadow-[0_30px_60px_-15px_rgba(11,18,32,0.45)] ring-1 ring-white/10 ${className}`}>
      <div className="relative overflow-hidden rounded-[2rem] bg-white">
        <span className="absolute left-1/2 top-2 z-10 h-[18px] w-[72px] -translate-x-1/2 rounded-full bg-[#0b1220]" />
        <img src={src} alt={alt} className="block aspect-[560/1213] w-full object-cover object-top" decoding="async" />
      </div>
    </div>
  )
}
