import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ChevronRight } from 'lucide-react'
import { Avatar, PageHeader } from '../components/PageHeader'
import { QrSheet } from '../components/QrSheet'
import { CardCarousel } from '../components/cards/CardCarousel'
import { TransactionList } from '../components/home/TransactionList'
import { BirthdayBanner } from '../components/home/BirthdayBanner'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { useCards, useMe, useRecentTransactions } from '../lib/queries'

export function HomePage() {
  const me = useMe()
  const cards = useCards()
  const transactions = useRecentTransactions(4)
  const [qrStore, setQrStore] = useState<string | null>(null)

  return (
    <>
      <PageHeader
        right={
          <div className="flex items-center gap-2.5">
            <div className="text-right">
              <div className="text-xs text-ink-2">Сәлем,</div>
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
            <h2 className="text-[17px] font-bold">Менің карталарым</h2>
            {cards.data && cards.data.length > 1 && (
              <Link to="/cards" className="flex items-center text-[13px] font-semibold text-brand">
                Барлығы <ChevronRight size={16} />
              </Link>
            )}
          </div>
          {cards.isPending && <Skeleton className="h-[178px] rounded-[18px]" />}
          {cards.isError && <ErrorBox message={cards.error.message} onRetry={() => cards.refetch()} />}
          {cards.data && cards.data.length === 0 && (
            <div className="rounded-[18px] bg-surface p-6 text-center text-sm text-ink-2">
              Әзірге бонус картаңыз жоқ. Дүкенде QR кодыңызды көрсетіңіз немесе телефон нөміріңізді айтыңыз.
            </div>
          )}
          {cards.data && cards.data.length > 0 && (
            <CardCarousel cards={cards.data} onShowQr={(c) => setQrStore(c.storeName)} />
          )}
        </section>

        {transactions.isPending && <Skeleton className="h-64" />}
        {transactions.data && <TransactionList items={transactions.data} allHref="/transactions" />}
      </div>
      <QrSheet open={qrStore !== null} onClose={() => setQrStore(null)} storeName={qrStore ?? undefined} />
    </>
  )
}
