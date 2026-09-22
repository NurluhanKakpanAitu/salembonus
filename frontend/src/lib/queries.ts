import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { cardsApi, meApi, notificationsApi, transactionsApi, type NotificationCategory } from './api'

export const useMe = () => useQuery({ queryKey: ['me'], queryFn: meApi.get })

export const useCards = () => useQuery({ queryKey: ['cards'], queryFn: cardsApi.list })

export const useRecentTransactions = (take = 20) =>
  useQuery({ queryKey: ['transactions', 'recent', take], queryFn: () => transactionsApi.recent(take) })

const PAGE = 10

export const useNotifications = (category: NotificationCategory | null) =>
  useInfiniteQuery({
    queryKey: ['notifications', category],
    queryFn: ({ pageParam }) => notificationsApi.list(category, pageParam, PAGE),
    initialPageParam: 0,
    getNextPageParam: (last, pages) => (last.hasMore ? pages.length * PAGE : undefined),
  })

export const useUnreadCount = () =>
  useQuery({ queryKey: ['notifications', 'unread-count'], queryFn: notificationsApi.unreadCount, refetchInterval: 60_000 })

export const useMarkRead = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => notificationsApi.markRead(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }),
  })
}

export const useMarkAllRead = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: notificationsApi.markAllRead,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }),
  })
}
