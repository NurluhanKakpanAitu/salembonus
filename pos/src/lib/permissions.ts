import type { TranslationKey } from './i18n'
import type { StaffRole } from './types'

/**
 * Рұқсаттар каталогы (сервердегі StaffPermissions-пен бірдей кілттер), модуль бойынша топталған —
 * «Сотрудники» бөліміндегі рұқсат редакторы үшін. Жапсырмалар — `perm.<кілт>`, топтар — `permGroup.<модуль>`.
 */
export interface PermissionGroup { module: string; label: TranslationKey; permissions: string[] }

export const PERMISSION_GROUPS: PermissionGroup[] = [
  { module: 'cashier', label: 'permGroup.cashier', permissions: ['sales.create', 'sales.return', 'sales.debt', 'sales.negative_stock', 'discount.approve'] },
  { module: 'products', label: 'permGroup.products', permissions: ['products.view', 'products.create', 'products.edit', 'catalog.structure', 'catalog.lifecycle', 'catalog.dictionaries'] },
  { module: 'warehouse', label: 'permGroup.warehouse', permissions: ['inventory.view', 'inventory.receive', 'inventory.post', 'inventory.transfer', 'inventory.writeoff', 'inventory.warehouses', 'inventory.suppliers'] },
  { module: 'sales', label: 'permGroup.sales', permissions: ['sales.view', 'sales.export'] },
  { module: 'statistics', label: 'permGroup.statistics', permissions: ['statistics.view', 'statistics.export', 'customers.contacts'] },
  { module: 'finance', label: 'permGroup.finance', permissions: ['finance.view', 'finance.operations', 'finance.reverse', 'finance.accounts'] },
  { module: 'bonus', label: 'permGroup.bonus', permissions: ['bonus.view', 'bonus.manage', 'bonus.campaigns', 'bonus.accrue', 'bonus.writeoff', 'bonus.reverse', 'bonus.terms'] },
  { module: 'clients', label: 'permGroup.clients', permissions: ['clients.view', 'clients.edit', 'clients.archive', 'clients.export', 'clients.merge', 'clients.debts'] },
  { module: 'core', label: 'permGroup.core', permissions: ['staff.manage', 'settings.manage'] },
]

export const ALL_PERMISSIONS = PERMISSION_GROUPS.flatMap((g) => g.permissions)

/** Рөлдің әдепкі жиыны (сервердегі StaffPermissions.DefaultsFor-мен бірдей). */
export function defaultPermissions(role: StaffRole): string[] {
  if (role === 'Owner') return ALL_PERMISSIONS
  if (role === 'Admin') return ALL_PERMISSIONS.filter((p) => !['finance.view', 'sales.negative_stock', 'staff.manage', 'settings.manage'].includes(p))
  return ['sales.create', 'inventory.view']
}

/** Кассирге берілмейтін рұқсаттар — сервер оларды алып тастайды, редакторда көрсетпейміз. */
export const CASHIER_HIDDEN = new Set(['staff.manage', 'settings.manage'])

/** Рұқсат жапсырмасының кілті: `perm.sales.create` → i18n. */
export const permKey = (p: string) => `perm.${p}` as TranslationKey
