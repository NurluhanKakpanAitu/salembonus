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
  /** PIN қойылған ба — кассада PIN-мен кіру мен бұғатты ашу үшін. */
  hasPin: boolean
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

export interface PasswordPolicy {
  minLength: number
  requireLetterAndDigit: boolean
}

export interface ResetRequested {
  codeLength: number
  expiresInSeconds: number
  retryAfterSeconds: number
  /** Тек разработкада: WhatsApp шаблоны бекітілгенше код жауапта келеді. */
  devCode: string | null
}

export interface ResetVerified {
  resetToken: string
  expiresInSeconds: number
}

/** Касса (құрылғы). */
export interface Register {
  id: string
  name: string
  storeId: string
  storeName: string
  storeAddress: string | null
  /** Әрекетсіз неше минуттан кейін бұғатталады; null — автоблок өшірулі. */
  autoLockMinutes: number | null
  isBound: boolean
  activatedAt: string | null
}

export interface RegisterCashier {
  id: string
  firstName: string
  lastName: string
  role: StaffRole
  hasPin: boolean
}
