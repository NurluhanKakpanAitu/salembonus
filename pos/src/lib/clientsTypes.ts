// «Клиенты» (ТЗ «Клиенты» v1.0) — серверлік DTO-лардың айнасы.

export type ClientGender = 'Unknown' | 'Male' | 'Female'
export type ClientSource = 'Manual' | 'Cashier' | 'Import' | 'App' | 'Integration'
export type ClientActivity = 'active' | 'cooling' | 'passive' | 'noPurchases'
export type QuickFilter = 'active' | 'cooling' | 'passive' | 'new' | 'repeat' | 'debt' | 'overdue' | 'bonus' | 'birthday' | 'noPurchases' | 'archived'

export const QUICK_FILTERS: QuickFilter[] = ['active', 'new', 'repeat', 'passive', 'debt', 'overdue', 'bonus', 'birthday', 'noPurchases', 'archived']
export const SOURCES: ClientSource[] = ['Manual', 'Cashier', 'Import', 'App', 'Integration']
export const CHANNELS = ['app', 'sms', 'whatsapp', 'email'] as const
export type Channel = (typeof CHANNELS)[number]

export interface ClientFilter {
  quick?: QuickFilter | null
  search?: string | null
  statusIds?: string[]
  registeredFrom?: string | null
  registeredTo?: string | null
  lastPurchaseFrom?: string | null
  lastPurchaseTo?: string | null
  minPurchases?: number | null
  maxPurchases?: number | null
  minAmount?: number | null
  maxAmount?: number | null
  minAverage?: number | null
  maxAverage?: number | null
  minBonus?: number | null
  maxBonus?: number | null
  hasDebt?: boolean | null
  overdue?: boolean | null
  kato?: string | null
  gender?: ClientGender | null
  birthMonth?: number | null
  source?: ClientSource | null
  consent?: Channel | null
}

export interface CustomFieldDefinition { key: string; label: string; type: 'text' | 'number' | 'date' | 'select'; options: string[] }

export interface ClientSettings {
  requireLastName: boolean; requireBirthDate: boolean; activeDays: number; passiveDays: number; newDays: number; dueSoonDays: number
  defaultColumns: string[]; customFields: CustomFieldDefinition[]; updatedAt: string | null
}

export interface ClientPermissions {
  edit: boolean; archive: boolean; export: boolean; merge: boolean; debts: boolean; contacts: boolean; settings: boolean
  bonusView: boolean; bonusAccrue: boolean; bonusManage: boolean; bonusCampaigns: boolean; salesView: boolean; salesCreate: boolean; financeOperate: boolean
}

export interface ClientMeta { can: ClientPermissions; settings: ClientSettings; statuses: { id: string; name: string; color: string }[]; currentStoreId: string }

export interface ClientRow {
  id: string; customerId: string; number: number; name: string; firstName: string; lastName: string; middleName: string | null; phone: string | null
  email: string | null; birthDate: string | null; gender: ClientGender; source: ClientSource; statusId: string | null; statusName: string; statusColor: string
  purchases: number; amount: number; average: number; firstPurchaseAt: string | null; lastPurchaseAt: string | null; daysSinceLastPurchase: number | null
  bonus: number; debt: number; overdue: number; registeredAt: string; hasApp: boolean; hasCard: boolean; isArchived: boolean; isNew: boolean
  activity: ClientActivity; consentApp: boolean; consentSms: boolean; consentWhatsApp: boolean; consentEmail: boolean; katoCode: string | null
  customFields: Record<string, string>
}

export interface ClientPage { rows: ClientRow[]; total: number; page: number; pageSize: number }
export interface ClientKpis { total: number; active: number; passive: number; new: number; repeat: number; withDebt: number; overdue: number; withBonus: number; generatedAt: string }
export interface ClientMonth { month: string; count: number; amount: number }
export interface ClientBirthday { client: ClientRow; nextBirthday: string; daysLeft: number; age: number | null }

export interface ClientOverview {
  kpis: ClientKpis; activity: { key: ClientActivity; count: number }[]; newByMonth: ClientMonth[]; top: ClientRow[]; atRisk: ClientRow[]
  birthdays: ClientBirthday[]; debtTotal: number; debtOverdue: number
}

export interface ClientLookup { exists: boolean; global: boolean; id: string | null; number: number | null; name: string | null; archived: boolean }

export interface ClientSale {
  id: string; number: number; createdAt: string; storeId: string; storeName: string; total: number; returned: number; status: 'Completed' | 'PartiallyReturned' | 'Returned'
  items: number; bonusAccrued: number; bonusRedeemed: number; discount: number; cashier: string; methods: string[]
}

export interface ClientActivitySummary { segment: ClientActivity; daysSinceLastPurchase: number | null; averageInterval: number | null; churnRisk: 'none' | 'low' | 'medium' | 'high' }

export interface ClientCard {
  client: ClientRow; address: string | null; kato: { code: string; name: string }[]; comment: string | null; consentUpdatedAt: string | null
  createdBy: string | null; updatedAt: string; archivedAt: string | null; archivedBy: string | null; archiveReason: string | null; mergedIntoId: string | null
  mergedIntoNumber: number | null; canEditPhone: boolean
  bonus: { hasCard: boolean; balance: number; statusId: string | null; statusName: string; statusColor: string; optOut: boolean }
  activitySummary: ClientActivitySummary; topProducts: { productId: string; name: string; quantity: number; amount: number; purchases: number }[]
  recentSales: ClientSale[]; notes: number
}

export interface ClientReceipt { kind: 'sale' | 'return'; id: string; saleId: string; number: number; createdAt: string; storeName: string; amount: number; status: string; methods: string[]; staff: string; comment: string | null }
export interface ClientDocument { kind: 'sale' | 'return' | 'finance'; id: string; number: string; date: string; amount: number; status: string; storeName: string; saleId: string | null; operationId: string | null; description: string | null }
export interface ClientCommunication { id: string; createdAt: string; channel: string; text: string; status: string; staff: string; storeName: string }

export interface ClientActivityDetail {
  summary: ClientActivitySummary; purchases30: number; purchases90: number; purchases365: number; amount30: number; amount90: number; amount365: number
  firstPurchaseAt: string | null; lastPurchaseAt: string | null; months: ClientMonth[]; communications: ClientCommunication[]
}

export type NoteCategory = 'General' | 'Preference' | 'Complaint' | 'Call' | 'Other'
export const NOTE_CATEGORIES: NoteCategory[] = ['General', 'Preference', 'Complaint', 'Call', 'Other']
export interface ClientNote { id: string; text: string; category: NoteCategory; authorId: string; author: string; createdAt: string; updatedAt: string | null; updatedBy: string | null; canEdit: boolean }
export interface ClientHistory { id: string; createdAt: string; staff: string; action: string; changes: { field: string; old: string | null; new: string | null }[] }
export interface ClientDuplicate { id: string; number: number; name: string; phone: string | null; birthDate: string | null; reason: 'name' | 'email' | 'birthDate' }

export interface ClientSegment {
  id: string; name: string; description: string | null; filter: ClientFilter; included: number; isShared: boolean; isMine: boolean; count: number
  createdAt: string; updatedAt: string; author: string
}

export interface ClientSave {
  phone?: string | null; firstName: string; lastName: string; middleName?: string | null; birthDate?: string | null; gender?: ClientGender | null; katoCode?: string | null
  address?: string | null; email?: string | null; comment?: string | null; consentApp?: boolean; consentSms?: boolean; consentWhatsApp?: boolean; consentEmail?: boolean
  customFields?: Record<string, string>
}

export interface ImportRow { row: number; status: 'new' | 'update' | 'skip' | 'error'; phone: string | null; name: string | null; message: string | null; profileId: string | null }
export interface ImportPreview { headers: string[]; mapping: Record<string, number>; rows: number; new: number; update: number; skip: number; errors: number; items: ImportRow[] }
export interface ImportResult { id: string; fileName: string; rows: number; created: number; updated: number; skipped: number; errors: number; items: ImportRow[]; createdAt: string }
export const IMPORT_FIELDS = ['phone', 'fullName', 'lastName', 'firstName', 'middleName', 'birthDate', 'gender', 'email', 'address', 'comment'] as const

export const EXPORT_FIELDS = [
  'number', 'lastName', 'firstName', 'middleName', 'name', 'phone', 'email', 'birthDate', 'gender', 'status', 'purchases', 'amount', 'average', 'lastPurchase',
  'bonus', 'debt', 'overdue', 'registered', 'source', 'activity', 'consentApp', 'consentSms', 'consentWhatsApp', 'consentEmail', 'katoCode', 'address', 'comment',
] as const
export const DEFAULT_EXPORT_FIELDS = ['number', 'name', 'phone', 'status', 'purchases', 'amount', 'average', 'lastPurchase', 'bonus', 'debt', 'registered']
