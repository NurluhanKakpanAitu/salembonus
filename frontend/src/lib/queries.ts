import { useQuery } from '@tanstack/react-query'
import { cardsApi, meApi, transactionsApi } from './api'

export const useMe = () => useQuery({ queryKey: ['me'], queryFn: meApi.get })

export const useCards = () => useQuery({ queryKey: ['cards'], queryFn: cardsApi.list })

export const useRecentTransactions = (take = 20) =>
  useQuery({ queryKey: ['transactions', 'recent', take], queryFn: () => transactionsApi.recent(take) })
