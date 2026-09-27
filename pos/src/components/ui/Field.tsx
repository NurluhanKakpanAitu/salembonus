import type { ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react'

/** Жапсырма + өріс + қате. Модалды формаларда бірдей көрінсін. */
export function Field({ label, error, required, children, hint }: {
  label: string
  error?: string | null
  required?: boolean
  hint?: string
  children: ReactNode
}) {
  return (
    <div>
      <div className="mb-1.5 text-[13px] font-medium text-ink-2">
        {label}{required && <span className="text-danger"> *</span>}
      </div>
      {children}
      {error ? <p className="mt-1.5 text-[13px] text-danger">{error}</p> : hint && <p className="mt-1.5 text-[12px] text-ink-3">{hint}</p>}
    </div>
  )
}

const control = 'w-full rounded-xl border bg-field px-3.5 text-[15px] text-ink outline-none transition-colors focus:border-brand focus:bg-surface'

export function Input({ error, className = '', ...rest }: { error?: boolean } & React.ComponentProps<'input'>) {
  return <input {...rest} className={`${control} h-11 ${error ? 'border-danger' : 'border-line'} ${className}`} />
}

export function Select({ error, className = '', children, ...rest }: { error?: boolean } & SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select {...rest} className={`${control} h-11 ${error ? 'border-danger' : 'border-line'} ${className}`}>
      {children}
    </select>
  )
}

export function Textarea({ className = '', ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...rest} className={`${control} min-h-24 border-line py-2.5 ${className}`} />
}

/** Қосу/өшіру ауыстырғышы. */
export function Toggle({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <label className="flex cursor-pointer items-center gap-3">
      <button type="button" role="switch" aria-checked={checked} onClick={() => onChange(!checked)}
        className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${checked ? 'bg-brand' : 'bg-line'}`}>
        <span className={`absolute top-0.5 size-5 rounded-full bg-white shadow transition-transform ${checked ? 'translate-x-5' : 'translate-x-0.5'}`} />
      </button>
      <span className="text-[14px] text-ink">{label}</span>
    </label>
  )
}
