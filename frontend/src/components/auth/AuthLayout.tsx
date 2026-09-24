import type { ReactNode } from 'react'
import { Brand } from '../Brand'
import { useT } from '../../lib/i18n'

export function AuthLayout({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <div className="mx-auto flex min-h-full max-w-[480px] flex-col bg-bg px-6 pb-[max(24px,env(safe-area-inset-bottom))] pt-[max(40px,env(safe-area-inset-top))]">
      <div className="flex justify-center">
        <Brand />
      </div>
      <div className="mt-12">
        <h1 className="text-[26px] font-extrabold leading-tight tracking-tight">{title}</h1>
        {subtitle && <p className="mt-1.5 text-sm text-ink-2">{subtitle}</p>}
      </div>
      <div className="mt-8 flex flex-1 flex-col">{children}</div>
    </div>
  )
}

export function PrimaryButton({ children, loading, ...rest }: React.ButtonHTMLAttributes<HTMLButtonElement> & { loading?: boolean }) {
  const t = useT()
  return (
    <button
      type="submit"
      {...rest}
      disabled={rest.disabled || loading}
      className="mt-6 h-13 w-full rounded-2xl bg-brand text-[15px] font-semibold text-white transition active:scale-[0.99] disabled:opacity-50"
    >
      {loading ? t('common.loading') : children}
    </button>
  )
}

export function FieldError({ message }: { message?: string | null }) {
  if (!message) return null
  return <p className="mt-2 text-[13px] text-danger">{message}</p>
}
