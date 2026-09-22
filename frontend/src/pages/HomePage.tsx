import { Avatar, PageHeader } from '../components/PageHeader'
import { CardCarousel } from '../components/home/CardCarousel'
import { LevelCard } from '../components/home/LevelCard'
import { TransactionList } from '../components/home/TransactionList'
import { BirthdayBanner } from '../components/home/BirthdayBanner'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { useCards, useMe, useRecentTransactions } from '../lib/queries'

export function HomePage() {
  const me = useMe()
  const cards = useCards()
  const transactions = useRecentTransactions(4)

  const primaryCard = cards.data?.[0]

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

      <div className="mt-3 flex flex-col gap-5">
        {cards.isPending && <Skeleton className="h-[290px] rounded-[22px]" />}
        {cards.isError && <ErrorBox message={cards.error.message} onRetry={() => cards.refetch()} />}
        {cards.data && cards.data.length > 0 && <CardCarousel cards={cards.data} />}
        {cards.data && cards.data.length === 0 && (
          <div className="rounded-card bg-surface p-6 text-center text-sm text-ink-2">
            Әзірге бонус картаңыз жоқ. Дүкенде телефон нөміріңізді айтыңыз.
          </div>
        )}

        {primaryCard && <LevelCard card={primaryCard} />}

        {transactions.isPending && <Skeleton className="h-64" />}
        {transactions.data && <TransactionList items={transactions.data} allHref="/transactions" />}

        {me.data?.isBirthdayToday && <BirthdayBanner />}
      </div>
    </>
  )
}
