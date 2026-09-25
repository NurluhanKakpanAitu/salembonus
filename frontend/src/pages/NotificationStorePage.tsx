import { ChevronDown } from 'lucide-react'
import { useParams } from 'react-router-dom'
import { BackHeader } from '../components/BackHeader'
import { NotificationItem } from '../components/notifications/NotificationItem'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import type { Notification } from '../lib/api'
import { useMarkRead, useNotifications, useNotificationStores } from '../lib/queries'
import { useT } from '../lib/i18n'

/** Бір дүкеннің хабарламалары. source "system" болса — дүкенге қатысы жоқ хабарламалар. */
export function NotificationStorePage() {
  const t = useT()
  const { source = 'system' } = useParams()
  const q = useNotifications(null, source)
  const markRead = useMarkRead()
  const stores = useNotificationStores()

  const items = q.data?.pages.flatMap((p) => p.items) ?? []
  const group = stores.data?.find((g) => (g.storeId ?? 'system') === source)
  const title = group?.storeName ?? items[0]?.storeName ?? t('notif.system')

  const open = (n: Notification) => {
    if (!n.isRead) markRead.mutate(n.id)
  }

  return (
    <>
      <BackHeader
        title={title}
        subtitle={items.length > 0 ? t('notif.count', { count: group?.total ?? items.length }) : undefined}
        fallback="/notifications"
      />

      <div className="mt-3 flex flex-col gap-2.5">
        {q.isPending && (
          <>
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
          </>
        )}
        {q.isError && <ErrorBox message={q.error.message} onRetry={() => q.refetch()} />}

        {items.map((n) => (
          <NotificationItem key={n.id} n={n} onOpen={open} />
        ))}

        {q.data && items.length === 0 && (
          <div className="rounded-card bg-surface p-6 text-center text-sm text-ink-2">{t('notif.empty')}</div>
        )}

        {q.hasNextPage && (
          <div className="flex justify-center pt-1">
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
