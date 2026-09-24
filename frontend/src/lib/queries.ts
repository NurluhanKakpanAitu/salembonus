import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { cardsApi, meApi, notificationsApi, storesApi, transactionsApi, type NotificationCategory, type UpdateProfile } from './api'
import { useAuth } from './auth'

const PAGE = 10
const LIVE = { refetchInterval: 15_000, refetchOnWindowFocus: true }

const useLoggedIn = () => useAuth((s) => !!s.accessToken)

export const useMe = () => {
  const enabled = useLoggedIn()
  return useQuery({ queryKey: ['me'], queryFn: meApi.get, enabled })
}

export const useUpdateMe = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateProfile) => meApi.update(body),
    onSuccess: (data) => {
      qc.setQueryData(['me'], data)
      useAuth.getState().setProfileCompleted(true)
    },
  })
}

export const useCards = () => {
  const enabled = useLoggedIn()
  return useQuery({ queryKey: ['cards'], queryFn: cardsApi.list, enabled, ...LIVE })
}

export const useCard = (storeId: string) =>
  useQuery({ queryKey: ['cards', storeId], queryFn: () => cardsApi.get(storeId), ...LIVE })

export const useStores = (search = '') =>
  useQuery({ queryKey: ['stores', search], queryFn: () => storesApi.list(search || undefined) })

export const useStore = (id: string) =>
  useQuery({ queryKey: ['stores', 'detail', id], queryFn: () => storesApi.get(id), ...LIVE })

export const useJoinStore = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (code: string) => storesApi.join(code),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['stores'] })
      qc.invalidateQueries({ queryKey: ['cards'] })
      qc.invalidateQueries({ queryKey: ['me'] })
      qc.invalidateQueries({ queryKey: ['notifications'] })
    },
  })
}

export const useQr = () => useQuery({ queryKey: ['me', 'qr'], queryFn: meApi.qr, staleTime: Infinity })

export const useRecentTransactions = (take = 20) => {
  const enabled = useLoggedIn()
  return useQuery({ queryKey: ['transactions', 'recent', take], queryFn: () => transactionsApi.recent(take), enabled, ...LIVE })
}

export const useTransactions = (storeId: string | null) =>
  useInfiniteQuery({
    queryKey: ['transactions', 'page', storeId],
    queryFn: ({ pageParam }) => transactionsApi.list(storeId, pageParam, PAGE),
    initialPageParam: 0,
    getNextPageParam: (last, pages) => (last.hasMore ? pages.length * PAGE : undefined),
  })

export const useNotifications = (category: NotificationCategory | null) =>
  useInfiniteQuery({
    queryKey: ['notifications', category],
    queryFn: ({ pageParam }) => notificationsApi.list(category, pageParam, PAGE),
    initialPageParam: 0,
    getNextPageParam: (last, pages) => (last.hasMore ? pages.length * PAGE : undefined),
  })

export const useUnreadCount = () => {
  const enabled = useLoggedIn()
  return useQuery({ queryKey: ['notifications', 'unread-count'], queryFn: notificationsApi.unreadCount, enabled, ...LIVE })
}

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
