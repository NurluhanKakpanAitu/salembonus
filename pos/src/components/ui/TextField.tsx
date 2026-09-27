import { forwardRef, useId, useState, type InputHTMLAttributes, type ReactNode } from 'react'
import { Eye, EyeOff } from 'lucide-react'

/** Белгішесі, қатесі және (құпиясөз үшін) көрсету/жасыру батырмасы бар өріс. */
export const TextField = forwardRef<HTMLInputElement, {
  label?: string
  icon?: ReactNode
  error?: string | null
  /** Құпиясөз өрісі: оң жақта көз белгісі. */
  secret?: boolean
  showLabel?: string
  hideLabel?: string
} & InputHTMLAttributes<HTMLInputElement>>(function TextField(
  { label, icon, error, secret, showLabel = 'Show', hideLabel = 'Hide', className = '', id, ...rest },
  ref,
) {
  const autoId = useId()
  const inputId = id ?? autoId
  const [visible, setVisible] = useState(false)

  return (
    <div className={className}>
      {label && (
        <label htmlFor={inputId} className="mb-1.5 block text-[13px] font-medium text-ink-2">
          {label}
        </label>
      )}
      <div
        className={`flex h-13 items-center gap-3 rounded-xl border bg-field px-4 transition-colors focus-within:border-brand focus-within:bg-surface ${
          error ? 'border-danger' : 'border-line'
        }`}
      >
        {icon && <span className="shrink-0 text-ink-3">{icon}</span>}
        <input
          ref={ref}
          id={inputId}
          {...rest}
          type={secret ? (visible ? 'text' : 'password') : rest.type}
          aria-invalid={!!error}
          aria-describedby={error ? `${inputId}-error` : undefined}
          className="h-full min-w-0 flex-1 bg-transparent text-[15px] text-ink outline-none placeholder:text-ink-3"
        />
        {secret && (
          <button
            type="button"
            onClick={() => setVisible((v) => !v)}
            aria-label={visible ? hideLabel : showLabel}
            className="shrink-0 text-ink-3 hover:text-ink-2"
          >
            {visible ? <Eye size={19} /> : <EyeOff size={19} />}
          </button>
        )}
      </div>
      {error && (
        <p id={`${inputId}-error`} className="mt-1.5 text-[13px] text-danger">
          {error}
        </p>
      )}
    </div>
  )
})
