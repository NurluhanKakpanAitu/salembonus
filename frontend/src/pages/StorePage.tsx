import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Crown, MapPin, Phone } from 'lucide-react'
import { BackHeader } from '../components/BackHeader'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { ApiError, type StoreDetail, type StoreLevel } from '../lib/api'
import { formatTenge } from '../lib/format'
import { useT, type TranslationKey, type Translator } from '../lib/i18n'
import { storeCategory } from '../lib/storeCategory'
import { storeIcon, storeTheme } from '../lib/theme'
import { useJoinStore, useStore } from '../lib/queries'

/** Әлі қосылмаған дүкеннің беті: сипаттама, байланыс, бонус жүйесі және қосу батырмасы. */
export function StorePage() {
  const t = useT()
  const { storeId = '' } = useParams()
  const store = useStore(storeId)
  const join = useJoinStore()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)

  const connect = async () => {
    setError(null)
    try {
      const res = await join.mutateAsync(storeId)
      navigate(`/cards/${res.store.id}`, { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('stores.joinFailed'))
    }
  }

  return (
    <>
      <BackHeader
        title={store.data?.name ?? '…'}
        subtitle={store.data ? storeCategory(store.data.category) : undefined}
        fallback="/stores"
      />

      {store.isPending && <Skeleton className="mt-4 h-80" />}
      {store.isError && <ErrorBox message={store.error.message} onRetry={() => store.refetch()} />}

      {store.data && (
        <div className="mt-3 flex flex-col gap-4">
          <Cover store={store.data} />

          <section className="rounded-card bg-surface p-4">
            <h2 className="text-[15px] font-bold">{t('store.about')}</h2>
            <p className="mt-1.5 text-sm leading-snug text-ink-2">{store.data.description}</p>
          </section>

          {(store.data.address || store.data.phone) && (
            <section className="rounded-card bg-surface p-4">
              <h2 className="mb-2 text-[15px] font-bold">{t('store.contacts')}</h2>
              {store.data.address && (
                <div className="flex items-start gap-2.5 py-1.5 text-sm">
                  <MapPin size={17} className="mt-0.5 shrink-0 text-ink-3" />
                  <span>{store.data.address}</span>
                </div>
              )}
              {store.data.phone && (
                <a href={`tel:${store.data.phone.replace(/\s/g, '')}`} className="flex items-center gap-2.5 py-1.5 text-sm font-semibold text-brand">
                  <Phone size={17} className="shrink-0" />
                  {store.data.phone}
                </a>
              )}
            </section>
          )}

          <LevelLadder levels={store.data.levels} t={t} />

          <p className="px-1 text-xs text-ink-3">
            {t('store.maxRedeem', { percent: store.data.maxRedeemPercent })}
          </p>

          {error && <div className="rounded-2xl bg-danger-soft px-4 py-3 text-sm text-danger">{error}</div>}

          {store.data.hasCard ? (
            <button
              type="button"
              onClick={() => navigate(`/cards/${store.data!.id}`)}
              className="h-13 w-full rounded-2xl bg-brand text-[15px] font-semibold text-white active:scale-[0.99]"
            >
              {t('store.openCard')}
            </button>
          ) : (
            <button
              type="button"
              disabled={join.isPending}
              onClick={connect}
              className="h-13 w-full rounded-2xl bg-brand text-[15px] font-semibold text-white active:scale-[0.99] disabled:opacity-50"
            >
              {join.isPending ? t('store.connecting') : t('store.connect')}
            </button>
          )}
        </div>
      )}
    </>
  )
}

/** Дүкен суреті. Сурет жүктелмеген болса — дүкен түсіндегі мұқаба мен иконка. */
function Cover({ store }: { store: StoreDetail }) {
  const theme = storeTheme(store.themeColor)
  const Icon = storeIcon(store.icon)

  return (
    <div
      className="flex h-40 items-center justify-center overflow-hidden rounded-card"
      style={{ background: theme.bg, color: theme.text }}
    >
      {store.photoUrl ? (
        <img src={store.photoUrl} alt="" className="size-full object-cover" />
      ) : (
        <Icon size={64} strokeWidth={1.4} />
      )}
    </div>
  )
}

function LevelLadder({ levels, t }: { levels: StoreLevel[]; t: Translator }) {
  return (
    <section className="rounded-card bg-surface p-4">
      <div className="mb-3 flex items-center gap-2">
        <Crown size={18} className="text-gold" />
        <h2 className="text-[15px] font-bold">{t('store.bonusSystem')}</h2>
      </div>
      <ul className="flex flex-col">
        {levels.map((l, i) => (
          <li
            key={l.name}
            className={`flex items-center gap-3 py-2.5 ${i < levels.length - 1 ? 'border-b border-line' : ''}`}
          >
            <div className="min-w-0 flex-1">
              <div className="truncate text-[15px] font-semibold">{t(`level.${l.name}` as TranslationKey)}</div>
              <div className="text-xs text-ink-2">
                {l.fromAmount > 0 ? t('store.fromAmount', { amount: formatTenge(l.fromAmount) }) : t('store.startLevel')}
              </div>
            </div>
            <div className="shrink-0 text-lg font-extrabold text-brand">{l.cashbackPercent}%</div>
          </li>
        ))}
      </ul>
    </section>
  )
}
