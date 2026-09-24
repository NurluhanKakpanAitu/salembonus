import { useState } from 'react'
import { CirclePlus } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Avatar, PageHeader } from '../components/PageHeader'
import { PageTitle } from '../components/PageTitle'
import { BonusCardTile } from '../components/cards/BonusCardTile'
import { QrSheet } from '../components/QrSheet'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { useCards } from '../lib/queries'
import { useT } from '../lib/i18n'

export function CardsPage() {
  const t = useT()
  const cards = useCards()
  const [qrStore, setQrStore] = useState<string | null>(null)

  return (
    <>
      <PageHeader right={<Avatar />} />

      <div className="mt-3 flex flex-col gap-4">
        <PageTitle title={t('cards.title')} subtitle={t('cards.subtitle')} />

        <Link
          to="/stores"
          className="flex items-center justify-center gap-2 rounded-[14px] bg-violet-soft px-4 py-3 text-sm font-semibold text-violet"
        >
          <CirclePlus size={20} /> {t('cards.addStore')}
        </Link>

        {cards.isPending && (
          <>
            <Skeleton className="h-[210px] rounded-[18px]" />
            <Skeleton className="h-[210px] rounded-[18px]" />
          </>
        )}
        {cards.isError && <ErrorBox message={cards.error.message} onRetry={() => cards.refetch()} />}
        {cards.data?.map((c) => (
          <Link key={c.storeId} to={`/cards/${c.storeId}`} className="block active:scale-[0.99]">
            <BonusCardTile card={c} onAction={() => setQrStore(c.storeName)} />
          </Link>
        ))}
        {cards.data?.length === 0 && (
          <div className="rounded-card bg-surface p-6 text-center text-sm text-ink-2">
            {t('cards.empty')}
          </div>
        )}
      </div>
      <QrSheet open={qrStore !== null} onClose={() => setQrStore(null)} storeName={qrStore ?? undefined} />
    </>
  )
}
