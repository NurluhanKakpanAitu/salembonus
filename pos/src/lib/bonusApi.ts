import { api, download } from './api'
import { qs } from './financeApi'
import type {
  AudiencePreview, BirthdayPreview, BirthdaySettings, BirthdaysPage, BonusCustomerCard, BonusCustomersPage, BonusMeta, BonusOperation, BonusOperationDetail,
  BonusOperationsPage, BonusOverview, BonusStatistics, BonusStatus, BonusTerms, Campaign, CampaignRecipientsPage, CampaignsPage, ExpiringPage, ExpirySettings,
  SaveCampaign, SaveStatus, TermsState, UpcomingBirthday,
} from './bonusTypes'

const base = '/pos/v1/bonus'
type Params = Record<string, string | number | boolean | null | undefined>
const post = <T>(path: string, body: unknown = {}) => api<T>(`${base}${path}`, { method: 'POST', body: JSON.stringify(body) })
const put = <T>(path: string, body: unknown) => api<T>(`${base}${path}`, { method: 'PUT', body: JSON.stringify(body) })

export const bonusKeys = { all: ['bonus'] as const, meta: ['bonus', 'meta'] as const }

export const bonusApi = {
  meta: () => api<BonusMeta>(`${base}/meta`),
  overview: (p: Params) => api<BonusOverview>(`${base}/overview${qs(p)}`),
  statistics: (p: Params) => api<BonusStatistics>(`${base}/statistics${qs(p)}`),

  statuses: () => api<BonusStatus[]>(`${base}/statuses`),
  createStatus: (r: SaveStatus) => post<BonusStatus>('/statuses', r),
  updateStatus: (id: string, r: SaveStatus) => put<BonusStatus>(`/statuses/${id}`, r),
  duplicateStatus: (id: string) => post<BonusStatus>(`/statuses/${id}/duplicate`),
  setStatusActive: (id: string, active: boolean) => post<BonusStatus>(`/statuses/${id}/${active ? 'activate' : 'deactivate'}`),
  deleteStatus: (id: string) => api<void>(`${base}/statuses/${id}`, { method: 'DELETE' }),

  customers: (p: Params) => api<BonusCustomersPage>(`${base}/customers${qs(p)}`),
  customer: (id: string) => api<BonusCustomerCard>(`${base}/customers/${id}`),
  changeStatus: (id: string, r: { mode: 'rules' | 'manual'; statusId?: string | null; reason?: string | null }) => post<BonusCustomerCard>(`/customers/${id}/status`, r),
  optOut: (id: string, optOut: boolean, reason?: string) => post<BonusCustomerCard>(`/customers/${id}/opt-out`, { optOut, reason }),

  expirySettings: () => api<ExpirySettings>(`${base}/expiry/settings`),
  saveExpiry: (r: Omit<ExpirySettings, 'openLots' | 'openLotsWithoutExpiry' | 'updatedAt'> & { applyToExisting: boolean }) => put<ExpirySettings>('/expiry/settings', r),
  expiring: (p: Params) => api<ExpiringPage>(`${base}/expiry/upcoming${qs(p)}`),
  setLotExpiry: (lotId: string, expiresAt: string | null, reason: string) => put<void>(`/lots/${lotId}/expiry`, { expiresAt, reason }),

  birthdaySettings: () => api<BirthdaySettings>(`${base}/birthday/settings`),
  saveBirthday: (r: Omit<BirthdaySettings, 'excluded' | 'updatedAt'> & { excludedCustomerIds: string[] }) => put<BirthdaySettings>('/birthday/settings', r),
  birthdays: (p: Params) => api<BirthdaysPage>(`${base}/birthday/upcoming${qs(p)}`),
  birthdayPreview: (customerId?: string) => api<BirthdayPreview>(`${base}/birthday/preview${qs({ customerId })}`),
  grantBirthday: (customerId: string, reason: string) => post<UpcomingBirthday | null>('/birthday/grants', { customerId, reason }),

  campaigns: (p: Params) => api<CampaignsPage>(`${base}/campaigns${qs(p)}`),
  campaign: (id: string) => api<Campaign>(`${base}/campaigns/${id}`),
  createCampaign: (r: SaveCampaign) => post<Campaign>('/campaigns', r),
  updateCampaign: (id: string, r: SaveCampaign) => put<Campaign>(`/campaigns/${id}`, r),
  duplicateCampaign: (id: string) => post<Campaign>(`/campaigns/${id}/duplicate`),
  audience: (r: SaveCampaign) => post<AudiencePreview>('/campaigns/audience', r),
  testCampaign: (id: string, r: { customerId?: string; phone?: string; message?: string }) => post<{ customer: string; message: string; delivered: boolean }>(`/campaigns/${id}/test`, r),
  scheduleCampaign: (id: string, scheduledAt: string) => post<Campaign>(`/campaigns/${id}/schedule`, { scheduledAt }),
  sendCampaign: (id: string) => post<Campaign>(`/campaigns/${id}/send`),
  cancelCampaign: (id: string) => post<Campaign>(`/campaigns/${id}/cancel`),
  retryCampaign: (id: string) => post<Campaign>(`/campaigns/${id}/retry`),
  recipients: (id: string, p: Params) => api<CampaignRecipientsPage>(`${base}/campaigns/${id}/recipients${qs(p)}`),

  operations: (p: Params) => api<BonusOperationsPage>(`${base}/operations${qs(p)}`),
  operation: (id: string) => api<BonusOperationDetail>(`${base}/operations/${id}`),
  accrue: (r: { customerId: string; amount: number; reason: string; validityDays: number | null; unlimited: boolean; comment?: string; clientRequestId: string }) =>
    post<BonusOperation>('/operations/accrue', r),
  writeOff: (r: { customerId: string; amount: number; reason: string; comment?: string; clientRequestId: string }) => post<BonusOperation>('/operations/write-off', r),
  returnBonus: (id: string, amount: number, reason: string, clientRequestId: string) => post<BonusOperation>(`/operations/${id}/return`, { amount, reason, clientRequestId }),
  reverse: (id: string, reason: string) => post<BonusOperation>(`/operations/${id}/reverse`, { reason }),

  terms: () => api<TermsState>(`${base}/terms`),
  saveTerms: (content: string, comment?: string) => put<BonusTerms>('/terms/draft', { content, comment }),
  publishTerms: (effectiveFrom: string | null, comment?: string) => post<BonusTerms>('/terms/publish', { effectiveFrom, comment }),
  restoreTerms: (id: string) => post<BonusTerms>(`/terms/${id}/restore`),

  runJobs: () => post<{ expired: number; birthdays: number; warnings: number; campaigns: number; statuses: number }>('/jobs/run'),
  exportFile: (report: string, format: 'xlsx' | 'csv', p: Params) => download(`${base}/export${qs({ ...p, report, format, page: undefined, pageSize: undefined })}`, `bonus_${report}.${format}`),
}
