import { useEffect, useMemo } from 'react'
import { ChevronDown, ChevronLeft } from 'lucide-react'
import { useNavigate, useParams } from 'react-router-dom'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import type { Notification } from '../lib/api'
import { dayKey, dayLabel, formatTime } from '../lib/format'
import { storeIcon } from '../lib/theme'
import { useMarkAllRead, useNotificationStores, useNotifications } from '../lib/queries'
import { useT, type Translator } from '../lib/i18n'
import { notificationText } from '../lib/notificationText'

/** Бір дүкенмен жазысу: хабарламалар күні бойынша топталып, көпіршік түрінде көрінеді. */
export function NotificationStorePage() {
  const t = useT()
  const navigate = useNavigate()
  const { source = 'system' } = useParams()
  const q = useNotifications(null, source)
  const stores = useNotificationStores()
  const markAll = useMarkAllRead()

  const group = stores.data?.find((g) => (g.storeId ?? 'system') === source)
  const items = q.data?.pages.flatMap((p) => p.items) ?? []
  const title = group?.storeName ?? items[0]?.storeName ?? t('notif.system')
  const color = group?.storeThemeColor ?? '#6B7280'
  const Icon = storeIcon(group?.storeIcon ?? 'store')

  // Жазысуды ашқан бойда барлық хабарлама оқылды деп белгіленеді.
  const unread = group?.unread ?? 0
  useEffect(() => {
    if (unread > 0) markAll.mutate(source)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [source, unread > 0])

  /** Ескіден жаңаға қарай, күн бойынша топтар. */
  const days = useMemo(() => {
    const map = new Map<string, Notification[]>()
    for (const n of [...items].reverse()) {
      const key = dayKey(n.createdAt)
      map.set(key, [...(map.get(key) ?? []), n])
    }
    return [...map.values()]
  }, [q.data])

  return (
    <>
      <header className="flex items-center gap-3 py-2">
        <button
          type="button"
          aria-label={t('common.back')}
          onClick={() => navigate('/notifications')}
          className="flex size-10 shrink-0 items-center justify-center rounded-full bg-surface text-ink"
        >
          <ChevronLeft size={22} />
        </button>
        <div className="flex min-w-0 flex-1 items-center justify-center gap-2.5">
          <div
            className="flex size-9 shrink-0 items-center justify-center rounded-xl"
            style={{ background: `${color}1F`, color }}
          >
            <Icon size={18} />
          </div>
          <h1 className="min-w-0 truncate text-[17px] font-bold">{title}</h1>
        </div>
        <div className="size-10 shrink-0" />
      </header>

      <div className="mt-2 flex flex-col gap-2">
        {q.isPending && (
          <>
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
          </>
        )}
        {q.isError && <ErrorBox message={q.error.message} onRetry={() => q.refetch()} />}

        {q.hasNextPage && (
          <div className="flex justify-center">
            <button
              type="button"
              disabled={q.isFetchingNextPage}
              onClick={() => q.fetchNextPage()}
              className="flex items-center gap-1.5 rounded-full bg-muted px-4 py-2 text-[13px] font-medium text-ink-2 disabled:opacity-60"
            >
              {q.isFetchingNextPage ? t('common.loadingShort') : t('notif.showOlder')} <ChevronDown size={15} />
            </button>
          </div>
        )}

        {days.map((dayItems) => (
          <section key={dayKey(dayItems[0].createdAt)} className="flex flex-col gap-2">
            <div className="flex justify-center">
              <span className="rounded-full bg-muted px-3 py-1 text-[12px] font-medium text-ink-2">
                {dayLabel(dayItems[0].createdAt)}
              </span>
            </div>
            {dayItems.map((n) => (
              <Bubble key={n.id} n={n} t={t} />
            ))}
          </section>
        ))}

        {q.data && items.length === 0 && (
          <div className="rounded-card bg-surface p-6 text-center text-sm text-ink-2">{t('notif.empty')}</div>
        )}
      </div>
    </>
  )
}

function Bubble({ n, t }: { n: Notification; t: Translator }) {
  const text = notificationText(n, t, { short: true })

  return (
    <article className="max-w-[80%] self-start rounded-2xl rounded-bl-md bg-surface px-3.5 py-3">
      <div className="flex items-baseline gap-3">
        <h2 className="min-w-0 flex-1 text-[15px] font-bold leading-snug">{text.title}</h2>
        <span className="shrink-0 text-[11px] text-ink-3">{formatTime(n.createdAt)}</span>
      </div>
      <p className="mt-1 text-[14px] leading-snug text-ink">{text.body}</p>
      {text.detail && <p className="mt-1 text-[13px] text-ink-2">{text.detail}</p>}
    </article>
  )
}
