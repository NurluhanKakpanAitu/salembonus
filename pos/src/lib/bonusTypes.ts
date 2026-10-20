/** «Бонусы» (ТЗ «Бонусы» v1.0): сервер DTO-ларының түрлері. Баланс тек операциялармен өзгереді. */

export type StatusRule = 'Registration' | 'PurchaseAmount' | 'PurchaseCount' | 'Manual'
export const STATUS_RULES: StatusRule[] = ['Registration', 'PurchaseAmount', 'PurchaseCount', 'Manual']
export type StatusPeriod = 'AllTime' | 'Last12Months' | 'CalendarYear'
export const STATUS_PERIODS: StatusPeriod[] = ['AllTime', 'Last12Months', 'CalendarYear']
export const STATUS_ICONS = ['sprout', 'star', 'crown', 'gem', 'heart', 'award', 'shield', 'handshake', 'flame', 'trophy'] as const
export const STATUS_COLORS = ['#16A34A', '#1769FF', '#7C3AED', '#DB2777', '#F59E0B', '#E11D48', '#0EA5E9', '#14B8A6', '#64748B', '#111827']
export type BonusOpType = 'Accrual' | 'Redemption' | 'Birthday' | 'Promo' | 'Expiration' | 'ReturnRestore' | 'ReturnReversal' | 'Adjustment' | 'Reversal'
export type CampaignType = 'Birthday' | 'Expiring' | 'Gift' | 'Promo' | 'Winback' | 'Custom'
export const CAMPAIGN_TYPES: CampaignType[] = ['Gift', 'Promo', 'Birthday', 'Expiring', 'Winback', 'Custom']
export type CampaignStatus = 'Draft' | 'Scheduled' | 'Queued' | 'Sending' | 'Completed' | 'CompletedWithErrors' | 'Cancelled'

export interface BonusPermissions { manage: boolean; campaigns: boolean; accrue: boolean; writeOff: boolean; reverse: boolean; terms: boolean; export: boolean; contacts: boolean }
export interface BonusChannel { key: string; available: boolean; note: string | null }

export interface BonusStatus {
  id: string; name: string; icon: string; color: string; rule: StatusRule; thresholdAmount: number | null; thresholdCount: number | null; period: StatusPeriod
  accrualPercent: number; countReturns: boolean; validityDays: number | null; autoDowngrade: boolean; description: string | null; isActive: boolean
  sortOrder: number; level: string; customers: number; createdAt: string; updatedAt: string
}
export interface SaveStatus {
  name: string; icon: string; color: string; rule: StatusRule; thresholdAmount: number | null; thresholdCount: number | null; period: StatusPeriod
  accrualPercent: number; countReturns: boolean; validityDays: number | null; autoDowngrade: boolean; description: string | null; isActive: boolean
}

export interface BonusMeta {
  storeId: string; storeName: string; timeZone: string; statuses: BonusStatus[]; can: BonusPermissions; channels: BonusChannel[]
  maxRedeemPercent: number; expiryEnabled: boolean; birthdayEnabled: boolean
}

export interface BonusCustomer {
  customerId: string; name: string; phone: string | null; cardCode: string | null; statusId: string | null; statusName: string; statusColor: string
  statusManual: boolean; balance: number; totalSpent: number; birthDate: string | null; registeredAt: string; lastOperationAt: string | null; optOut: boolean
}
export interface BonusCustomersPage { items: BonusCustomer[]; total: number; page: number; pageSize: number }
export interface BonusLot { id: string; type: BonusOpType; source: string | null; original: number; remaining: number; used: number; accruedAt: string; expiresAt: string | null; state: 'active' | 'spent' | 'expired' | 'reversed'; comment: string | null }
export interface BonusCustomerCard {
  customer: BonusCustomer; percent: number; nextExpiryAmount: number | null; nextExpiryAt: string | null; statusReason: string | null; statusAssignedAt: string | null
  statusExpiresAt: string | null; purchases: { amount: number; count: number; lastAt: string | null }; lots: BonusLot[]; operations: BonusOperation[]
}

export interface BonusOperation {
  id: string; createdAt: string; storeId: string; storeName: string; customerId: string; customerName: string; phone: string | null; type: BonusOpType
  amount: number; balanceBefore: number | null; balanceAfter: number | null; basis: string; basisLink: string | null; source: string; author: string
  status: 'posted' | 'reversed'; comment: string | null; percent: number | null; receiptId: string | null; receiptNumber: number | null
  sourceTransactionId: string | null; sourceId: string | null; expiresAt: string | null; remaining: number; canReverse: boolean; canReturn: boolean
}
export interface BonusOperationsPage { items: BonusOperation[]; total: number; page: number; pageSize: number }
export interface BonusOperationDetail { operation: BonusOperation; related: BonusOperation[]; returnableAmount: number }

export interface ExpirySettings {
  enabled: boolean; value: number; unit: 'Days' | 'Months'; statusIds: string[]; excludedTypes: string[]; warnEnabled: boolean; warnDays: number[]
  warnChannel: string; openLots: number; openLotsWithoutExpiry: number; updatedAt: string
}
export interface ExpiringLot {
  lotId: string; expiresAt: string; customerId: string; customerName: string; phone: string | null; statusName: string; statusColor: string
  amount: number; original: number; type: BonusOpType; accruedAt: string; notification: 'none' | 'scheduled' | 'sent'; state: string
}
export interface ExpiringPage { items: ExpiringLot[]; total: number; page: number; pageSize: number; sum: number; enabled: boolean }

export type BirthdayTiming = 'OnDay' | 'DaysBefore' | 'DaysAfter'
export type BirthdayValidity = 'DaysAfterAccrual' | 'UntilBirthday' | 'DaysAfterBirthday' | 'FixedDate' | 'Unlimited'
export interface CustomerRef { id: string; name: string; phone: string | null }
export interface BirthdaySettings {
  enabled: boolean; amount: number; timing: BirthdayTiming; timingDays: number; time: string; validity: BirthdayValidity; validityDays: number | null
  validityDate: string | null; statusIds: string[]; minPurchaseAmount: number | null; minPurchaseCount: number | null; purchasePeriodDays: number
  excluded: CustomerRef[]; message: string; updatedAt: string
}
export interface UpcomingBirthday {
  birthday: string; daysLeft: number; customerId: string; customerName: string; phone: string | null; statusName: string; statusColor: string
  eligible: boolean; amount: number; plannedDate: string; state: string; transactionId: string | null
}
export interface BirthdaysPage { items: UpcomingBirthday[]; total: number; page: number; pageSize: number; enabled: boolean; withoutBirthDate: number }
export interface BirthdayPreview { customer: string; message: string; amount: number; plannedDate: string; accrualAt: string; expiresAt: string | null }

export interface AudienceFilter {
  statusIds: string[]; positiveBalance: boolean; expiringWithinDays: number | null; minPurchaseAmount: number | null; minPurchaseCount: number | null
  inactiveDays: number | null; birthdayMonth: number | null; registeredFrom: string | null; registeredTo: string | null
}
export const EMPTY_FILTER: AudienceFilter = {
  statusIds: [], positiveBalance: false, expiringWithinDays: null, minPurchaseAmount: null, minPurchaseCount: null, inactiveDays: null, birthdayMonth: null,
  registeredFrom: null, registeredTo: null,
}
export interface Campaign {
  id: string; name: string; type: CampaignType; status: CampaignStatus; channel: string; audienceMode: 'All' | 'Manual'; filter: AudienceFilter
  customerIds: string[]; message: string; bonusAmount: number | null; bonusValidityDays: number | null; scheduledAt: string | null
  recipients: number; sent: number; delivered: number; read: number; failed: number; excluded: number; author: string; createdAt: string
  startedAt: string | null; completedAt: string | null; cancelledAt: string | null; canEdit: boolean; canCancel: boolean; canSend: boolean; canRetry: boolean
}
export interface CampaignsPage { items: Campaign[]; total: number; page: number; pageSize: number }
export interface SaveCampaign {
  name: string; type: CampaignType; channel: string; audienceMode: 'All' | 'Manual'; filter: AudienceFilter; customerIds: string[]; message: string
  bonusAmount: number | null; bonusValidityDays: number | null; scheduledAt?: string | null
}
export interface AudiencePreview { found: number; eligible: number; excluded: number; reasons: { reason: string; count: number }[]; sample: CustomerRef[]; bonusTotal: number }
export interface CampaignRecipient { id: string; customerId: string; customerName: string; contact: string | null; status: string; reason: string | null; sentAt: string | null; read: boolean; bonusTransactionId: string | null }
export interface CampaignRecipientsPage { items: CampaignRecipient[]; total: number; page: number; pageSize: number }

export interface BonusKpi { key: string; value: number | null; previous: number | null; changePercent: number | null; asOfNow: boolean; note: string | null }
export interface BonusDay { date: string; accrued: number; used: number; returned: number; expired: number }
export interface StatusShare { statusId: string | null; name: string; color: string; customers: number; percent: number }
export interface BonusOverview {
  from: string; to: string; generatedAt: string; kpis: BonusKpi[]; days: BonusDay[]; statuses: StatusShare[]; expiring: ExpiringLot[]; expiryEnabled: boolean
  birthdays: UpcomingBirthday[]; birthdayEnabled: boolean; recent: BonusOperation[]
}
export interface TopCustomer { place: number; customerId: string; name: string; statusName: string; statusColor: string; value: number; returned: number; lastOperationAt: string | null }
export interface CampaignEffect { id: string; name: string; sentAt: string | null; recipients: number; sent: number; read: number; readPercent: number | null; bonusTotal: number }
export interface BonusStatistics {
  from: string; to: string; generatedAt: string; kpis: BonusKpi[]; days: BonusDay[]; types: { type: string; amount: number; count: number }[]
  statuses: StatusShare[]; topBalance: TopCustomer[]; topUsage: TopCustomer[]; campaigns: CampaignEffect[]
  expiredInPeriod: number; expiring30: number; expiring60: number; expiring90: number
}

export interface BonusTerms {
  id: string; version: number; content: string; status: 'Draft' | 'Published' | 'Archived'; comment: string | null; effectiveFrom: string | null
  basedOnVersion: number | null; author: string; updatedAt: string; publishedAt: string | null; publishedBy: string | null
}
export interface TermsState { published: BonusTerms | null; draft: BonusTerms | null; versions: BonusTerms[]; template: string }
