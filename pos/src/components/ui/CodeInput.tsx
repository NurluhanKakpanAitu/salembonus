import { useRef, useState } from 'react'

/**
 * Растау кодының өрісі (ТЗ «Авторизация» §6.3–6.5): көзге 4 бөлек ұяшық, астында бір нақты өріс.
 *
 * Әр ұяшықты бөлек input етсек, жылдам тергенде немесе кодты қойғанда цифрлар жоғалады
 * (әр өріс ескі мәнді көреді). Бір өріс мұны толық шешеді: цифр келесі ұяшыққа өзі «өтеді»,
 * Backspace алдыңғысына қайтарады, көшіріп қою мен телефонның кодты автоматты ұсынуы жұмыс істейді.
 */
export function CodeInput({ length = 4, value, onChange, error, autoFocus, label }: {
  length?: number
  value: string
  onChange: (value: string) => void
  error?: boolean
  autoFocus?: boolean
  label: string
}) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [focused, setFocused] = useState(false)
  const active = Math.min(value.length, length - 1)

  return (
    <div className="relative mx-auto flex w-fit justify-center gap-3" onClick={() => inputRef.current?.focus()}>
      {Array.from({ length }, (_, i) => (
        <span
          key={i}
          aria-hidden="true"
          className={`flex size-15 items-center justify-center rounded-xl border bg-field text-[26px] font-bold text-ink transition-colors ${
            error ? 'border-danger' : focused && i === active ? 'border-brand bg-surface' : 'border-line'
          }`}
        >
          {value[i] ?? ''}
        </span>
      ))}
      <input
        ref={inputRef}
        value={value}
        onChange={(e) => onChange(e.target.value.replace(/\D/g, '').slice(0, length))}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        inputMode="numeric"
        autoComplete="one-time-code"
        autoFocus={autoFocus}
        maxLength={length}
        aria-label={label}
        aria-invalid={error}
        className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
      />
    </div>
  )
}
