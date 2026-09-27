import type { LucideIcon } from 'lucide-react'
import {
  Apple, Baby, Bath, Battery, BookOpen, Cake, Car, CarFront, CircleDot, Coffee, Cog, CupSoda, Disc, Dog, Droplet,
  Dumbbell, Filter, Flower2, Fuel, Gamepad2, Gem, Gift, Hammer, Headphones, Home, Lamp, Link, Milk, Monitor,
  MoveVertical, Package, PenTool, Pill, Pizza, Settings2, Shirt, ShoppingBasket, Smartphone, Snowflake, Sparkles,
  SprayCan, Store, Watch, Wrench, Zap,
} from 'lucide-react'

/**
 * Санат белгішелері (ТЗ «Товар» §7.1). ТЗ «Авторизация» §2.9 бірыңғай сызықтық белгішелерді
 * талап етеді, сондықтан макеттегі эмодзи-суреттердің орнына lucide жиыны. Кілт базада сақталады.
 */
export const CATALOG_ICONS: Record<string, LucideIcon> = {
  car: Car, 'car-front': CarFront, disc: Disc, cog: Cog, filter: Filter, zap: Zap, snowflake: Snowflake,
  'settings-2': Settings2, fuel: Fuel, 'circle-dot': CircleDot, link: Link, 'move-vertical': MoveVertical,
  droplet: Droplet, battery: Battery, wrench: Wrench, hammer: Hammer, sparkles: Sparkles, package: Package,
  'shopping-basket': ShoppingBasket, 'cup-soda': CupSoda, coffee: Coffee, milk: Milk, apple: Apple, pizza: Pizza,
  cake: Cake, shirt: Shirt, watch: Watch, gem: Gem, smartphone: Smartphone, monitor: Monitor, headphones: Headphones,
  'gamepad-2': Gamepad2, 'spray-can': SprayCan, bath: Bath, pill: Pill, baby: Baby, dog: Dog, 'flower-2': Flower2,
  home: Home, lamp: Lamp, 'book-open': BookOpen, 'pen-tool': PenTool, dumbbell: Dumbbell, gift: Gift, store: Store,
}

export function CatalogIcon({ name, size = 20, className }: { name: string | null | undefined; size?: number; className?: string }) {
  const Icon = (name && CATALOG_ICONS[name]) || Package
  return <Icon size={size} className={className} />
}
