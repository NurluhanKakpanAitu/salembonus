import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { Loader2 } from 'lucide-react'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'

const variants: Record<Variant, string> = {
  primary: 'bg-brand text-white hover:bg-brand-hover disabled:bg-brand/60',
  secondary: 'border border-line bg-surface text-ink hover:bg-field',
  ghost: 'text-ink-2 hover:bg-field hover:text-ink',
  danger: 'bg-danger text-white hover:bg-danger/90',
}

/** Негізгі батырма. loading кезінде басылмайды — форма екі рет жіберілмейді (ТЗ §4.12). */
export function Button({ variant = 'primary', loading = false, icon, iconRight, className = '', children, disabled, ...rest }: {
  variant?: Variant
  loading?: boolean
  icon?: ReactNode
  iconRight?: ReactNode
} & ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...rest}
      disabled={disabled || loading}
      className={`inline-flex h-11 items-center justify-center gap-2 rounded-xl px-5 text-[15px] font-semibold transition-colors disabled:cursor-not-allowed ${variants[variant]} ${className}`}
    >
      {loading ? <Loader2 size={18} className="animate-spin" /> : icon}
      {children}
      {!loading && iconRight}
    </button>
  )
}
