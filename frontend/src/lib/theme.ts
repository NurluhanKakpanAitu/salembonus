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

/** Hex-ті HSL-ге және кері аударады — түсті ашықтау/күңгірттеу үшін. */
function toHsl(hex: string): [number, number, number] | null {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim())
  if (!m) return null
  const n = parseInt(m[1], 16)
  const r = ((n >> 16) & 255) / 255
  const g = ((n >> 8) & 255) / 255
  const b = (n & 255) / 255
  const max = Math.max(r, g, b)
  const min = Math.min(r, g, b)
  const l = (max + min) / 2
  const d = max - min
  if (d === 0) return [0, 0, l]
  const sat = l > 0.5 ? d / (2 - max - min) : d / (max + min)
  const h =
    max === r ? ((g - b) / d + (g < b ? 6 : 0)) : max === g ? (b - r) / d + 2 : (r - g) / d + 4
  return [h * 60, sat, l]
}

/** Бір түстің ашықтығын жылжытады: жарық көзі мен көлеңкені жасау үшін. */
function shade(hex: string, deltaL: number, deltaH = 0): string {
  const hsl = toHsl(hex)
  if (!hsl) return hex
  const [h, s, l] = hsl
  const light = Math.min(1, Math.max(0, l + deltaL))
  return `hsl(${(h + deltaH + 360) % 360} ${Math.round(s * 100)}% ${Math.round(light * 100)}%)`
}

/**
 * Карта фоны: дүкеннің өз түсінен жасалған жұмсақ градиент.
 * Күңгірт түстер аздап ашығырақ, ашық түстер аздап күңгірттеу басталады.
 */
export function storeGradient(hex: string): string {
  const hsl = toHsl(hex)
  const l = hsl?.[2] ?? 0.2
  const lift = l < 0.35 ? 0.13 : 0.09
  return `linear-gradient(145deg, ${shade(hex, lift, -6)} 0%, ${hex} 52%, ${shade(hex, -0.07, 6)} 100%)`
}

export interface StoreTheme {
  /** Үлкен беттер үшін градиент. */
  bg: string
  /** Кіші иконкалар мен мәтін түсі үшін таза түс. */
  solid: string
  text: string
  muted: string
  box: string
  iconColor: string
}

export function storeTheme(hex: string): StoreTheme {
  const light = isLightColor(hex)
  return {
    bg: storeGradient(hex),
    solid: hex,
    text: light ? '#151821' : '#FFFFFF',
    muted: light ? 'rgba(21,24,33,0.6)' : 'rgba(255,255,255,0.7)',
    box: light ? 'rgba(255,255,255,0.75)' : 'rgba(255,255,255,0.12)',
    iconColor: light ? '#151821' : hex,
  }
}

/** Деңгей кілті бойынша белгінің түсі (Vip, Regular, Favorite, New). */
export const levelBadge = (level: string): { bg: string; text: string; border?: string } => {
  switch (level) {
    case 'Vip':
      return { bg: 'transparent', text: '#F5B301', border: '#F5B301' }
    case 'Regular':
      return { bg: '#1F7A4D', text: '#FFFFFF' }
    case 'Favorite':
      return { bg: '#DDEEFF', text: '#1D6FB8' }
    default:
      return { bg: '#6B7280', text: '#FFFFFF' }
  }
}
