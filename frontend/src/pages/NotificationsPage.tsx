import { CheckCheck } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Avatar, PageHeader } from '../components/PageHeader'
import { PageTitle } from '../components/PageTitle'
import { IconButton } from '../components/IconButton'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import type { NotificationStore } from '../lib/api'
import { formatDateTime } from '../lib/format'
import { storeIcon, storeTheme } from '../lib/theme'
import { useMarkAllRead, useNotificationStores, useUnreadCount } from '../lib/queries'
import { useT, type Translator } from '../lib/i18n'
import { notificationText } from '../lib/notificationText'

/** Хабарламалар дүкендер бойынша жиналады; ішіне кіргенде сол дүкеннің хабарламалары көрінеді. */
export function NotificationsPage() {
  const t = useT()
  const stores = useNotificationStores()
  const unread = useUnreadCount()
  const markAll = useMarkAllRead()

  return (
    <>
      <PageHeader
        right={
          <div className="flex items-center gap-2.5">
            <IconButton icon={CheckCheck} label={t('notif.markAll')} onClick={() => markAll.mutate()} />
            <Avatar />
          </div>
        }
      />

      <div className="mt-3 flex flex-col gap-4">
        <PageTitle
          title={t('notif.title')}
          subtitle={unread.data ? t('notif.unread', { count: unread.data }) : t('notif.subtitle')}
        />

        {stores.isPending && (
          <div className="flex flex-col gap-2.5">
            <Skeleton className="h-[74px]" />
            <Skeleton className="h-[74px]" />
            <Skeleton className="h-[74px]" />
          </div>
        )}
        {stores.isError && <ErrorBox message={stores.error.message} onRetry={() => stores.refetch()} />}

        <div className="flex flex-col gap-2.5">
          {stores.data?.map((g) => <StoreRow key={g.storeId ?? 'system'} group={g} t={t} />)}
        </div>

        {stores.data?.length === 0 && (
          <div className="rounded-card bg-surface p-6 text-center text-sm text-ink-2">{t('notif.noStores')}</div>
        )}
      </div>
    </>
  )
}

function StoreRow({ group, t }: { group: NotificationStore; t: Translator }) {
  const color = group.storeThemeColor ?? '#6B7280'
  const theme = storeTheme(color)
  const Icon = storeIcon(group.storeIcon ?? 'store')
  const preview = notificationText(group.last, t)

  return (
    <Link
      to={`/notifications/${group.storeId ?? 'system'}`}
      className="flex items-center gap-3 rounded-2xl bg-surface p-3.5 active:scale-[0.99]"
    >
      <div
        className="flex size-12 shrink-0 items-center justify-center rounded-2xl"
        style={{ background: color, color: theme.text }}
      >
        <Icon size={22} />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-baseline gap-2">
          <div className="min-w-0 flex-1 truncate text-[15px] font-bold leading-tight">
            {group.storeName ?? t('notif.system')}
          </div>
          <div className="shrink-0 text-[11px] text-ink-3">{formatDateTime(group.last.createdAt)}</div>
        </div>
        <div className={`mt-0.5 truncate text-[13px] ${group.unread > 0 ? 'font-semibold text-ink' : 'text-ink-2'}`}>
          {preview.title}
        </div>
        <div className="truncate text-xs text-ink-3">{t('notif.count', { count: group.total })}</div>
      </div>
      {group.unread > 0 && (
        <div className="flex size-6 shrink-0 items-center justify-center rounded-full bg-brand text-[11px] font-bold text-white">
          {group.unread}
        </div>
      )}
    </Link>
  )
}
