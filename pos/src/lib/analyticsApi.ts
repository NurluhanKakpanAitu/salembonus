import { api, download } from './api'
import type { AnalyticsFilters, AnalyticsTab, AnalyticsTabData, Overview, SalesTrend } from './analyticsTypes'

const base = '/pos/v1/analytics'

/** Жалпы сүзгілер + вкладканың жергілікті параметрлері → query string. */
export function analyticsQs(f: AnalyticsFilters, extra: Record<string, string | number | boolean | null | undefined> = {}) {
  const p = new URLSearchParams({ from: f.from, to: f.to, compare: f.compare })
  if (f.storeIds.length) p.set('storeIds', f.storeIds.join(','))
  for (const [k, v] of Object.entries(extra)) if (v !== undefined && v !== null && v !== '') p.set(k, String(v))
  return p.toString()
}

export const analyticsKeys = {
  all: ['analytics'] as const,
}

export const analyticsApi = {
  overview: (f: AnalyticsFilters, extra?: Record<string, string | number | undefined>) => api<Overview>(`${base}/overview?${analyticsQs(f, extra)}`),
  trend: (f: AnalyticsFilters) => api<SalesTrend>(`${base}/sales-trend?${analyticsQs(f)}`),
  tab: (tab: Exclude<AnalyticsTab, 'overview'>, f: AnalyticsFilters, extra: Record<string, string | number | boolean | null | undefined>) =>
    api<AnalyticsTabData>(`${base}/tabs/${tab}?${analyticsQs(f, extra)}`),
  exportFile: (tab: AnalyticsTab, format: 'xlsx' | 'csv', f: AnalyticsFilters, extra: Record<string, string | number | boolean | null | undefined>) =>
    download(`${base}/export?${analyticsQs(f, { ...extra, tab, format, page: undefined, pageSize: undefined })}`, `statistics_${tab}.${format}`),
}
