import { useMemo, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ChevronDown, ChevronUp } from 'lucide-react'
import { Input, Select } from '../ui/Field'
import { EmptyRow, Pager, td, th } from '../inventory/inventoryUi'
import { BlockError, Skeleton } from '../stats/statsUi'
import { useAuth } from '../../lib/auth'
import { dateTime } from '../../lib/dates'
import { tenge } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'
import { staffAdminApi, type AuditRow } from '../../lib/staffAdminApi'

const GROUPS = ['auth', 'staff', 'settings', 'register', 'cashier', 'sale', 'catalog', 'product', 'inventory', 'finance', 'bonus', 'clients']

/** Әрекет кілтінің тобы: «auth.login» → «Вход и безопасность» + «login». */
function groupOf(action: string) {
  const head = action.split('.')[0]
  return GROUPS.includes(head) ? head : 'other'
}

function Json({ raw }: { raw: string }) {
  let obj: unknown
  try { obj = JSON.parse(raw) } catch { return <pre className="whitespace-pre-wrap text-[12px]">{raw}</pre> }
  if (obj === null || typeof obj !== 'object') return <span className="text-[12px]">{String(obj)}</span>
  return (
    <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 text-[12px]">
      {Object.entries(obj as Record<string, unknown>).map(([k, v]) => (
        <div key={k} className="contents"><dt className="text-ink-3">{k}</dt><dd className="min-w-0 break-words font-medium">{Array.isArray(v) ? v.join(', ') : v === null || v === undefined ? '—' : typeof v === 'object' ? JSON.stringify(v) : String(v)}</dd></div>
      ))}
    </dl>
  )
}

/**
 * Әрекет журналы (core.audit_log): сүзгілер (қызметкер, дүкен, әрекет тобы, кезең), жол ашылғанда — ескі/жаңа мәндер.
 * compact — қызметкер карточкасындағы «Активность»: тек сол қызметкер, сүзгісіз.
 */
export function AuditTable({ staffId, compact = false }: { staffId?: string; compact?: boolean }) {
  const t = useT()
  const me = useAuth((s) => s.me)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(compact ? 20 : 50)
  const [staff, setStaff] = useState('')
  const [store, setStore] = useState('')
  const [group, setGroup] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [open, setOpen] = useState<string | null>(null)
  const filter = { staffId: staffId ?? (staff || null), storeId: store || null, action: group ? `${group}.` : null, from: from || null, to: to ? `${to}T23:59:59` : null, page, pageSize }
  const q = useQuery({ queryKey: ['audit', filter], queryFn: () => staffAdminApi.audit(filter), placeholderData: keepPreviousData })
  const people = useQuery({ queryKey: ['staff', 'list'], queryFn: staffAdminApi.list, enabled: !compact })
  const groups = useMemo(() => { const g = new Set((q.data?.actions ?? []).map(groupOf)); return GROUPS.filter((x) => g.has(x)).concat(g.has('other') ? ['other'] : []) }, [q.data?.actions])
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />

  const objectOf = (a: AuditRow) => [a.entity, a.registerName].filter(Boolean).join(' · ') || '—'
  return (
    <div className="flex flex-col gap-3">
      {!compact && (
        <div className="flex flex-wrap items-center gap-2">
          <Select className="h-10! w-auto min-w-44 text-[13px]" value={staff} onChange={(e) => { setStaff(e.target.value); setPage(1) }} aria-label={t('audit.col.who')}>
            <option value="">{t('audit.filter.staff')}</option>
            {(people.data?.rows ?? []).map((p) => <option key={p.id} value={p.id}>{p.firstName} {p.lastName}</option>)}
          </Select>
          {(me?.stores.length ?? 0) > 1 && (
            <Select className="h-10! w-auto min-w-40 text-[13px]" value={store} onChange={(e) => { setStore(e.target.value); setPage(1) }} aria-label={t('audit.filter.store')}>
              <option value="">{t('audit.filter.store')}</option>
              {me!.stores.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </Select>
          )}
          <Select className="h-10! w-auto min-w-44 text-[13px]" value={group} onChange={(e) => { setGroup(e.target.value); setPage(1) }} aria-label={t('audit.col.action')}>
            <option value="">{t('audit.filter.action')}</option>
            {groups.map((g) => <option key={g} value={g}>{t(`audit.group.${g}` as TranslationKey)}</option>)}
          </Select>
          <label className="flex items-center gap-1.5 text-[13px] text-ink-2">{t('audit.filter.from')}<Input type="date" className="h-10! w-40" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1) }} /></label>
          <label className="flex items-center gap-1.5 text-[13px] text-ink-2">{t('audit.filter.to')}<Input type="date" className="h-10! w-40" value={to} onChange={(e) => { setTo(e.target.value); setPage(1) }} /></label>
        </div>
      )}
      {!q.data ? <Skeleton className="h-40" /> : (
        <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
          <table className="w-full min-w-[720px]">
            <thead className="border-b border-line bg-field/60"><tr>
              <th className={th}>{t('audit.col.time')}</th>{!compact && <th className={th}>{t('audit.col.who')}</th>}<th className={th}>{t('audit.col.action')}</th>
              <th className={th}>{t('audit.col.object')}</th><th className={th}>{t('audit.col.details')}</th><th className="w-10" />
            </tr></thead>
            <tbody>
              {q.data.rows.length === 0 && <EmptyRow colSpan={compact ? 5 : 6} text={t('audit.empty')} />}
              {q.data.rows.map((a) => {
                const expanded = open === a.id
                const hasMore = !!(a.oldValues || a.newValues)
                return (
                  <tr key={a.id} className="border-b border-line align-top last:border-0">
                    <td className={`${td} whitespace-nowrap text-ink-2`}>{dateTime(a.createdAt)}</td>
                    {!compact && <td className={td}><span className="block font-medium">{a.staffName ?? t('audit.system')}</span>{a.storeName && <span className="block text-[12px] text-ink-3">{a.storeName}</span>}</td>}
                    <td className={td}>
                      <span className="block font-medium">{t(`audit.group.${groupOf(a.action)}` as TranslationKey)}</span>
                      <span className="block text-[12px] text-ink-3">{a.action}{!a.success && <span className="ml-1 rounded bg-danger-soft px-1 text-danger">{t('audit.failed')}</span>}</span>
                    </td>
                    <td className={td}>{objectOf(a)}{a.amount != null && <span className="block text-[12px] text-ink-3">{tenge(a.amount)}</span>}</td>
                    <td className={`${td} max-w-96`}>
                      {a.details && <span className="block text-[13px]">{a.details}</span>}
                      {expanded && (
                        <div className="mt-1 grid gap-2 sm:grid-cols-2">
                          {a.oldValues && <div className="rounded-lg bg-field p-2"><div className="mb-1 text-[11px] font-bold uppercase text-ink-3">{t('audit.before')}</div><Json raw={a.oldValues} /></div>}
                          {a.newValues && <div className="rounded-lg bg-success-soft/50 p-2"><div className="mb-1 text-[11px] font-bold uppercase text-ink-3">{t('audit.after')}</div><Json raw={a.newValues} /></div>}
                        </div>
                      )}
                    </td>
                    <td className="px-2 py-2.5">
                      {hasMore && (
                        <button type="button" onClick={() => setOpen(expanded ? null : a.id)} aria-label={expanded ? t('audit.hide') : t('audit.show')}
                          className="flex size-8 items-center justify-center rounded-lg text-ink-3 hover:bg-field hover:text-ink">{expanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}</button>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
      {q.data && <Pager page={page} pageSize={pageSize} total={q.data.total} onPage={setPage} onPageSize={(s) => { setPageSize(s); setPage(1) }} />}
    </div>
  )
}
