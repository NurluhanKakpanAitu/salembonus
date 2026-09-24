import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { BackHeader } from '../components/BackHeader'
import { BonusCardTile } from '../components/cards/BonusCardTile'
import { StoreStatusCard } from '../components/cards/StoreStatusCard'
import { TransactionList } from '../components/home/TransactionList'
import { QrSheet } from '../components/QrSheet'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { useCard, useStore, useTransactions } from '../lib/queries'

export function CardDetailPage() {
  const { storeId = '' } = useParams()
  const card = useCard(storeId)
  const store = useStore(storeId)
  const tx = useTransactions(storeId)
  const [qrOpen, setQrOpen] = useState(false)
  const items = tx.data?.pages.flatMap((p) => p.items) ?? []

  return (
    <>
      <BackHeader title={card.data?.storeName ?? 'Карта'} subtitle={card.data?.category} fallback="/cards" />
      <div className="mt-3 flex flex-col gap-5">
        {card.isPending && <Skeleton className="h-[210px] rounded-[18px]" />}
        {card.isError && <ErrorBox message={card.error.message} onRetry={() => card.refetch()} />}
        {card.data && <BonusCardTile card={card.data} onAction={() => setQrOpen(true)} />}

        {store.isPending && <Skeleton className="h-64" />}
        {store.data && <StoreStatusCard store={store.data} />}

        {tx.data && (
          <TransactionList
            items={items}
            title="Соңғы операциялар"
            allHref={tx.hasNextPage ? `/transactions?storeId=${storeId}` : undefined}
          />
        )}
      </div>
      <QrSheet open={qrOpen} onClose={() => setQrOpen(false)} storeName={card.data?.storeName} />
    </>
  )
}
