export type StaffRole = 'Owner' | 'Admin' | 'Cashier'

export interface StaffStore {
  id: string
  name: string
  address: string | null
  role: StaffRole
  permissions: string[]
}

export interface StaffMe {
  id: string
  phone: string
  firstName: string
  lastName: string
  language: 'ru' | 'kk'
  organizationId: string
  organizationName: string
  stores: StaffStore[]
  /** Кіргеннен кейінгі бет: иесі — статистика, кассир — касса. */
  startPage: 'statistics' | 'cashier'
}

export interface StaffSession {
  accessToken: string
  accessTokenExpiresAt: string
  me: StaffMe
}

/** Сервердің рұқсат кілттері (backend: StaffPermissions). */
export const Permission = {
  StatisticsView: 'statistics.view',
  FinanceView: 'finance.view',
  ProductsView: 'products.view',
  SalesCreate: 'sales.create',
  StaffManage: 'staff.manage',
  SettingsManage: 'settings.manage',
} as const
