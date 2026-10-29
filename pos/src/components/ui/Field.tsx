import type { ReactNode, SelectHTMLAttributes } from 'react'
import { cn } from '../../lib/cn'

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

/** Өрістің базалық түрі. Сыртан берілген w-/h-/text- кластары базаны ауыстырады (cn) — өріс көршісін сықпайды. */
const control = 'salem-control w-full rounded-xl border bg-field text-ink outline-none transition-colors focus:border-brand focus:bg-surface disabled:opacity-60'

export function Input({ error, className = '', ...rest }: { error?: boolean } & React.ComponentProps<'input'>) {
  return <input {...rest} className={cn(control, 'h-11 px-3.5', error ? 'border-danger' : 'border-line', className)} />
}

/** Таңдау өрісі: өз көрсеткіші (index.css `.salem-select`), оған оң жақта орын, мәтін сыймаса — көп нүкте. */
export function Select({ error, className = '', children, ...rest }: { error?: boolean } & SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select {...rest} className={cn(control, 'salem-select h-11 pl-3.5 pr-9', error ? 'border-danger' : 'border-line', className)}>
      {children}
    </select>
  )
}

export function Textarea({ className = '', ...rest }: React.ComponentProps<'textarea'>) {
  return <textarea {...rest} className={cn(control, 'min-h-24 border-line px-3.5 py-2.5', className)} />
}

/** Қосу/өшіру ауыстырғышы. */
export function Toggle({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <label className="flex cursor-pointer items-center gap-3">
      <button type="button" role="switch" aria-checked={checked} aria-label={label || undefined} onClick={() => onChange(!checked)}
        className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${checked ? 'bg-brand' : 'bg-line'}`}>
        <span className={`absolute left-0 top-0.5 size-5 rounded-full bg-white shadow transition-transform ${checked ? 'translate-x-5' : 'translate-x-0.5'}`} />
      </button>
      <span className="text-[14px] text-ink">{label}</span>
    </label>
  )
}
