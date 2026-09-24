import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Check, ChevronRight, Plus, QrCode, Search } from 'lucide-react'
import { Avatar, PageHeader } from '../components/PageHeader'
import { PageTitle } from '../components/PageTitle'
import { QrScanner } from '../components/stores/QrScanner'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { ApiError, type StoreListItem } from '../lib/api'
import { storeIcon, storeTheme } from '../lib/theme'
import { useJoinStore, useStores } from '../lib/queries'

export function StoresPage() {
  const [search, setSearch] = useState('')
  const [scanOpen, setScanOpen] = useState(false)
  const [joinError, setJoinError] = useState<string | null>(null)
  const stores = useStores(search)
  const join = useJoinStore()
  const navigate = useNavigate()

  const addStore = async (code: string) => {
    setJoinError(null)
    try {
      const res = await join.mutateAsync(code)
      setScanOpen(false)
      navigate(`/cards/${res.store.id}`)
    } catch (err) {
      setJoinError(err instanceof ApiError ? err.message : 'Дүкенді қосу мүмкін болмады')
    }
  }

  return (
    <>
      <PageHeader right={<Avatar />} />

      <div className="mt-3 flex flex-col gap-4">
        <PageTitle title="Дүкендер" subtitle="Серіктес дүкендерден бонус жинаңыз" />

        <button
          type="button"
          onClick={() => {
            setJoinError(null)
            setScanOpen(true)
          }}
          className="flex h-14 w-full items-center justify-center gap-2.5 rounded-2xl bg-brand text-[15px] font-semibold text-white active:scale-[0.99]"
        >
          <QrCode size={22} /> QR кодты сканерлеу
        </button>

        {joinError && !scanOpen && (
          <div className="rounded-2xl bg-danger-soft px-4 py-3 text-sm text-danger">{joinError}</div>
        )}

        <div className="relative">
          <Search size={18} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-ink-3" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Дүкен іздеу"
            className="h-12 w-full rounded-2xl border border-line bg-surface pl-11 pr-4 text-[15px] outline-none focus:border-brand"
          />
        </div>

        {stores.isPending && (
          <div className="flex flex-col gap-2.5">
            <Skeleton className="h-20" />
            <Skeleton className="h-20" />
            <Skeleton className="h-20" />
          </div>
        )}
        {stores.isError && <ErrorBox message={stores.error.message} onRetry={() => stores.refetch()} />}

        <div className="flex flex-col gap-2.5">
          {stores.data?.map((s) => (
            <StoreRow key={s.id} store={s} onAdd={() => addStore(s.id)} adding={join.isPending} />
          ))}
        </div>

        {stores.data?.length === 0 && (
          <div className="rounded-card bg-surface p-6 text-center text-sm text-ink-2">Дүкен табылмады</div>
        )}
      </div>

      <QrScanner
        open={scanOpen}
        onClose={() => setScanOpen(false)}
        onDetect={addStore}
        busy={join.isPending}
        error={joinError}
      />
    </>
  )
}

function StoreRow({ store, onAdd, adding }: { store: StoreListItem; onAdd: () => void; adding: boolean }) {
  const t = storeTheme(store.themeColor)
  const Icon = storeIcon(store.icon)

  const body = (
    <>
      <div
        className="flex size-12 shrink-0 items-center justify-center rounded-2xl"
        style={{ background: store.themeColor, color: t.text }}
      >
        <Icon size={22} />
      </div>
      <div className="min-w-0 flex-1">
        <div className="truncate text-[15px] font-bold leading-tight">{store.name}</div>
        <div className="truncate text-xs text-ink-2">{store.category} · {store.cashbackPercent}% бонус</div>
        {store.hasCard ? (
          <div className="mt-1 inline-flex items-center gap-1 rounded-full bg-green-soft px-2 py-0.5 text-[11px] font-semibold text-green">
            <Check size={12} strokeWidth={3} /> Сіз қосылғансыз
          </div>
        ) : (
          <div className="mt-0.5 truncate text-xs text-ink-3">{store.description}</div>
        )}
      </div>
      {store.hasCard ? (
        <div className="flex shrink-0 items-center gap-1 text-right">
          <div className="text-[15px] font-bold">{store.balance.toLocaleString('ru-RU')} Б</div>
          <ChevronRight size={18} className="text-ink-3" />
        </div>
      ) : (
        <button
          type="button"
          disabled={adding}
          onClick={onAdd}
          className="flex shrink-0 items-center gap-1 rounded-xl bg-brand-soft px-3 py-2 text-[13px] font-semibold text-brand disabled:opacity-50"
        >
          <Plus size={15} /> Қосу
        </button>
      )}
    </>
  )

  const cls = 'flex items-center gap-3 rounded-2xl bg-surface p-3.5'

  return store.hasCard ? (
    <Link to={`/cards/${store.id}`} className={`${cls} active:scale-[0.99]`}>{body}</Link>
  ) : (
    <div className={cls}>{body}</div>
  )
}
