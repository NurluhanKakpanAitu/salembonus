import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ChevronRight } from 'lucide-react'
import { Avatar, PageHeader } from '../components/PageHeader'
import { QrSheet } from '../components/QrSheet'
import { CardCarousel } from '../components/cards/CardCarousel'
import { LevelProgress } from '../components/cards/LevelProgress'
import { TransactionList } from '../components/home/TransactionList'
import { BirthdayBanner } from '../components/home/BirthdayBanner'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { useCards, useMe, useRecentTransactions } from '../lib/queries'
import { useT } from '../lib/i18n'

export function HomePage() {
  const t = useT()
  const me = useMe()
  const cards = useCards()
  const transactions = useRecentTransactions(4)
  const [qrStore, setQrStore] = useState<string | null>(null)
  const [activeStoreId, setActiveStoreId] = useState<string | null>(null)
  // Карусельде көрініп тұрған карта; әлі сырғытылмаса — бірінші карта.
  const activeCard = cards.data?.find((c) => c.storeId === activeStoreId) ?? cards.data?.[0]

  return (
    <>
      <PageHeader
        right={
          <div className="flex items-center gap-2.5">
            <div className="text-right">
              <div className="text-xs text-ink-2">{t('home.greeting')}</div>
              <div className="text-[15px] font-bold">{me.data ? `${me.data.firstName}!` : '…'}</div>
            </div>
            <Avatar />
          </div>
        }
      />

      <div className="mt-3 flex flex-col gap-6">
        {me.data?.isBirthdayToday && <BirthdayBanner />}

        <section>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-[17px] font-bold">{t('home.myCards')}</h2>
            {cards.data && cards.data.length > 1 && (
              <Link to="/cards" className="flex items-center text-[13px] font-semibold text-brand">
                {t('common.all')} <ChevronRight size={16} />
              </Link>
            )}
          </div>
          {cards.isPending && <Skeleton className="h-[178px] rounded-[18px]" />}
          {cards.isError && <ErrorBox message={cards.error.message} onRetry={() => cards.refetch()} />}
          {cards.data && cards.data.length === 0 && (
            <div className="rounded-[18px] bg-surface p-6 text-center text-sm text-ink-2">
              {t('home.noCards')}
            </div>
          )}
          {cards.data && cards.data.length > 0 && (
            <CardCarousel
              cards={cards.data}
              onShowQr={(c) => setQrStore(c.storeName)}
              onActiveChange={(c) => setActiveStoreId(c.storeId)}
            />
          )}
          {activeCard && (
            <div className="mt-3">
              <LevelProgress card={activeCard} />
            </div>
          )}
        </section>

        {transactions.isPending && <Skeleton className="h-64" />}
        {transactions.data && <TransactionList items={transactions.data} allHref="/transactions" />}
      </div>
      <QrSheet open={qrStore !== null} onClose={() => setQrStore(null)} storeName={qrStore ?? undefined} />
    </>
  )
}
