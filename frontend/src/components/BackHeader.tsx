import { ChevronLeft } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useT } from '../lib/i18n'

export function BackHeader({ title, subtitle, fallback = '/' }: { title: string; subtitle?: string; fallback?: string }) {
  const navigate = useNavigate()
  const t = useT()
  const back = () => (window.history.length > 1 ? navigate(-1) : navigate(fallback))
  return (
    <header className="flex items-center gap-3 py-2">
      <button type="button" aria-label={t('common.back')} onClick={back} className="flex size-10 items-center justify-center rounded-full bg-surface text-ink">
        <ChevronLeft size={22} />
      </button>
      <div className="min-w-0">
        <h1 className="truncate text-xl font-extrabold leading-tight tracking-tight">{title}</h1>
        {subtitle && <div className="text-xs text-ink-2">{subtitle}</div>}
      </div>
    </header>
  )
}
