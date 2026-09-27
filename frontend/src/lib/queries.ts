import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { cardsApi, katoApi, meApi, notificationsApi, storesApi, transactionsApi, type NotificationCategory, type UpdateProfile } from './api'
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

export const useSetAvatar = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (avatarUrl: string | null) => meApi.setAvatar(avatarUrl),
    onSuccess: (data) => qc.setQueryData(['me'], data),
  })
}

/** Аккаунтты біржола өшіру. Сәтті болса, барлық кэш тазаланады. */
export const useDeleteMe = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => meApi.remove(),
    onSuccess: () => qc.clear(),
  })
}

/** КАТО тармақтары ұзақ өзгермейді, сондықтан ұзақ сақталады. */
export const useKatoChildren = (parent: string | null | undefined, enabled = true) =>
  useQuery({
    queryKey: ['kato', parent ?? 'root'],
    queryFn: () => katoApi.children(parent),
    enabled,
    staleTime: Infinity,
  })

export const useKatoSettlements = (parent: string | null | undefined) =>
  useQuery({
    queryKey: ['kato', 'settlements', parent],
    queryFn: () => katoApi.settlements(parent!),
    enabled: !!parent,
    staleTime: Infinity,
  })

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

/**
 * storeId берілсе тек сол дүкеннің операциялары, null болса — барлығы.
 * undefined болса сұраныс жіберілмейді: қай карта ашық екені әлі белгісіз деген сөз.
 */
export const useRecentTransactions = (take = 20, storeId?: string | null) => {
  const loggedIn = useLoggedIn()
  return useQuery({
    queryKey: ['transactions', 'recent', take, storeId ?? 'all'],
    queryFn: () => transactionsApi.recent(take, storeId),
    enabled: loggedIn && storeId !== undefined,
    ...LIVE,
  })
}

export const useTransactions = (storeId: string | null) =>
  useInfiniteQuery({
    queryKey: ['transactions', 'page', storeId],
    queryFn: ({ pageParam }) => transactionsApi.list(storeId, pageParam, PAGE),
    initialPageParam: 0,
    getNextPageParam: (last, pages) => (last.hasMore ? pages.length * PAGE : undefined),
  })

export const useReceipt = (receiptId: string | null) =>
  useQuery({
    queryKey: ['transactions', 'receipt', receiptId],
    queryFn: () => transactionsApi.receipt(receiptId!),
    enabled: receiptId !== null,
    staleTime: Infinity,
  })

/** source: дүкен идентификаторы, жүйелік хабарламалар үшін "system", барлығы үшін бос. */
export const useNotifications = (category: NotificationCategory | null, source?: string) =>
  useInfiniteQuery({
    queryKey: ['notifications', category, source ?? 'all'],
    queryFn: ({ pageParam }) => notificationsApi.list(category, pageParam, PAGE, source),
    initialPageParam: 0,
    getNextPageParam: (last, pages) => (last.hasMore ? pages.length * PAGE : undefined),
  })

export const useNotificationStores = () => {
  const enabled = useLoggedIn()
  return useQuery({ queryKey: ['notifications', 'stores'], queryFn: notificationsApi.stores, enabled, ...LIVE })
}

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
    mutationFn: (source?: string) => notificationsApi.markAllRead(source),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }),
  })
}
