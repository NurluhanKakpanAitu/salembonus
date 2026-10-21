import { useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { Building2, MessageSquare } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { Select, Textarea } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { SearchInput } from '../../components/ui/SearchInput'
import { toast } from '../../components/ui/Toast'
import { EmptyRow, td, th, useDebounced } from '../../components/inventory/inventoryUi'
import { BlockError } from '../../components/stats/statsUi'
import { adminApi, type Lead, type LeadStatus } from '../adminApi'
import { PageHeader, dateTime, reportError } from '../ui'
import { CreateBusinessDrawer } from './BusinessesPage'

const STATUS: Record<LeadStatus, { label: string; cls: string }> = {
  new: { label: 'Новая', cls: 'bg-brand-soft text-brand' },
  contacted: { label: 'Связались', cls: 'bg-amber-50 text-amber-700' },
  won: { label: 'Подключён', cls: 'bg-success-soft text-success' },
  lost: { label: 'Отказ', cls: 'bg-field text-ink-2' },
}
const INTEREST = { pos: 'SalemPos', bonus: 'SalemBonus', both: 'Обе системы' }

/** «Заявки»: лендингтен (salemtech.kz) түскен өтінімдер — күйі, пікір, өтінімнен бизнес қосу (өтінім «Подключён» болады). */
export function LeadsPage() {
  const qc = useQueryClient()
  const nav = useNavigate()
  const [status, setStatus] = useState<string>('new')
  const [search, setSearch] = useState('')
  const q = useDebounced(search, 300)
  const [comment, setComment] = useState<Lead | null>(null)
  const [creating, setCreating] = useState<Lead | null>(null)
  const list = useQuery({
    queryKey: ['admin', 'leads', status, q], placeholderData: keepPreviousData,
    queryFn: () => adminApi.leads({ status: status === 'all' ? undefined : status, search: q || undefined, pageSize: 200 }),
  })
  const plans = useQuery({ queryKey: ['admin', 'plans'], queryFn: adminApi.plans })
  const refresh = () => { void list.refetch(); void qc.invalidateQueries({ queryKey: ['admin', 'overview'] }) }
  const setLeadStatus = async (l: Lead, s: LeadStatus) => {
    try { await adminApi.updateLead(l.id, { status: s }); refresh() } catch (err) { reportError(err) }
  }
  if (list.isError) return <BlockError message={(list.error as Error).message} onRetry={() => void list.refetch()} />
  const counts = list.data?.counts ?? {}
  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Заявки" subtitle="С лендинга salemtech.kz: свяжитесь с клиентом и подключите бизнес в один клик" />
      <div className="flex flex-wrap items-center gap-1.5">
        {(['new', 'contacted', 'won', 'lost', 'all'] as const).map((s) => (
          <button key={s} type="button" onClick={() => setStatus(s)}
            className={`rounded-full border px-3 py-1.5 text-[13px] font-semibold ${status === s ? 'border-brand bg-brand text-white' : 'border-line bg-surface text-ink-2 hover:border-brand/50'}`}>
            {s === 'all' ? 'Все' : STATUS[s].label} <span className="opacity-70">{counts[s] ?? 0}</span>
          </button>
        ))}
        <SearchInput value={search} onChange={setSearch} placeholder="Имя, телефон, бизнес, город" className="ml-auto min-w-64" />
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[960px]">
          <thead className="border-b border-line bg-field/60"><tr>
            <th className={th}>Дата</th><th className={th}>Контакт</th><th className={th}>Бизнес</th><th className={th}>Интерес</th><th className={th}>Комментарий</th><th className={th}>Статус</th><th className="w-48" />
          </tr></thead>
          <tbody>
            {!list.data && <EmptyRow colSpan={7} text="Загрузка…" />}
            {list.data?.rows.length === 0 && <EmptyRow colSpan={7} text="Заявок нет" />}
            {list.data?.rows.map((l) => (
              <tr key={l.id} className="border-b border-line align-top last:border-0">
                <td className={`${td} whitespace-nowrap`}>{dateTime(l.createdAt)}<span className="block text-[12px] uppercase text-ink-3">{l.language}</span></td>
                <td className={td}><span className="block font-semibold">{l.name}</span><a href={`tel:${l.phone}`} className="block text-[13px] text-brand hover:underline">{l.phone}</a></td>
                <td className={td}><span className="block">{l.businessName ?? '—'}</span><span className="block text-[12px] text-ink-3">{l.city ?? ''}</span></td>
                <td className={td}>{INTEREST[l.interest]}</td>
                <td className={`${td} max-w-72`}>
                  {l.comment && <span className="block whitespace-pre-wrap text-[13px]">{l.comment}</span>}
                  {l.adminComment && <span className="mt-1 block rounded-lg bg-field px-2 py-1 text-[12px] text-ink-2">{l.adminComment}{l.updatedBy ? ` — ${l.updatedBy}` : ''}</span>}
                  {!l.comment && !l.adminComment && <span className="text-ink-3">—</span>}
                </td>
                <td className={td}>
                  <Select className="h-9! w-36! text-[13px]" value={l.status} onChange={(e) => void setLeadStatus(l, e.target.value as LeadStatus)} aria-label="Статус">
                    {(Object.keys(STATUS) as LeadStatus[]).map((s) => <option key={s} value={s}>{STATUS[s].label}</option>)}
                  </Select>
                  {l.organizationId && <Link to={`/businesses/${l.organizationId}`} className="mt-1 block text-[12px] font-semibold text-brand hover:underline">Открыть бизнес</Link>}
                </td>
                <td className="px-3 py-2.5">
                  <div className="flex justify-end gap-1.5">
                    <Button variant="secondary" className="h-9! px-2.5!" icon={<MessageSquare size={15} />} onClick={() => setComment(l)} aria-label="Комментарий" />
                    {!l.organizationId && <Button className="h-9! px-3! text-[13px]!" icon={<Building2 size={15} />} onClick={() => setCreating(l)}>Создать бизнес</Button>}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {comment && <CommentModal lead={comment} onClose={() => setComment(null)} onSaved={() => { setComment(null); refresh() }} />}
      {creating && (
        <CreateBusinessDrawer plans={plans.data ?? []} onClose={() => setCreating(null)}
          prefill={{
            name: creating.businessName ?? '', city: creating.city ?? '', contactName: creating.name, contactPhone: creating.phone, ownerPhone: creating.phone,
            ownerFirst: creating.name.split(' ')[0], comment: [creating.comment, `Заявка с сайта ${dateTime(creating.createdAt)}`].filter(Boolean).join('\n'),
          }}
          onCreated={async (orgId) => {
            const lead = creating
            setCreating(null)
            try { await adminApi.updateLead(lead.id, { status: 'won', organizationId: orgId }); toast('Заявка отмечена как подключённая') } catch (err) { reportError(err) }
            refresh()
            nav(`/businesses/${orgId}`)
          }} />
      )}
    </div>
  )
}

function CommentModal({ lead, onClose, onSaved }: { lead: Lead; onClose: () => void; onSaved: () => void }) {
  const [text, setText] = useState(lead.adminComment ?? '')
  const [busy, setBusy] = useState(false)
  const save = async () => {
    setBusy(true)
    try { await adminApi.updateLead(lead.id, { adminComment: text.trim() || null, status: lead.status === 'new' ? 'contacted' : lead.status }); onSaved() } catch (err) { reportError(err) } finally { setBusy(false) }
  }
  return (
    <Modal title={`Комментарий · ${lead.name}`} onClose={onClose} width={480}>
      <div className="flex flex-col gap-3">
        <Textarea value={text} maxLength={1000} autoFocus onChange={(e) => setText(e.target.value)} placeholder="Итог звонка, договорённости" className="min-h-28" />
        {lead.status === 'new' && <p className="text-[12px] text-ink-3">После сохранения заявка перейдёт в статус «Связались».</p>}
        <div className="flex justify-end gap-2"><Button variant="secondary" onClick={onClose}>Отмена</Button><Button loading={busy} onClick={() => void save()}>Сохранить</Button></div>
      </div>
    </Modal>
  )
}
