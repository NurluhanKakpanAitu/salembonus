import type { LucideIcon } from 'lucide-react'
import {
  ArrowLeftRight, Banknote, Cake, Calculator, ChartColumn, Crown, FileText, Gift, HandCoins, History, Landmark, LayoutDashboard, ListOrdered, Megaphone, Package,
  PieChart, ScrollText, Settings, ShoppingCart, Timer, TrendingUp, Truck, UserCog, Users, Wallet, Warehouse, Activity, FileUp, Layers, SlidersHorizontal,
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

export interface SubNavItem { path: string; label: TranslationKey; icon: LucideIcon }

/** «Финансы» подразделдері (ТЗ «Финансы» §1, §2): сол жақ мәзірде ашылады, белсендісі бөлектеледі. */
export const FINANCE_SECTIONS: SubNavItem[] = [
  { path: '', label: 'fin.section.overview', icon: LayoutDashboard },
  { path: 'pnl', label: 'fin.section.pnl', icon: PieChart },
  { path: 'cash-flow', label: 'fin.section.cash-flow', icon: TrendingUp },
  { path: 'income-expense', label: 'fin.section.income-expense', icon: ArrowLeftRight },
  { path: 'transactions', label: 'fin.section.transactions', icon: ListOrdered },
  { path: 'accounts', label: 'fin.section.accounts', icon: Landmark },
  { path: 'product-profit', label: 'fin.section.product-profit', icon: Banknote },
  { path: 'customer-debts', label: 'fin.section.customer-debts', icon: HandCoins },
  { path: 'supplier-debts', label: 'fin.section.supplier-debts', icon: Truck },
  { path: 'documents', label: 'fin.section.documents', icon: FileText },
]

/** «Бонусы» бөлімдері (ТЗ «Бонусы» §1): бірінші — «Обзор», белсендісі сол жақ мәзірде бөлектеледі. */
export const BONUS_SECTIONS: SubNavItem[] = [
  { path: '', label: 'bon.section.overview', icon: LayoutDashboard },
  { path: 'statuses', label: 'bon.section.statuses', icon: Crown },
  { path: 'expiry', label: 'bon.section.expiry', icon: Timer },
  { path: 'birthday', label: 'bon.section.birthday', icon: Cake },
  { path: 'campaigns', label: 'bon.section.campaigns', icon: Megaphone },
  { path: 'statistics', label: 'bon.section.statistics', icon: ChartColumn },
  { path: 'history', label: 'bon.section.history', icon: History },
  { path: 'rules', label: 'bon.section.rules', icon: ScrollText },
]

/** «Клиенты» бөлімдері (ТЗ «Клиенты» §1): «Все клиенты» — негізгі жұмыс экраны. */
export const CLIENT_SECTIONS: SubNavItem[] = [
  { path: '', label: 'cli.section.list', icon: Users },
  { path: 'overview', label: 'cli.section.overview', icon: LayoutDashboard },
  { path: 'segments', label: 'cli.section.segments', icon: Layers },
  { path: 'debts', label: 'cli.section.debts', icon: HandCoins },
  { path: 'activity', label: 'cli.section.activity', icon: Activity },
  { path: 'birthdays', label: 'cli.section.birthdays', icon: Cake },
  { path: 'exchange', label: 'cli.section.exchange', icon: FileUp },
  { path: 'settings', label: 'cli.section.settings', icon: SlidersHorizontal },
]

/** Мәзірде ішкі бөлімдері ашылатын бөлімдер. */
export const SUB_SECTIONS: Record<string, SubNavItem[]> = { '/finance': FINANCE_SECTIONS, '/bonus': BONUS_SECTIONS, '/clients': CLIENT_SECTIONS }

/** Мәзір реті макет бойынша (SALEMPOS.md, бөлімдер тізімі). */
export const NAV: NavItem[] = [
  { path: '/cashier', label: 'nav.cashier', icon: Calculator, permission: Permission.SalesCreate },
  { path: '/products', label: 'nav.products', icon: Package, permission: Permission.ProductsView },
  { path: '/warehouse', label: 'nav.warehouse', icon: Warehouse, permission: Permission.InventoryView },
  { path: '/sales', label: 'nav.sales', icon: ShoppingCart, permission: Permission.SalesView },
  { path: '/statistics', label: 'nav.statistics', icon: ChartColumn, permission: Permission.StatisticsView },
  { path: '/finance', label: 'nav.finance', icon: Wallet, permission: Permission.FinanceView },
  { path: '/bonus', label: 'nav.bonus', icon: Gift, permission: Permission.BonusView },
  { path: '/clients', label: 'nav.clients', icon: Users, permission: Permission.ClientsView },
  { path: '/staff', label: 'nav.staff', icon: UserCog, permission: Permission.StaffManage },
  { path: '/settings', label: 'nav.settings', icon: Settings, permission: Permission.SettingsManage },
]

export function canSee(item: NavItem, store: StaffStore | null): boolean {
  if (!store) return false
  if (store.role === 'Owner') return true
  if (item.permission) return store.permissions.includes(item.permission)
  return item.roles?.includes(store.role) ?? false
}
