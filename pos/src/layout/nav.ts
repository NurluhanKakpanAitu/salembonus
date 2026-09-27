import type { LucideIcon } from 'lucide-react'
import {
  Calculator, ChartColumn, Gift, Package, Settings, ShoppingCart, UserCog, Users, Wallet, Warehouse,
} from 'lucide-react'
import type { TranslationKey } from '../lib/i18n'
import { Permission, type StaffRole, type StaffStore } from '../lib/types'

export interface NavItem {
  path: string
  label: TranslationKey
  icon: LucideIcon
  /** ТЗ-сы бар бөлімдер нақты рұқсатпен ашылады. */
  permission?: string
  /** ТЗ-сы әлі жоқ бөлімдер — әзірге иесі мен әкімшіге ғана көрінеді. */
  roles?: StaffRole[]
}

const managers: StaffRole[] = ['Owner', 'Admin']

/** Мәзір реті макет бойынша (SALEMPOS.md, бөлімдер тізімі). */
export const NAV: NavItem[] = [
  { path: '/cashier', label: 'nav.cashier', icon: Calculator, permission: Permission.SalesCreate },
  { path: '/products', label: 'nav.products', icon: Package, permission: Permission.ProductsView },
  { path: '/warehouse', label: 'nav.warehouse', icon: Warehouse, roles: managers },
  { path: '/sales', label: 'nav.sales', icon: ShoppingCart, roles: managers },
  { path: '/statistics', label: 'nav.statistics', icon: ChartColumn, permission: Permission.StatisticsView },
  { path: '/finance', label: 'nav.finance', icon: Wallet, permission: Permission.FinanceView },
  { path: '/bonus', label: 'nav.bonus', icon: Gift, roles: managers },
  { path: '/clients', label: 'nav.clients', icon: Users, roles: managers },
  { path: '/staff', label: 'nav.staff', icon: UserCog, permission: Permission.StaffManage },
  { path: '/settings', label: 'nav.settings', icon: Settings, permission: Permission.SettingsManage },
]

export function canSee(item: NavItem, store: StaffStore | null): boolean {
  if (!store) return false
  if (store.role === 'Owner') return true
  if (item.permission) return store.permissions.includes(item.permission)
  return item.roles?.includes(store.role) ?? false
}
