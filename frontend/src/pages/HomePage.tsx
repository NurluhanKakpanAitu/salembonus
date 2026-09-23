import { useState } from 'react'
import { Avatar, PageHeader } from '../components/PageHeader'
import { QrSheet } from '../components/QrSheet'
import { BalanceCard } from '../components/home/BalanceCard'
import { StoreStrip } from '../components/home/StoreStrip'
import { TransactionList } from '../components/home/TransactionList'
import { BirthdayBanner } from '../components/home/BirthdayBanner'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { useCards, useMe, useRecentTransactions } from '../lib/queries'

export function HomePage() {
  const me = useMe()
  const cards = useCards()
  const transactions = useRecentTransactions(4)
  const [qrOpen, setQrOpen] = useState(false)

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
        {me.isPending && <Skeleton className="h-40 rounded-[24px]" />}
        {me.isError && <ErrorBox message={me.error.message} onRetry={() => me.refetch()} />}
        {me.data && <BalanceCard me={me.data} onShowQr={() => setQrOpen(true)} />}

        {me.data?.isBirthdayToday && <BirthdayBanner />}

        {cards.isPending && <Skeleton className="h-40" />}
        {cards.isError && <ErrorBox message={cards.error.message} onRetry={() => cards.refetch()} />}
        {cards.data && cards.data.length > 0 && <StoreStrip cards={cards.data} />}
        {cards.data && cards.data.length === 0 && (
          <div className="rounded-[20px] bg-surface p-6 text-center text-sm text-ink-2">
            Әзірге бонус картаңыз жоқ. Дүкенде QR кодыңызды көрсетіңіз немесе телефон нөміріңізді айтыңыз.
          </div>
        )}

        {transactions.isPending && <Skeleton className="h-64" />}
        {transactions.data && <TransactionList items={transactions.data} allHref="/transactions" />}
      </div>
      <QrSheet open={qrOpen} onClose={() => setQrOpen(false)} />
    </>
  )
}
