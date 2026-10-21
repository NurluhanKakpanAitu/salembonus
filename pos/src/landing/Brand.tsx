/**
 * Salem белгілері бір отбасы (SalemPos `components/Logo.tsx`, SalemBonus `frontend/src/components/Brand.tsx` сияқты):
 * «Salem» + көк өнім сөзі, «Sa» астында көк күлкі-дуга; қосымша белгісі — көк градиентті шаршы ішінде ақ күлкі.
 */
export function SalemWordmark({ word, size = 22, tone = 'dark', color = '#1570ff' }: {
  word: string
  size?: number
  tone?: 'dark' | 'light'
  color?: string
}) {
  return (
    <span className="inline-flex flex-col" style={{ paddingBottom: size * 0.22 }}>
      <span className={`relative whitespace-nowrap font-extrabold leading-none tracking-[-0.035em] ${tone === 'light' ? 'text-white' : 'text-[#0b1220]'}`} style={{ fontSize: size }}>
        Salem<span style={{ color }}>{word}</span>
        <svg aria-hidden="true" viewBox="0 0 100 30" className="absolute left-[0.04em] top-[0.98em] w-[0.98em]" style={{ height: '0.3em' }}>
          <path d="M8 6c15 22 69 22 84 0" fill="none" stroke={color} strokeWidth="10" strokeLinecap="round" />
        </svg>
      </span>
    </span>
  )
}

export function AppMark({ size = 32, id = 'salem-mark' }: { size?: number; id?: string }) {
  return (
    <svg width={size} height={size} viewBox="0 0 100 100" aria-hidden="true" className="shrink-0">
      <defs>
        <linearGradient id={id} x1="0" y1="1" x2="1" y2="0">
          <stop offset="0" stopColor="#0152FE" />
          <stop offset="1" stopColor="#0297FD" />
        </linearGradient>
      </defs>
      <rect width="100" height="100" rx="24" fill={`url(#${id})`} />
      <path d="M26 46c7 15 41 15 48 0" fill="none" stroke="#fff" strokeWidth="11" strokeLinecap="round" />
    </svg>
  )
}
