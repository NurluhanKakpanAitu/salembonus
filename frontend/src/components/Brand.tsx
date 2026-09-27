/** Қосымша белгісі: көк градиентті дөңгелектелген шаршы ішінде ақ күлкі. */
export function LogoMark({ size = 44 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 100 100" aria-hidden="true">
      <defs>
        <linearGradient id="salembonus-mark" x1="0" y1="1" x2="1" y2="0">
          <stop offset="0" stopColor="#0152FE" />
          <stop offset="1" stopColor="#0297FD" />
        </linearGradient>
      </defs>
      <rect width="100" height="100" rx="24" fill="url(#salembonus-mark)" />
      <path d="M26 46c7 15 41 15 48 0" fill="none" stroke="#fff" strokeWidth="11" strokeLinecap="round" />
    </svg>
  )
}

/** Wordmark: Salem (қара) + Bonus (көк), "Sa" астында көк күлкі-дуга. */
export function Wordmark({ height = 26 }: { height?: number }) {
  return (
    <span
      className="relative inline-block whitespace-nowrap font-logo font-extrabold leading-none tracking-[-0.03em] text-ink"
      style={{ fontSize: height }}
    >
      Salem<span className="text-brand">Bonus</span>
      <svg
        aria-hidden="true"
        viewBox="0 0 100 30"
        className="absolute left-[0.04em] top-[0.98em] w-[0.98em]"
        style={{ height: '0.3em' }}
      >
        <path d="M8 6c15 22 69 22 84 0" fill="none" stroke="#0A84F8" strokeWidth="9" strokeLinecap="round" />
      </svg>
    </span>
  )
}

export function Brand() {
  return (
    <div className="min-w-0 py-2">
      <Wordmark height={20} />
    </div>
  )
}
