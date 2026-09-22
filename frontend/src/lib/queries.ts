import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { cardsApi, meApi, notificationsApi, qrApi, transactionsApi, transactionsPageApi, type NotificationCategory } from './api'

export const useMe = () => useQuery({ queryKey: ['me'], queryFn: meApi.get })

const PAGE = 10
const LIVE = { refetchInterval: 15_000, refetchOnWindowFocus: true }

export const useCards = () => useQuery({ queryKey: ['cards'], queryFn: cardsApi.list, ...LIVE })

export const useCard = (storeId: string) =>
  useQuery({ queryKey: ['cards', storeId], queryFn: () => cardsApi.get(storeId), ...LIVE })

export const useQr = () => useQuery({ queryKey: ['me', 'qr'], queryFn: qrApi.get, staleTime: Infinity })

export const useTransactions = (storeId: string | null) =>
  useInfiniteQuery({
    queryKey: ['transactions', 'page', storeId],
    queryFn: ({ pageParam }) => transactionsPageApi.list(storeId, pageParam, PAGE),
    initialPageParam: 0,
    getNextPageParam: (last, pages) => (last.hasMore ? pages.length * PAGE : undefined),
  })

export const useRecentTransactions = (take = 20) =>
  useQuery({ queryKey: ['transactions', 'recent', take], queryFn: () => transactionsApi.recent(take), ...LIVE })

export const useNotifications = (category: NotificationCategory | null) =>
  useInfiniteQuery({
    queryKey: ['notifications', category],
    queryFn: ({ pageParam }) => notificationsApi.list(category, pageParam, PAGE),
    initialPageParam: 0,
    getNextPageParam: (last, pages) => (last.hasMore ? pages.length * PAGE : undefined),
  })

export const useUnreadCount = () =>
  useQuery({ queryKey: ['notifications', 'unread-count'], queryFn: notificationsApi.unreadCount, ...LIVE })

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
