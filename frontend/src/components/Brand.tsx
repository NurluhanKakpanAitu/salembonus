/** Күлкі-белгі: көк дөңгелек ішінде ақ дуга. Иконка, favicon, QR ортасы үшін. */
export function LogoMark({ size = 44 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 100 100" aria-hidden="true">
      <circle cx="50" cy="50" r="50" fill="#0A84F8" />
      <path d="M24 44c7 13 45 13 52 0" fill="none" stroke="#fff" strokeWidth="11" strokeLinecap="round" />
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
      <Wordmark height={24} />
    </div>
  )
}
