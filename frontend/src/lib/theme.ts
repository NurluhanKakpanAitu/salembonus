import { Car, Coffee, Dumbbell, Flower, ShoppingBag, Store, Utensils, type LucideIcon } from 'lucide-react'

const ICONS: Record<string, LucideIcon> = {
  car: Car,
  coffee: Coffee,
  flower: Flower,
  dumbbell: Dumbbell,
  'shopping-bag': ShoppingBag,
  utensils: Utensils,
  store: Store,
}

export const storeIcon = (key: string): LucideIcon => ICONS[key] ?? Store

/** Түс ашық па (мәтін қара болу керек пе). */
export function isLightColor(hex: string): boolean {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim())
  if (!m) return false
  const n = parseInt(m[1], 16)
  const r = (n >> 16) & 255
  const g = (n >> 8) & 255
  const b = n & 255
  return (0.299 * r + 0.587 * g + 0.114 * b) / 255 > 0.6
}

export interface StoreTheme {
  bg: string
  text: string
  muted: string
  box: string
  iconColor: string
}

export function storeTheme(hex: string): StoreTheme {
  const light = isLightColor(hex)
  return {
    bg: hex,
    text: light ? '#151821' : '#FFFFFF',
    muted: light ? 'rgba(21,24,33,0.6)' : 'rgba(255,255,255,0.7)',
    box: light ? 'rgba(255,255,255,0.75)' : 'rgba(255,255,255,0.12)',
    iconColor: light ? '#151821' : hex,
  }
}

export const levelBadge = (level: string): { bg: string; text: string; border?: string } => {
  switch (level) {
    case 'VIP клиент':
      return { bg: 'transparent', text: '#F5B301', border: '#F5B301' }
    case 'Тұрақты клиент':
      return { bg: '#1F7A4D', text: '#FFFFFF' }
    case 'Сүйікті клиент':
      return { bg: '#DDEEFF', text: '#1D6FB8' }
    default:
      return { bg: '#6B7280', text: '#FFFFFF' }
  }
}
