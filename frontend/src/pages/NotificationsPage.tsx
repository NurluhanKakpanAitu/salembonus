import { ChevronRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import type { NotificationStore } from '../lib/api'
import { formatDateTime } from '../lib/format'
import { storeIcon } from '../lib/theme'
import { useNotificationStores } from '../lib/queries'
import { useT, type Translator } from '../lib/i18n'
import { notificationText } from '../lib/notificationText'

/** Дүкендермен жазысу тізімі: әр дүкеннің соңғы хабарламасы көрінеді. */
export function NotificationsPage() {
  const t = useT()
  const stores = useNotificationStores()

  return (
    <>
      <header className="py-3 text-center">
        <h1 className="text-[17px] font-bold">{t('notif.title')}</h1>
      </header>

      <div className="mt-1 flex flex-col gap-2.5">
        {stores.isPending && (
          <>
            <Skeleton className="h-[80px]" />
            <Skeleton className="h-[80px]" />
            <Skeleton className="h-[80px]" />
          </>
        )}
        {stores.isError && <ErrorBox message={stores.error.message} onRetry={() => stores.refetch()} />}

        {stores.data?.map((g) => <StoreRow key={g.storeId ?? 'system'} group={g} t={t} />)}

        {stores.data?.length === 0 && (
          <div className="rounded-card bg-surface p-6 text-center text-sm text-ink-2">{t('notif.noStores')}</div>
        )}
      </div>
    </>
  )
}

function StoreRow({ group, t }: { group: NotificationStore; t: Translator }) {
  const color = group.storeThemeColor ?? '#6B7280'
  const Icon = storeIcon(group.storeIcon ?? 'store')
  const preview = notificationText(group.last, t)
  const unread = group.unread > 0

  return (
    <Link
      to={`/notifications/${group.storeId ?? 'system'}`}
      className="flex items-center gap-3 rounded-2xl bg-surface p-3 active:scale-[0.99]"
    >
      <div
        className="flex size-12 shrink-0 items-center justify-center rounded-2xl"
        style={{ background: `${color}1F`, color }}
      >
        <Icon size={22} />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-baseline gap-2">
          <div className="min-w-0 flex-1 truncate text-[16px] font-bold leading-tight">
            {group.storeName ?? t('notif.system')}
          </div>
          <div className={`shrink-0 text-[12px] ${unread ? 'font-semibold text-brand' : 'text-ink-3'}`}>
            {formatDateTime(group.last.createdAt)}
          </div>
        </div>
        <div className={`mt-1 truncate text-[14px] ${unread ? 'font-semibold text-ink' : 'text-ink-2'}`}>
          {preview.title}
        </div>
      </div>
      <ChevronRight size={18} className="shrink-0 text-ink-3" />
    </Link>
  )
}
