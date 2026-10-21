import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, Ban, CirclePlay, CreditCard, KeyRound, Pencil, Plus, Power, Store, UserPlus } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { confirmDialog } from '../../components/ui/Confirm'
import { Drawer } from '../../components/ui/Drawer'
import { Field, Input, Select, Textarea, Toggle } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { RowMenu } from '../../components/ui/RowMenu'
import { toast } from '../../components/ui/Toast'
import { td, th } from '../../components/inventory/inventoryUi'
import { BlockError, Skeleton } from '../../components/stats/statsUi'
import { num, tenge } from '../../lib/money'
import { AdminApiError, adminApi, type BusinessDetail, type ModuleKey, type Payment } from '../adminApi'
import { PaymentModal } from '../PaymentModal'
import { askReason } from '../reason'
import { METHODS, MODULES, PageHeader, Panel, SecretModal, StatusPill, date, dateTime, errText, moduleLabel, reportError } from '../ui'

type Tab = 'overview' | 'modules' | 'payments' | 'staff' | 'stores' | 'history'
const TABS: { key: Tab; label: string }[] = [
  { key: 'overview', label: 'Обзор' }, { key: 'modules', label: 'Модули' }, { key: 'payments', label: 'Оплаты' },
  { key: 'staff', label: 'Доступы' }, { key: 'stores', label: 'Магазины' }, { key: 'history', label: 'История' },
]

/** Бизнес карточкасы: төлем күйі мен қолданыс, модульдер, төлемдер, аккаунттар, дүкендер, әкімшілік журналы. */
export function BusinessPage() {
  const { id = '' } = useParams()
  const qc = useQueryClient()
  const [tab, setTab] = useState<Tab>('overview')
  const [modal, setModal] = useState<'edit' | 'payment' | 'suspend' | null>(null)
  const key = ['admin', 'business', id]
  const q = useQuery({ queryKey: key, queryFn: () => adminApi.business(id) })
  const d = q.data
  const update = (next: BusinessDetail) => { qc.setQueryData(key, next); void qc.invalidateQueries({ queryKey: ['admin', 'businesses'] }); void qc.invalidateQueries({ queryKey: ['admin', 'overview'] }) }
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  if (!d) return <Skeleton className="h-96" />
  const b = d.business
  const suspended = b.status === 'suspended'
  const activate = async () => {
    try { update(await adminApi.activate(id)); toast('Доступ восстановлен') } catch (err) { reportError(err) }
  }
  return (
    <div className="flex flex-col gap-4">
      <Link to="/businesses" className="inline-flex items-center gap-1 text-[13px] font-semibold text-ink-2 hover:text-brand"><ArrowLeft size={15} />Бизнесы</Link>
      <PageHeader title={<span className="flex flex-wrap items-center gap-3">{b.name}<StatusPill status={b.status} /></span>}
        subtitle={[b.city, b.bin ? `БИН ${b.bin}` : null, `создан ${date(b.createdAt)}`].filter(Boolean).join(' · ')}
        actions={<>
          <Button variant="secondary" icon={<Pencil size={16} />} onClick={() => setModal('edit')}>Редактировать</Button>
          {suspended ? <Button variant="secondary" icon={<CirclePlay size={16} />} onClick={() => void activate()}>Возобновить</Button>
            : <Button variant="secondary" icon={<Ban size={16} />} onClick={() => setModal('suspend')}>Приостановить</Button>}
          <Button icon={<CreditCard size={16} />} onClick={() => setModal('payment')}>Принять оплату</Button>
        </>} />
      {suspended && <p className="rounded-xl border border-danger/30 bg-danger-soft px-4 py-3 text-[14px] text-danger">Приостановлен {dateTime(d.suspendedAt)}: {d.suspendReason}. Сотрудники не могут войти в SalemPos.</p>}

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        <Mini label="Тариф" value={b.planName ?? '—'} hint={`${tenge(b.fee)}/мес${b.customFee ? ' · своя цена' : ''}`} />
        <Mini label={b.status === 'trial' ? 'Пробный до' : 'Оплачено до'} value={date(b.status === 'trial' ? b.trialUntil : b.paidUntil)}
          hint={b.daysOverdue ? `просрочено ${b.daysOverdue} дн.` : `всего оплачено ${tenge(d.paidTotal)}`} danger={!!b.daysOverdue} />
        <Mini label="Продажи за 30 дней" value={tenge(b.revenue30)} hint={`${num(b.sales30)} чеков · последняя ${date(b.lastSaleAt)}`} />
        <Mini label="Магазины / сотрудники" value={`${b.stores} / ${b.staff}`} hint={`последний вход ${dateTime(b.lastLoginAt)}`} />
        <Mini label="Клиенты" value={num(b.clients)} hint="с бонусной картой" />
      </div>

      <nav className="flex gap-1 overflow-x-auto border-b border-line [scrollbar-width:none]">
        {TABS.map((t) => (
          <button key={t.key} type="button" onClick={() => setTab(t.key)}
            className={`-mb-px shrink-0 border-b-2 px-3 py-2.5 text-[14px] font-semibold ${tab === t.key ? 'border-brand text-brand' : 'border-transparent text-ink-2 hover:text-ink'}`}>
            {t.label}{t.key === 'payments' ? ` (${d.payments.filter((p) => !p.voided).length})` : t.key === 'staff' ? ` (${d.staff.length})` : t.key === 'stores' ? ` (${d.stores.length})` : ''}
          </button>
        ))}
      </nav>

      {tab === 'overview' && <OverviewTab d={d} />}
      {tab === 'modules' && <ModulesTab d={d} onSaved={update} />}
      {tab === 'payments' && <PaymentsTab d={d} onPay={() => setModal('payment')} onChanged={() => void q.refetch()} />}
      {tab === 'staff' && <StaffTab d={d} onChanged={() => void q.refetch()} />}
      {tab === 'stores' && <StoresTab d={d} onSaved={update} />}
      {tab === 'history' && <HistoryTab d={d} />}

      {modal === 'edit' && <EditDrawer d={d} onClose={() => setModal(null)} onSaved={(next) => { setModal(null); update(next) }} />}
      {modal === 'payment' && <PaymentModal organizationId={id} onClose={() => setModal(null)} onDone={() => { setModal(null); void q.refetch() }} />}
      {modal === 'suspend' && <SuspendModal id={id} name={b.name} onClose={() => setModal(null)} onDone={(next) => { setModal(null); update(next) }} />}
    </div>
  )
}

function Mini({ label, value, hint, danger }: { label: string; value: React.ReactNode; hint?: string; danger?: boolean }) {
  return (
    <div className="min-w-0 rounded-2xl border border-line bg-surface p-3.5">
      <div className="break-words text-[12px] text-ink-2">{label}</div>
      <div className="mt-1 break-words text-[18px] font-bold">{value}</div>
      {hint && <div className={`break-words text-[12px] ${danger ? 'text-danger' : 'text-ink-3'}`}>{hint}</div>}
    </div>
  )
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return <div className="flex gap-3 py-2 text-[14px]"><span className="w-44 shrink-0 text-ink-2">{label}</span><span className="min-w-0 flex-1 break-words font-medium">{value || '—'}</span></div>
}

function OverviewTab({ d }: { d: BusinessDetail }) {
  const b = d.business
  return (
    <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
      <Panel title="Контакты">
        <div className="divide-y divide-line">
          <Row label="Контактное лицо" value={b.contactName} />
          <Row label="Телефон" value={b.contactPhone} />
          <Row label="E-mail" value={d.contactEmail} />
          <Row label="Владелец" value={[b.ownerName, b.ownerPhone].filter(Boolean).join(' · ')} />
          <Row label="Город" value={b.city} />
          <Row label="Комментарий" value={d.comment} />
        </div>
      </Panel>
      <Panel title="Подписка">
        <div className="divide-y divide-line">
          <Row label="Тариф" value={b.planName ?? 'Без тарифа'} />
          <Row label="Плата в месяц" value={`${tenge(b.fee)}${b.customFee ? ' (своя цена)' : ''}`} />
          <Row label="Пробный период до" value={date(b.trialUntil)} />
          <Row label="Оплачено до" value={date(b.paidUntil)} />
          <Row label="Всего оплачено" value={tenge(d.paidTotal)} />
          <Row label="Модули" value={b.modules.map(moduleLabel).join(', ') || 'нет'} />
        </div>
      </Panel>
    </div>
  )
}

/** Модульдер: өшірілгені бизнестің барлық қызметкеріне (иесіне де) келесі сұраныстан бастап жабық; деректер жойылмайды. */
function ModulesTab({ d, onSaved }: { d: BusinessDetail; onSaved: (next: BusinessDetail) => void }) {
  const [modules, setModules] = useState<ModuleKey[]>(d.business.modules)
  const [busy, setBusy] = useState(false)
  useEffect(() => setModules(d.business.modules), [d.business.modules])
  const dirty = [...modules].sort().join() !== [...d.business.modules].sort().join()
  const save = async () => {
    const off = d.business.modules.filter((m) => !modules.includes(m))
    if (off.length && !(await confirmDialog({ message: `Отключить: ${off.map(moduleLabel).join(', ')}? Разделы сразу закроются для всех сотрудников бизнеса, данные сохранятся.`, confirmLabel: 'Сохранить', danger: true }))) return
    setBusy(true)
    try { onSaved(await adminApi.setModules(d.business.id, modules)); toast('Модули сохранены') } catch (err) { reportError(err) } finally { setBusy(false) }
  }
  return (
    <Panel title="Подключённые модули" action={<Button loading={busy} disabled={!dirty} onClick={() => void save()}>Сохранить</Button>}>
      <div className="grid grid-cols-1 gap-2 md:grid-cols-2">
        {MODULES.map((m) => {
          const on = modules.includes(m.key)
          return (
            <div key={m.key} className={`flex items-center gap-3 rounded-xl border p-3 ${on ? 'border-brand/40 bg-brand-soft/30' : 'border-line'}`}>
              <span className={`flex size-9 items-center justify-center rounded-xl ${on ? 'bg-brand text-white' : 'bg-field text-ink-3'}`}>{m.icon}</span>
              <span className="min-w-0 flex-1"><span className="block text-[14px] font-semibold">{m.label}</span><span className="block text-[12px] text-ink-3">{m.hint}</span></span>
              <Toggle checked={on} onChange={(v) => setModules(v ? MODULES.filter((x) => x.key === m.key || modules.includes(x.key)).map((x) => x.key) : modules.filter((x) => x !== m.key))} label="" />
            </div>
          )
        })}
      </div>
      <p className="mt-3 text-[12px] text-ink-3">Сотрудники и Настройки доступны всегда. Отключение не удаляет данные — после включения всё вернётся.</p>
    </Panel>
  )
}

function PaymentsTab({ d, onPay, onChanged }: { d: BusinessDetail; onPay: () => void; onChanged: () => void }) {
  const voidPayment = async (p: Payment) => {
    const reason = await askReason(`Аннулировать оплату ${tenge(p.amount)} от ${date(p.paidOn)}?`)
    if (!reason) return
    try { await adminApi.voidPayment(p.id, reason); toast('Оплата аннулирована'); onChanged() } catch (err) { reportError(err) }
  }
  return (
    <Panel title={`Оплаты · всего ${tenge(d.paidTotal)}`} action={<Button icon={<Plus size={16} />} onClick={onPay}>Принять оплату</Button>}>
      {d.payments.length === 0 ? <p className="py-8 text-center text-[13px] text-ink-3">Оплат ещё не было</p> : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px]">
            <thead className="border-b border-line"><tr>
              <th className={th}>Дата</th><th className={`${th} text-right`}>Сумма</th><th className={th}>Период</th><th className={th}>Способ</th><th className={th}>Документ</th><th className={th}>Принял</th><th className="w-10" />
            </tr></thead>
            <tbody>
              {d.payments.map((p) => (
                <tr key={p.id} className={`border-b border-line last:border-0 ${p.voided ? 'opacity-50' : ''}`}>
                  <td className={td}>{date(p.paidOn)}</td>
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
      )}
    </Panel>
  )
}

function StaffTab({ d, onChanged }: { d: BusinessDetail; onChanged: () => void }) {
  const [adding, setAdding] = useState(false)
  const [secret, setSecret] = useState<{ title: string; phone: string; password: string } | null>(null)
  const reset = async (staffId: string, name: string) => {
    if (!(await confirmDialog({ message: `Сбросить пароль ${name}? Все сеансы и PIN будут сброшены.`, confirmLabel: 'Сбросить', danger: true }))) return
    try { const r = await adminApi.resetStaffPassword(d.business.id, staffId); setSecret({ title: `Новый пароль · ${name}`, phone: r.account.phone, password: r.tempPassword }); onChanged() } catch (err) { reportError(err) }
  }
  const setActive = async (staffId: string, active: boolean) => {
    try { await adminApi.setStaffActive(d.business.id, staffId, active); toast(active ? 'Доступ включён' : 'Доступ отключён'); onChanged() } catch (err) { reportError(err) }
  }
  return (
    <Panel title="Сотрудники и доступы" action={<Button icon={<UserPlus size={16} />} onClick={() => setAdding(true)}>Добавить владельца / админа</Button>}>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[720px]">
          <thead className="border-b border-line"><tr><th className={th}>Сотрудник</th><th className={th}>Роли</th><th className={th}>Вход</th><th className={th}>Последний вход</th><th className="w-10" /></tr></thead>
          <tbody>
            {d.staff.map((s) => (
              <tr key={s.id} className={`border-b border-line last:border-0 ${s.isActive ? '' : 'opacity-50'}`}>
                <td className={td}><span className="block font-semibold">{s.name}</span><span className="block text-[12px] text-ink-3">{s.phone}</span></td>
                <td className={td}>{s.roles.map((r) => <span key={r.storeId} className="mr-1 inline-block rounded-md bg-field px-1.5 py-0.5 text-[12px]">{r.role === 'Owner' ? 'Владелец' : r.role === 'Admin' ? 'Админ' : 'Кассир'} · {r.storeName}</span>)}</td>
                <td className={td}>{[s.hasPassword ? 'пароль' : null, s.hasPin ? 'PIN' : null].filter(Boolean).join(' + ') || '—'}{!s.isActive && <span className="ml-1 text-danger">отключён</span>}</td>
                <td className={td}>{dateTime(s.lastLoginAt)}</td>
                <td className="px-2"><RowMenu items={[
                  ...(s.hasPassword ? [{ label: 'Сбросить пароль', icon: <KeyRound size={16} />, onClick: () => void reset(s.id, s.name) }] : []),
                  { label: s.isActive ? 'Отключить доступ' : 'Включить доступ', icon: <Power size={16} />, danger: s.isActive, onClick: () => void setActive(s.id, !s.isActive) },
                ]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="mt-3 text-[12px] text-ink-3">Кассиров и права сотрудников владелец настраивает сам в SalemPos → «Сотрудники».</p>
      {adding && <AccountModal d={d} onClose={() => setAdding(false)} onDone={(r) => { setAdding(false); setSecret(r); onChanged() }} />}
      {secret && <SecretModal title={secret.title} onClose={() => setSecret(null)}
        lines={[{ label: 'Вход', value: 'https://pos.salemtech.kz' }, { label: 'Телефон', value: secret.phone }, { label: 'Пароль', value: secret.password, secret: true }]} />}
    </Panel>
  )
}

function AccountModal({ d, onClose, onDone }: { d: BusinessDetail; onClose: () => void; onDone: (r: { title: string; phone: string; password: string }) => void }) {
  const [phone, setPhone] = useState('+7')
  const [first, setFirst] = useState('')
  const [last, setLast] = useState('')
  const [role, setRole] = useState<'Owner' | 'Admin'>('Admin')
  const [storeId, setStoreId] = useState(d.stores[0]?.id ?? '')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const r = await adminApi.addAccount(d.business.id, { phone, firstName: first, lastName: last || null, role, storeId: role === 'Admin' ? storeId : null })
      onDone({ title: `Доступ создан · ${r.account.name}`, phone: r.account.phone, password: r.tempPassword })
    } catch (err) { setError(errText(err)) } finally { setBusy(false) }
  }
  return (
    <Modal title="Новый доступ" onClose={onClose} width={480}>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <Field label="Роль"><Select value={role} onChange={(e) => setRole(e.target.value as 'Owner' | 'Admin')}><option value="Admin">Администратор магазина</option><option value="Owner">Владелец (все магазины)</option></Select></Field>
        {role === 'Admin' && <Field label="Магазин"><Select value={storeId} onChange={(e) => setStoreId(e.target.value)}>{d.stores.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</Select></Field>}
        <Field label="Телефон" required><Input type="tel" value={phone} onChange={(e) => setPhone(e.target.value)} /></Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Имя" required><Input value={first} maxLength={60} onChange={(e) => setFirst(e.target.value)} /></Field>
          <Field label="Фамилия"><Input value={last} maxLength={60} onChange={(e) => setLast(e.target.value)} /></Field>
        </div>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2"><Button type="button" variant="secondary" onClick={onClose}>Отмена</Button><Button type="submit" loading={busy} disabled={!first.trim()}>Создать</Button></div>
      </form>
    </Modal>
  )
}

function StoresTab({ d, onSaved }: { d: BusinessDetail; onSaved: (next: BusinessDetail) => void }) {
  const [adding, setAdding] = useState(false)
  const toggle = async (storeId: string, active: boolean, name: string) => {
    if (!active && !(await confirmDialog({ message: `Закрыть магазин «${name}»? Сотрудники не смогут в нём работать, данные сохранятся.`, confirmLabel: 'Закрыть', danger: true }))) return
    try { onSaved(await adminApi.setStoreActive(d.business.id, storeId, active)) } catch (err) { reportError(err) }
  }
  return (
    <Panel title="Магазины" action={<Button icon={<Store size={16} />} onClick={() => setAdding(true)}>Добавить магазин</Button>}>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[720px]">
          <thead className="border-b border-line"><tr><th className={th}>Магазин</th><th className={th}>Адрес</th><th className={th}>Код для клиентов</th><th className={`${th} text-right`}>Касс</th><th className={th}>Создан</th><th className="w-10" /></tr></thead>
          <tbody>
            {d.stores.map((s) => (
              <tr key={s.id} className={`border-b border-line last:border-0 ${s.isActive ? '' : 'opacity-50'}`}>
                <td className={td}><span className="block font-semibold">{s.name}</span><span className="block text-[12px] text-ink-3">{s.category}{s.isActive ? '' : ' · закрыт'}</span></td>
                <td className={td}>{s.address ?? '—'}</td>
                <td className={`${td} font-mono`}>{s.joinCode}</td>
                <td className={`${td} text-right`}>{s.registers}</td>
                <td className={td}>{date(s.createdAt)}</td>
                <td className="px-2"><RowMenu items={[{ label: s.isActive ? 'Закрыть магазин' : 'Открыть магазин', icon: <Power size={16} />, danger: s.isActive, onClick: () => void toggle(s.id, !s.isActive, s.name) }]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {adding && <StoreModal id={d.business.id} onClose={() => setAdding(false)} onDone={(next) => { setAdding(false); onSaved(next); toast('Магазин добавлен') }} />}
    </Panel>
  )
}

function StoreModal({ id, onClose, onDone }: { id: string; onClose: () => void; onDone: (next: BusinessDetail) => void }) {
  const [name, setName] = useState('')
  const [category, setCategory] = useState('')
  const [address, setAddress] = useState('')
  const [phone, setPhone] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    try { onDone(await adminApi.addStore(id, { name, category: category || null, address: address || null, phone: phone || null })) } catch (err) { setError(errText(err)) } finally { setBusy(false) }
  }
  return (
    <Modal title="Новый магазин" onClose={onClose} width={480}>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <Field label="Название" required><Input value={name} maxLength={200} autoFocus onChange={(e) => setName(e.target.value)} /></Field>
        <Field label="Категория"><Input value={category} maxLength={100} placeholder="Магазин" onChange={(e) => setCategory(e.target.value)} /></Field>
        <Field label="Адрес"><Input value={address} maxLength={300} onChange={(e) => setAddress(e.target.value)} /></Field>
        <Field label="Телефон"><Input value={phone} maxLength={30} onChange={(e) => setPhone(e.target.value)} /></Field>
        <p className="text-[12px] text-ink-3">Создаётся «Касса №1», владельцы бизнеса получают доступ к магазину.</p>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2"><Button type="button" variant="secondary" onClick={onClose}>Отмена</Button><Button type="submit" loading={busy} disabled={name.trim().length < 2}>Добавить</Button></div>
      </form>
    </Modal>
  )
}

const ACTIONS: Record<string, string> = {
  'business.create': 'Бизнес создан', 'business.update': 'Данные изменены', 'business.modules': 'Модули изменены', 'business.suspend': 'Приостановлен',
  'business.activate': 'Возобновлён', 'business.store.create': 'Добавлен магазин', 'business.store.activate': 'Магазин открыт', 'business.store.deactivate': 'Магазин закрыт',
  'business.account.create': 'Создан доступ', 'business.account.password': 'Сброшен пароль', 'business.account.activate': 'Доступ включён',
  'business.account.deactivate': 'Доступ отключён', 'payment.create': 'Принята оплата', 'payment.void': 'Оплата аннулирована',
}

function summary(action: string, raw: string | null) {
  if (!raw) return null
  try {
    const d = JSON.parse(raw) as Record<string, unknown>
    if (action === 'business.modules') {
      const on = (d.Enabled as string[] | undefined) ?? []
      const off = (d.Disabled as string[] | undefined) ?? []
      return [on.length ? `включены: ${on.map(moduleLabel).join(', ')}` : null, off.length ? `отключены: ${off.map(moduleLabel).join(', ')}` : null].filter(Boolean).join('; ')
    }
    if (action.startsWith('payment.')) return `${tenge(Number(d.Amount ?? 0))} · ${date(String(d.PeriodFrom ?? ''))} — ${date(String(d.PeriodTo ?? ''))}${d.Reason ? ` · ${String(d.Reason)}` : ''}`
    if (action === 'business.suspend') return String(d.Reason ?? '')
    if (d.Phone || d.Name) return [d.Name, d.Phone].filter(Boolean).join(' · ')
    if (d.Name && d.Store) return `${String(d.Name)} · ${String(d.Store)}`
    return null
  } catch { return null }
}

function HistoryTab({ d }: { d: BusinessDetail }) {
  return (
    <Panel title="История действий администраторов">
      {d.history.length === 0 ? <p className="py-8 text-center text-[13px] text-ink-3">Записей нет</p> : (
        <ul className="divide-y divide-line">
          {d.history.map((h) => (
            <li key={h.id} className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5 py-2.5 text-[13px]">
              <span className="w-36 shrink-0 text-ink-3">{dateTime(h.createdAt)}</span>
              <span className="font-semibold">{ACTIONS[h.action] ?? h.action}</span>
              <span className="min-w-0 flex-1 text-ink-2">{summary(h.action, h.data)}</span>
              <span className="text-ink-3">{h.admin}</span>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  )
}

function EditDrawer({ d, onClose, onSaved }: { d: BusinessDetail; onClose: () => void; onSaved: (next: BusinessDetail) => void }) {
  const b = d.business
  const plans = useQuery({ queryKey: ['admin', 'plans'], queryFn: adminApi.plans })
  const [f, setF] = useState({
    name: b.name, bin: b.bin ?? '', city: b.city ?? '', contactName: b.contactName ?? '', contactPhone: b.contactPhone ?? '', contactEmail: d.contactEmail ?? '',
    comment: d.comment ?? '', planId: b.planId ?? '', applyPlanModules: false, customFee: b.customFee, fee: b.customFee ? String(b.fee) : '', trialUntil: b.trialUntil ?? '',
  })
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const patch = (p: Partial<typeof f>) => setF((x) => ({ ...x, ...p }))
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      onSaved(await adminApi.updateBusiness(b.id, {
        name: f.name, bin: f.bin || null, city: f.city || null, contactName: f.contactName || null, contactPhone: f.contactPhone || null, contactEmail: f.contactEmail || null,
        comment: f.comment || null, planId: f.planId || null, applyPlanModules: f.applyPlanModules, customFee: f.customFee,
        monthlyFee: f.customFee ? Number(f.fee.replace(',', '.')) || 0 : null, trialUntil: f.trialUntil || null,
      }))
      toast('Сохранено')
    } catch (err) { setError(err instanceof AdminApiError ? err.message : errText(err)) } finally { setBusy(false) }
  }
  const plan = plans.data?.find((p) => p.id === f.planId)
  return (
    <Drawer title="Редактирование бизнеса" onClose={onClose} width={560}
      footer={<><Button variant="secondary" onClick={onClose}>Отмена</Button><Button type="submit" form="edit-business" loading={busy}>Сохранить</Button></>}>
      <form id="edit-business" onSubmit={submit} className="flex flex-col gap-3">
        <Field label="Название" required><Input value={f.name} maxLength={200} onChange={(e) => patch({ name: e.target.value })} /></Field>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="БИН / ИИН"><Input value={f.bin} maxLength={12} inputMode="numeric" onChange={(e) => patch({ bin: e.target.value.replace(/\D/g, '') })} /></Field>
          <Field label="Город"><Input value={f.city} maxLength={100} onChange={(e) => patch({ city: e.target.value })} /></Field>
          <Field label="Контактное лицо"><Input value={f.contactName} maxLength={120} onChange={(e) => patch({ contactName: e.target.value })} /></Field>
          <Field label="Телефон контакта"><Input value={f.contactPhone} maxLength={20} onChange={(e) => patch({ contactPhone: e.target.value })} /></Field>
        </div>
        <Field label="E-mail"><Input type="email" value={f.contactEmail} maxLength={200} onChange={(e) => patch({ contactEmail: e.target.value })} /></Field>
        <Field label="Комментарий"><Textarea value={f.comment} maxLength={1000} onChange={(e) => patch({ comment: e.target.value })} className="min-h-16" /></Field>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Тариф">
            <Select value={f.planId} onChange={(e) => patch({ planId: e.target.value })}>
              <option value="">Без тарифа</option>
              {(plans.data ?? []).map((p) => <option key={p.id} value={p.id}>{p.name} — {tenge(p.monthlyPrice)}/мес{p.isActive ? '' : ' (архив)'}</option>)}
            </Select>
          </Field>
          <Field label="Пробный период до"><Input type="date" value={f.trialUntil} onChange={(e) => patch({ trialUntil: e.target.value })} /></Field>
        </div>
        {plan && f.planId !== (b.planId ?? '') && <Toggle checked={f.applyPlanModules} onChange={(v) => patch({ applyPlanModules: v })} label={`Подключить модули тарифа: ${plan.modules.map(moduleLabel).join(', ')}`} />}
        <div className="flex flex-wrap items-center gap-3">
          <Toggle checked={f.customFee} onChange={(v) => patch({ customFee: v })} label="Своя цена" />
          {f.customFee && <Input className="w-40!" inputMode="decimal" placeholder="₸ в месяц" value={f.fee} onChange={(e) => patch({ fee: e.target.value })} />}
        </div>
        {error && <p className="text-[13px] text-danger">{error}</p>}
      </form>
    </Drawer>
  )
}

function SuspendModal({ id, name, onClose, onDone }: { id: string; name: string; onClose: () => void; onDone: (next: BusinessDetail) => void }) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    try { onDone(await adminApi.suspend(id, reason.trim())); toast('Бизнес приостановлен') } catch (err) { setError(errText(err)) } finally { setBusy(false) }
  }
  return (
    <Modal title={`Приостановить «${name}»`} onClose={onClose} width={480}>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <p className="text-[14px] text-ink-2">Все сотрудники бизнеса будут разлогинены и не смогут войти, пока доступ не возобновят. Данные сохраняются.</p>
        <Field label="Причина" required><Textarea value={reason} maxLength={500} autoFocus onChange={(e) => setReason(e.target.value)} className="min-h-20" /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2"><Button type="button" variant="secondary" onClick={onClose}>Отмена</Button><Button type="submit" variant="danger" loading={busy} disabled={reason.trim().length < 3}>Приостановить</Button></div>
      </form>
    </Modal>
  )
}
