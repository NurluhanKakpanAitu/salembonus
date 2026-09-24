import { useT } from '../lib/i18n'

export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded-card bg-gray-200/70 ${className}`} />
}

export function ErrorBox({ message, onRetry }: { message: string; onRetry?: () => void }) {
  const t = useT()
  return (
    <div className="rounded-card border border-danger/20 bg-danger-soft p-4 text-sm text-ink">
      <div className="font-semibold text-danger">{t('common.loadFailed')}</div>
      <div className="mt-1 text-ink-2">{message}</div>
      {onRetry && (
        <button type="button" onClick={onRetry} className="mt-3 rounded-lg bg-brand px-3 py-1.5 text-xs font-semibold text-white">
          {t('common.retry')}
        </button>
      )}
    </div>
  )
}
