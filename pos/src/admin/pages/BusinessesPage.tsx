import { useMemo, useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { Drawer } from '../../components/ui/Drawer'
import { Field, Input, Select, Textarea, Toggle } from '../../components/ui/Field'
import { SearchInput } from '../../components/ui/SearchInput'
import { EmptyRow, td, th, useDebounced } from '../../components/inventory/inventoryUi'
import { BlockError } from '../../components/stats/statsUi'
import { num, tenge } from '../../lib/money'
import { AdminApiError, adminApi, type CreateBusiness, type ModuleKey, type Plan } from '../adminApi'
import { MODULES, ModuleChips, PageHeader, STATUS, STATUS_ORDER, SecretModal, StatusPill, date, errText } from '../ui'

/** «Бизнесы»: күй бойынша қойындылар, іздеу (атауы, БСН, иесі, телефон), тариф сүзгісі; жол — бизнес карточкасы. */
export function BusinessesPage() {
  const nav = useNavigate()
  const [params, setParams] = useSearchParams()
  const status = params.get('status') ?? 'all'
  const planId = params.get('plan') ?? ''
  const [search, setSearch] = useState(params.get('q') ?? '')
  const q = useDebounced(search, 300)
  const [creating, setCreating] = useState(false)
  const set = (k: string, v: string | null) => setParams((p) => { const n = new URLSearchParams(p); if (v) n.set(k, v); else n.delete(k); return n }, { replace: true })
  const list = useQuery({
    queryKey: ['admin', 'businesses', status, planId, q], placeholderData: keepPreviousData,
    queryFn: () => adminApi.businesses({ status, planId: planId || undefined, search: q || undefined, pageSize: 200 }),
  })
  const plans = useQuery({ queryKey: ['admin', 'plans'], queryFn: adminApi.plans })
  if (list.isError) return <BlockError message={(list.error as Error).message} onRetry={() => void list.refetch()} />
  const counts = list.data?.counts ?? {}
  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Бизнесы" subtitle="Клиенты SalemPos: оплата, модули, магазины и доступы"
        actions={<Button icon={<Plus size={17} />} onClick={() => setCreating(true)}>Добавить бизнес</Button>} />
      <div className="flex flex-wrap gap-1.5">
        {['all', ...STATUS_ORDER].map((s) => (
          <button key={s} type="button" onClick={() => set('status', s === 'all' ? null : s)}
            className={`rounded-full border px-3 py-1.5 text-[13px] font-semibold ${status === s ? 'border-brand bg-brand text-white' : 'border-line bg-surface text-ink-2 hover:border-brand/50'}`}>
            {s === 'all' ? 'Все' : STATUS[s as keyof typeof STATUS].label} <span className="opacity-70">{counts[s] ?? 0}</span>
          </button>
        ))}
      </div>
      <div className="flex flex-wrap gap-2">
        <SearchInput value={search} onChange={setSearch} placeholder="Название, БИН, владелец, телефон" className="min-w-64 flex-1" />
        <Select className="h-11! w-56!" value={planId} onChange={(e) => set('plan', e.target.value || null)} aria-label="Тариф">
          <option value="">Все тарифы</option>
          {(plans.data ?? []).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
        </Select>
      </div>
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[1000px]">
          <thead className="border-b border-line bg-field/60"><tr>
            <th className={th}>Бизнес</th><th className={th}>Тариф</th><th className={th}>Статус</th><th className={th}>Оплачено до</th><th className={th}>Модули</th>
            <th className={`${th} text-right`}>Магазины</th><th className={`${th} text-right`}>Продажи 30 дн.</th><th className={th}>Последняя продажа</th>
          </tr></thead>
          <tbody>
            {!list.data && <EmptyRow colSpan={8} text="Загрузка…" />}
            {list.data?.rows.length === 0 && <EmptyRow colSpan={8} text="Ничего не найдено" />}
            {list.data?.rows.map((b) => (
              <tr key={b.id} className="cursor-pointer border-b border-line last:border-0 hover:bg-field/60" onClick={() => nav(`/businesses/${b.id}`)}>
                <td className={td}>
                  <span className="block font-semibold">{b.name}</span>
                  <span className="block text-[12px] text-ink-3">{[b.city, b.ownerName, b.ownerPhone].filter(Boolean).join(' · ') || '—'}</span>
                </td>
                <td className={td}><span className="block">{b.planName ?? '—'}</span><span className="block text-[12px] text-ink-3">{tenge(b.fee)}/мес{b.customFee ? ' · своя цена' : ''}</span></td>
                <td className={td}><StatusPill status={b.status} />{b.daysOverdue ? <span className="ml-1 text-[12px] text-danger">{b.daysOverdue} дн.</span> : null}</td>
                <td className={td}>{b.status === 'trial' ? `пробный до ${date(b.trialUntil)}` : date(b.paidUntil)}</td>
                <td className={td}><ModuleChips modules={b.modules} max={4} /></td>
                <td className={`${td} text-right`}>{b.stores}</td>
                <td className={`${td} text-right`}><span className="block">{tenge(b.revenue30)}</span><span className="block text-[12px] text-ink-3">{num(b.sales30)} чеков</span></td>
                <td className={td}>{date(b.lastSaleAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {creating && <CreateBusinessDrawer plans={plans.data ?? []} onClose={() => setCreating(false)} onCreated={(id) => { setCreating(false); nav(`/businesses/${id}`) }} />}
    </div>
  )
}

const TIME_ZONES = ['Asia/Almaty', 'Asia/Qostanay', 'Asia/Qyzylorda', 'Asia/Aqtobe', 'Asia/Aqtau', 'Asia/Atyrau', 'Asia/Oral']

/** «Добавить бизнес»: бизнес, бірінші дүкен, иесінің аккаунты, тариф, сынақ мерзімі, модульдер. Құпиясөз — бір рет. */
function CreateBusinessDrawer({ plans, onClose, onCreated }: { plans: Plan[]; onClose: () => void; onCreated: (id: string) => void }) {
  const qc = useQueryClient()
  const activePlans = useMemo(() => plans.filter((p) => p.isActive), [plans])
  const [f, setF] = useState({
    name: '', bin: '', city: '', contactName: '', contactPhone: '', contactEmail: '', comment: '',
    storeName: '', category: '', address: '', storePhone: '', timeZone: 'Asia/Almaty',
    ownerPhone: '+7', ownerFirst: '', ownerLast: '',
    planId: activePlans[0]?.id ?? '', customFee: false, fee: '', trialDays: '14', modules: (activePlans[0]?.modules ?? MODULES.map((m) => m.key)) as ModuleKey[],
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<{ id: string; name: string; phone: string; password: string } | null>(null)
  const patch = (p: Partial<typeof f>) => setF((x) => ({ ...x, ...p }))
  const plan = plans.find((p) => p.id === f.planId)
  const choosePlan = (id: string) => { const p = plans.find((x) => x.id === id); patch({ planId: id, modules: p ? p.modules : f.modules }) }
  const toggle = (k: ModuleKey) => patch({ modules: f.modules.includes(k) ? f.modules.filter((x) => x !== k) : [...f.modules, k] })

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    const err: Record<string, string> = {}
    if (f.name.trim().length < 2) err.name = 'Укажите название'
    if (f.ownerPhone.replace(/\D/g, '').length < 11) err['owner.phone'] = 'Телефон владельца в формате +7XXXXXXXXXX'
    if (!f.ownerFirst.trim()) err['owner.firstName'] = 'Укажите имя'
    setErrors(err)
    if (Object.keys(err).length) return
    const body: CreateBusiness = {
      name: f.name.trim(), bin: f.bin.trim() || null, city: f.city.trim() || null, contactName: f.contactName.trim() || null, contactPhone: f.contactPhone.trim() || null,
      contactEmail: f.contactEmail.trim() || null, comment: f.comment.trim() || null, planId: f.planId || null,
      monthlyFee: f.customFee ? Number(f.fee.replace(',', '.')) || 0 : null, trialDays: Number(f.trialDays), modules: f.modules,
      store: { name: f.storeName.trim() || null, category: f.category.trim() || null, address: f.address.trim() || null, phone: f.storePhone.trim() || null, timeZone: f.timeZone },
      owner: { phone: f.ownerPhone.trim(), firstName: f.ownerFirst.trim(), lastName: f.ownerLast.trim() || null },
    }
    setBusy(true)
    try {
      const r = await adminApi.createBusiness(body)
      void qc.invalidateQueries({ queryKey: ['admin'] })
      setResult({ id: r.business.business.id, name: r.business.business.name, phone: r.ownerPhone, password: r.tempPassword })
    } catch (ex) {
      if (ex instanceof AdminApiError && ex.field) setErrors({ [ex.field]: ex.message })
      else setErrors({ form: errText(ex) })
    } finally { setBusy(false) }
  }

  if (result) return (
    <SecretModal title={`Бизнес «${result.name}» создан`} onClose={() => onCreated(result.id)}
      lines={[{ label: 'Вход', value: 'https://pos.salemtech.kz' }, { label: 'Телефон', value: result.phone }, { label: 'Пароль', value: result.password, secret: true }]}
      note="Пароль владельца показывается один раз. Передайте его лично — после первого входа владелец меняет пароль в профиле и задаёт PIN." />
  )

  return (
    <Drawer title="Новый бизнес" subtitle="Организация, первый магазин с кассой, аккаунт владельца, тариф и модули" onClose={onClose} width={620}
      footer={<><Button variant="secondary" onClick={onClose}>Отмена</Button><Button type="submit" form="create-business" loading={busy}>Создать бизнес</Button></>}>
      <form id="create-business" onSubmit={submit} className="flex flex-col gap-5">
        <Section title="Бизнес">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Field label="Название" required error={errors.name}><Input value={f.name} autoFocus maxLength={200} error={!!errors.name} onChange={(e) => patch({ name: e.target.value })} /></Field>
            <Field label="БИН / ИИН" error={errors.bin}><Input value={f.bin} inputMode="numeric" maxLength={12} error={!!errors.bin} onChange={(e) => patch({ bin: e.target.value.replace(/\D/g, '') })} /></Field>
            <Field label="Город"><Input value={f.city} maxLength={100} onChange={(e) => patch({ city: e.target.value })} /></Field>
            <Field label="Контактное лицо"><Input value={f.contactName} maxLength={120} onChange={(e) => patch({ contactName: e.target.value })} /></Field>
            <Field label="Телефон контакта"><Input value={f.contactPhone} maxLength={20} onChange={(e) => patch({ contactPhone: e.target.value })} /></Field>
            <Field label="E-mail" error={errors.email}><Input type="email" value={f.contactEmail} maxLength={200} error={!!errors.email} onChange={(e) => patch({ contactEmail: e.target.value })} /></Field>
          </div>
          <Field label="Комментарий"><Textarea value={f.comment} maxLength={1000} onChange={(e) => patch({ comment: e.target.value })} className="min-h-16" /></Field>
        </Section>
        <Section title="Первый магазин">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Field label="Название магазина" hint="Пусто — как у бизнеса" error={errors['store.name']}><Input value={f.storeName} maxLength={200} placeholder={f.name} onChange={(e) => patch({ storeName: e.target.value })} /></Field>
            <Field label="Категория"><Input value={f.category} maxLength={100} placeholder="Магазин" onChange={(e) => patch({ category: e.target.value })} /></Field>
            <Field label="Адрес"><Input value={f.address} maxLength={300} onChange={(e) => patch({ address: e.target.value })} /></Field>
            <Field label="Телефон магазина"><Input value={f.storePhone} maxLength={30} onChange={(e) => patch({ storePhone: e.target.value })} /></Field>
            <Field label="Часовой пояс" error={errors['store.timeZone']}>
              <Select value={f.timeZone} onChange={(e) => patch({ timeZone: e.target.value })}>{TIME_ZONES.map((z) => <option key={z} value={z}>{z}</option>)}</Select>
            </Field>
          </div>
        </Section>
        <Section title="Владелец (вход в SalemPos)">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <Field label="Телефон" required error={errors['owner.phone']}><Input type="tel" value={f.ownerPhone} error={!!errors['owner.phone']} onChange={(e) => patch({ ownerPhone: e.target.value })} /></Field>
            <Field label="Имя" required error={errors['owner.firstName']}><Input value={f.ownerFirst} maxLength={60} error={!!errors['owner.firstName']} onChange={(e) => patch({ ownerFirst: e.target.value })} /></Field>
            <Field label="Фамилия"><Input value={f.ownerLast} maxLength={60} onChange={(e) => patch({ ownerLast: e.target.value })} /></Field>
          </div>
        </Section>
        <Section title="Тариф и модули">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Field label="Тариф" error={errors.planId}>
              <Select value={f.planId} onChange={(e) => choosePlan(e.target.value)}>
                <option value="">Без тарифа</option>
                {activePlans.map((p) => <option key={p.id} value={p.id}>{p.name} — {tenge(p.monthlyPrice)}/мес</option>)}
              </Select>
            </Field>
            <Field label="Пробный период" error={errors.trialDays}>
              <Select value={f.trialDays} onChange={(e) => patch({ trialDays: e.target.value })}>
                {['0', '7', '14', '30'].map((d) => <option key={d} value={d}>{d === '0' ? 'Без пробного' : `${d} дней`}</option>)}
              </Select>
            </Field>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <Toggle checked={f.customFee} onChange={(v) => patch({ customFee: v })} label="Своя цена" />
            {f.customFee ? (
              <Input className="w-40!" inputMode="decimal" placeholder="₸ в месяц" value={f.fee} onChange={(e) => patch({ fee: e.target.value })} />
            ) : <span className="text-[13px] text-ink-2">{plan ? `${tenge(plan.monthlyPrice)} в месяц по тарифу` : 'Плата не задана'}</span>}
          </div>
          {errors.monthlyFee && <p className="text-[13px] text-danger">{errors.monthlyFee}</p>}
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            {MODULES.map((m) => (
              <label key={m.key} className={`flex cursor-pointer items-start gap-2.5 rounded-xl border p-2.5 ${f.modules.includes(m.key) ? 'border-brand bg-brand-soft/40' : 'border-line'}`}>
                <input type="checkbox" className="mt-1 accent-brand" checked={f.modules.includes(m.key)} onChange={() => toggle(m.key)} />
                <span className="min-w-0"><span className="flex items-center gap-1.5 text-[14px] font-semibold">{m.icon}{m.label}</span><span className="block text-[12px] text-ink-3">{m.hint}</span></span>
              </label>
            ))}
          </div>
          <p className="text-[12px] text-ink-3">Сотрудники и Настройки доступны всегда. Модули можно менять позже в карточке бизнеса.</p>
        </Section>
        {errors.form && <p className="rounded-xl bg-danger-soft px-3 py-2 text-[13px] text-danger">{errors.form}</p>}
      </form>
    </Drawer>
  )
}

export function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return <section className="flex flex-col gap-3"><h3 className="text-[14px] font-bold text-[#0b1f44]">{title}</h3>{children}</section>
}
