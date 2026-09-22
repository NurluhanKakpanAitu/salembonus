import { ChevronDown } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'
import { BackHeader } from '../components/BackHeader'
import { TransactionList } from '../components/home/TransactionList'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { useTransactions } from '../lib/queries'

export function TransactionsPage() {
  const [params] = useSearchParams()
  const storeId = params.get('storeId')
  const q = useTransactions(storeId)
  const items = q.data?.pages.flatMap((p) => p.items) ?? []

  return (
    <>
      <BackHeader title="Барлық операциялар" subtitle={items[0]?.storeName && storeId ? items[0].storeName : undefined} />
      <div className="mt-3 flex flex-col gap-4">
        {q.isPending && <Skeleton className="h-72" />}
        {q.isError && <ErrorBox message={q.error.message} onRetry={() => q.refetch()} />}
        {q.data && <TransactionList items={items} title="Операциялар" />}
        {q.hasNextPage && (
          <div className="flex justify-center">
            <button
              type="button"
              disabled={q.isFetchingNextPage}
              onClick={() => q.fetchNextPage()}
              className="flex items-center gap-1.5 rounded-full bg-gray-200 px-4 py-2.5 text-[13px] font-medium text-ink-2 disabled:opacity-60"
            >
              {q.isFetchingNextPage ? 'Жүктелуде…' : 'Ескі операцияларды көрсету'} <ChevronDown size={16} />
            </button>
          </div>
        )}
      </div>
    </>
  )
}
