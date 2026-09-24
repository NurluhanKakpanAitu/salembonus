import { useMemo } from 'react'
import { CheckCheck, ChevronDown } from 'lucide-react'
import { Avatar, PageHeader } from '../components/PageHeader'
import { PageTitle } from '../components/PageTitle'
import { IconButton } from '../components/IconButton'
import { NotificationItem } from '../components/notifications/NotificationItem'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import type { Notification } from '../lib/api'
import { dayKey, dayLabel } from '../lib/format'
import { useMarkAllRead, useMarkRead, useNotifications, useUnreadCount } from '../lib/queries'
import { useT } from '../lib/i18n'

export function NotificationsPage() {
  const t = useT()
  const q = useNotifications(null)
  const unread = useUnreadCount()
  const markRead = useMarkRead()
  const markAll = useMarkAllRead()

  const groups = useMemo(() => {
    const items = q.data?.pages.flatMap((p) => p.items) ?? []
    const map = new Map<string, Notification[]>()
    for (const n of items) {
      const k = dayKey(n.createdAt)
      map.set(k, [...(map.get(k) ?? []), n])
    }
    return [...map.values()]
  }, [q.data])

  const open = (n: Notification) => {
    if (!n.isRead) markRead.mutate(n.id)
  }

  return (
    <>
      <PageHeader
        right={
          <div className="flex items-center gap-2.5">
            <IconButton
              icon={CheckCheck}
              label={t('notif.markAll')}
              onClick={() => markAll.mutate()}
            />
            <Avatar />
          </div>
        }
      />

      <div className="mt-3 flex flex-col gap-4">
        <PageTitle
          title={t('notif.title')}
          subtitle={unread.data ? t('notif.unread', { count: unread.data }) : t('notif.subtitle')}
        />

        {q.isPending && (
          <>
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
          </>
        )}
        {q.isError && <ErrorBox message={q.error.message} onRetry={() => q.refetch()} />}

        {groups.map((items) => (
          <section key={dayKey(items[0].createdAt)} className="flex flex-col gap-2.5">
            <h2 className="text-[15px] font-bold">{dayLabel(items[0].createdAt)}</h2>
            {items.map((n) => (
              <NotificationItem key={n.id} n={n} onOpen={open} />
            ))}
          </section>
        ))}

        {q.data && groups.length === 0 && (
          <div className="rounded-card bg-surface p-6 text-center text-sm text-ink-2">{t('notif.empty')}</div>
        )}

        {q.hasNextPage && (
          <div className="flex justify-center">
            <button
              type="button"
              disabled={q.isFetchingNextPage}
              onClick={() => q.fetchNextPage()}
              className="flex items-center gap-1.5 rounded-full bg-gray-200 px-4 py-2.5 text-[13px] font-medium text-ink-2 disabled:opacity-60"
            >
              {q.isFetchingNextPage ? t('common.loadingShort') : t('notif.showOlder')} <ChevronDown size={16} />
            </button>
          </div>
        )}
      </div>
    </>
  )
}
