import { useSyncExternalStore } from 'react'

/** CSS медиа-сұрауына жазылу: экран ені өзгергенде компонент қайта салынады (касса: ноутбук / телефон орналасуы). */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    (notify) => {
      const mql = window.matchMedia(query)
      mql.addEventListener('change', notify)
      return () => mql.removeEventListener('change', notify)
    },
    () => window.matchMedia(query).matches,
    () => false,
  )
}

/** Tailwind `lg` шегі — кабинеттің бүйір мәзірі мен кассаның екі бағанды орналасуы осыдан басталады. */
export const useIsDesktop = () => useMediaQuery('(min-width: 1024px)')
