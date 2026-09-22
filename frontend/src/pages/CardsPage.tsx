import { useState } from 'react'
import { Bell, CirclePlus, Wallet, X } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { Avatar, PageHeader } from '../components/PageHeader'
import { PageTitle } from '../components/PageTitle'
import { IconButton } from '../components/IconButton'
import { StoreCard } from '../components/cards/StoreCard'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { useCards } from '../lib/queries'

export function CardsPage() {
  const cards = useCards()
  const navigate = useNavigate()
  const [promoOpen, setPromoOpen] = useState(true)

  return (
    <>
      <PageHeader
        right={
          <div className="flex items-center gap-2.5">
            <IconButton icon={Bell} dot label="Хабарламалар" onClick={() => navigate('/notifications')} />
            <Avatar />
          </div>
        }
      />

      <div className="mt-3 flex flex-col gap-4">
        <PageTitle title="Менің карталарым" subtitle="Барлық бонус карталар бір жерде" />

        <Link
          to="/stores"
          className="flex items-center justify-center gap-2 rounded-[14px] bg-violet-soft px-4 py-3 text-sm font-semibold text-violet"
        >
          <CirclePlus size={20} /> Дүкен қосу
        </Link>

        {cards.isPending && (
          <>
            <Skeleton className="h-[150px] rounded-[20px]" />
            <Skeleton className="h-[150px] rounded-[20px]" />
          </>
        )}
        {cards.isError && <ErrorBox message={cards.error.message} onRetry={() => cards.refetch()} />}
        {cards.data?.map((c) => <StoreCard key={c.storeId} card={c} />)}
        {cards.data?.length === 0 && (
          <div className="rounded-card bg-surface p-6 text-center text-sm text-ink-2">
            Әзірге карта жоқ. Дүкенде сатып алғанда телефон нөміріңізді айтыңыз, карта автоматты қосылады.
          </div>
        )}

        {promoOpen && (
          <div className="flex items-center gap-3 rounded-card bg-violet-soft px-4 py-3.5">
            <div className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-surface text-violet">
              <Wallet size={24} />
            </div>
            <div className="min-w-0 flex-1">
              <div className="text-sm font-bold">Бір аккаунт — көп дүкен</div>
              <div className="mt-0.5 text-xs text-ink-2">Жаңа дүкендерді қосып, сүйікті жерлеріңізде бонус жинаңыз</div>
            </div>
            <button type="button" aria-label="Жабу" onClick={() => setPromoOpen(false)} className="text-ink-3">
              <X size={18} />
            </button>
          </div>
        )}
      </div>
    </>
  )
}
