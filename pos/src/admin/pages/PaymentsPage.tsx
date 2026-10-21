import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { Input, Select, Toggle } from '../../components/ui/Field'
import { RowMenu } from '../../components/ui/RowMenu'
import { toast } from '../../components/ui/Toast'
import { EmptyRow, Pager, td, th } from '../../components/inventory/inventoryUi'
import { BlockError } from '../../components/stats/statsUi'
import { tenge } from '../../lib/money'
import { adminApi, type Payment } from '../adminApi'
import { PaymentModal } from '../PaymentModal'
import { askReason } from '../reason'
import { METHODS, PageHeader, date, dateTime, reportError } from '../ui'

/** «Оплаты»: барлық бизнестің абонент төлемдері — кезең, тәсіл, бизнес бойынша сүзгі, сома; аннулирование себебімен. */
export function PaymentsPage() {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [method, setMethod] = useState('')
  const [organizationId, setOrganizationId] = useState('')
  const [voided, setVoided] = useState(false)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const [adding, setAdding] = useState(false)
  const businesses = useQuery({ queryKey: ['admin', 'businesses', 'names'], queryFn: () => adminApi.businesses({ pageSize: 200, sort: 'name' }) })
  const q = useQuery({
    queryKey: ['admin', 'payments', from, to, method, organizationId, voided, page, pageSize], placeholderData: keepPreviousData,
    queryFn: () => adminApi.payments({ from: from || undefined, to: to || undefined, method: method || undefined, organizationId: organizationId || undefined, includeVoided: voided, page, pageSize }),
  })
  const voidPayment = async (p: Payment) => {
    const reason = await askReason(`Аннулировать оплату ${tenge(p.amount)} от «${p.organizationName}»? «Оплачено до» пересчитается.`)
    if (!reason) return
    try { await adminApi.voidPayment(p.id, reason); toast('Оплата аннулирована'); void q.refetch() } catch (err) { reportError(err) }
  }
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Оплаты" subtitle={q.data ? `Найдено ${q.data.total} · сумма ${tenge(q.data.sum)}` : 'Абонентская плата бизнесов'}
        actions={<Button icon={<Plus size={17} />} onClick={() => setAdding(true)}>Принять оплату</Button>} />
      <div className="flex flex-wrap items-center gap-2 rounded-2xl border border-line bg-surface p-3">
        <Select className="h-11! w-64!" value={organizationId} onChange={(e) => { setOrganizationId(e.target.value); setPage(1) }} aria-label="Бизнес">
          <option value="">Все бизнесы</option>
          {(businesses.data?.rows ?? []).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
        </Select>
        <Input type="date" className="w-40!" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1) }} aria-label="С" />
        <span className="text-ink-3">—</span>
        <Input type="date" className="w-40!" value={to} onChange={(e) => { setTo(e.target.value); setPage(1) }} aria-label="По" />
        <Select className="h-11! w-48!" value={method} onChange={(e) => { setMethod(e.target.value); setPage(1) }} aria-label="Способ">
          <option value="">Все способы</option>
          {Object.entries(METHODS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </Select>
        <span className="ml-auto"><Toggle checked={voided} onChange={(v) => { setVoided(v); setPage(1) }} label="Показать аннулированные" /></span>
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[900px]">
          <thead className="border-b border-line bg-field/60"><tr>
            <th className={th}>Дата</th><th className={th}>Бизнес</th><th className={`${th} text-right`}>Сумма</th><th className={th}>Период</th><th className={th}>Способ</th>
            <th className={th}>Документ</th><th className={th}>Принял</th><th className="w-10" />
          </tr></thead>
          <tbody>
            {!q.data && <EmptyRow colSpan={8} text="Загрузка…" />}
            {q.data?.rows.length === 0 && <EmptyRow colSpan={8} text="Оплат не найдено" />}
            {q.data?.rows.map((p) => (
              <tr key={p.id} className={`border-b border-line last:border-0 ${p.voided ? 'opacity-50' : ''}`}>
                <td className={td}>{date(p.paidOn)}</td>
                <td className={td}><Link to={`/businesses/${p.organizationId}`} className="font-semibold hover:text-brand">{p.organizationName}</Link></td>
                <td className={`${td} text-right font-semibold ${p.voided ? 'line-through' : ''}`}>{tenge(p.amount)}</td>
                <td className={td}>{date(p.periodFrom)} — {date(p.periodTo)}</td>
                <td className={td}>{METHODS[p.method] ?? p.method}</td>
                <td className={td}>{p.reference ?? '—'}{p.voided && <span className="block text-[12px] text-danger">аннулирован: {p.voidReason}</span>}</td>
                <td className={td}><span className="block">{p.createdBy}</span><span className="block text-[12px] text-ink-3">{dateTime(p.createdAt)}</span></td>
                <td className="px-2">{!p.voided && <RowMenu items={[{ label: 'Аннулировать', danger: true, onClick: () => void voidPayment(p) }]} />}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {q.data && <Pager page={page} pageSize={pageSize} total={q.data.total} onPage={setPage} onPageSize={(s) => { setPageSize(s); setPage(1) }} />}
      {adding && <PaymentModal onClose={() => setAdding(false)} onDone={() => { setAdding(false); void q.refetch() }} />}
    </div>
  )
}
