/**
 * SalemPos белгісі: «Salem» + көк «Pos», астында «Sa» тұсында көк күлкі-дуга.
 * Слоган берілсе — ол дугамен бір жолда, оның оң жағында тұрады (макет бойынша).
 * tone="light" — қара фонда (ақ «Salem»), tone="dark" — ашық фонда.
 */
export function Logo({ size = 28, tone = 'light', tagline }: {
  size?: number
  tone?: 'light' | 'dark'
  tagline?: string
}) {
  return (
    <div className="inline-flex flex-col">
      <span
        className={`whitespace-nowrap font-extrabold leading-none tracking-[-0.035em] ${
          tone === 'light' ? 'text-white' : 'text-ink'
        }`}
        style={{ fontSize: size }}
      >
        Salem<span className="text-brand">Pos</span>
      </span>
      <div className="flex items-center" style={{ marginTop: size * 0.02 }}>
        <svg
          aria-hidden="true"
          viewBox="0 0 100 30"
          className="shrink-0"
          style={{ width: size * 0.98, height: size * 0.3, marginLeft: size * 0.04 }}
        >
          <path d="M8 6c15 22 69 22 84 0" fill="none" stroke="#1570ff" strokeWidth="10" strokeLinecap="round" />
        </svg>
        {tagline && (
          <span
            className={`whitespace-nowrap font-medium uppercase leading-none tracking-[0.3em] ${
              tone === 'light' ? 'text-white/85' : 'text-ink-2'
            }`}
            style={{ fontSize: size * 0.2, marginLeft: size * 0.14 }}
          >
            {tagline}
          </span>
        )}
      </div>
    </div>
  )
}
