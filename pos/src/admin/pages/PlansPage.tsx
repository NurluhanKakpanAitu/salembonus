import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, Tags } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { Drawer } from '../../components/ui/Drawer'
import { Field, Input, Textarea, Toggle } from '../../components/ui/Field'
import { toast } from '../../components/ui/Toast'
import { BlockError, Skeleton } from '../../components/stats/statsUi'
import { tenge } from '../../lib/money'
import { adminApi, type ModuleKey, type Plan } from '../adminApi'
import { MODULES, ModuleChips, PageHeader, errText } from '../ui'

/** «Тарифы»: айлық бағасы және әдепкі модульдері. Тарифті өзгерту бизнестердің модульдерін өзгертпейді — олар карточкада бөлек. */
export function PlansPage() {
  const q = useQuery({ queryKey: ['admin', 'plans'], queryFn: adminApi.plans })
  const [editing, setEditing] = useState<Plan | 'new' | null>(null)
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Тарифы" subtitle="Цена в месяц и модули по умолчанию для новых бизнесов"
        actions={<Button icon={<Plus size={17} />} onClick={() => setEditing('new')}>Новый тариф</Button>} />
      {!q.data ? <Skeleton className="h-48" /> : q.data.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-line bg-surface py-14 text-center text-[13px] text-ink-3"><Tags className="mx-auto mb-2" size={28} />Тарифов пока нет</div>
      ) : (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
          {q.data.map((p) => (
            <div key={p.id} className={`flex flex-col gap-3 rounded-2xl border bg-surface p-4 ${p.isActive ? 'border-line' : 'border-dashed border-line opacity-60'}`}>
              <div className="flex items-start gap-2">
                <div className="min-w-0 flex-1">
                  <div className="text-[17px] font-bold">{p.name}{!p.isActive && <span className="ml-2 text-[12px] font-normal text-ink-3">архив</span>}</div>
                  <div className="text-[22px] font-extrabold text-brand">{tenge(p.monthlyPrice)}<span className="text-[13px] font-medium text-ink-3"> / мес</span></div>
                </div>
                <Button variant="ghost" className="h-9! px-2.5!" icon={<Pencil size={16} />} onClick={() => setEditing(p)} aria-label="Изменить" />
              </div>
              {p.description && <p className="text-[13px] text-ink-2">{p.description}</p>}
              <ModuleChips modules={p.modules} />
              <div className="mt-auto text-[12px] text-ink-3">Бизнесов на тарифе: {p.businesses}</div>
            </div>
          ))}
        </div>
      )}
      {editing && <PlanDrawer plan={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); void q.refetch() }} />}
    </div>
  )
}

function PlanDrawer({ plan, onClose, onSaved }: { plan: Plan | null; onClose: () => void; onSaved: () => void }) {
  const qc = useQueryClient()
  const [name, setName] = useState(plan?.name ?? '')
  const [price, setPrice] = useState(plan ? String(plan.monthlyPrice) : '')
  const [description, setDescription] = useState(plan?.description ?? '')
  const [active, setActive] = useState(plan?.isActive ?? true)
  const [modules, setModules] = useState<ModuleKey[]>(plan?.modules ?? MODULES.map((m) => m.key))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const submit = async () => {
    setBusy(true)
    setError(null)
    try {
      const body = { name, monthlyPrice: Number(price.replace(',', '.')) || 0, description: description || null, isActive: active, modules }
      if (plan) await adminApi.updatePlan(plan.id, body); else await adminApi.createPlan(body)
      toast('Тариф сохранён')
      void qc.invalidateQueries({ queryKey: ['admin'] })
      onSaved()
    } catch (err) { setError(errText(err)) } finally { setBusy(false) }
  }
  return (
    <Drawer title={plan ? 'Изменить тариф' : 'Новый тариф'} onClose={onClose} width={480}
      footer={<><Button variant="secondary" onClick={onClose}>Отмена</Button><Button loading={busy} disabled={name.trim().length < 2} onClick={() => void submit()}>Сохранить</Button></>}>
      <div className="flex flex-col gap-4">
        <Field label="Название" required><Input value={name} maxLength={120} autoFocus onChange={(e) => setName(e.target.value)} /></Field>
        <Field label="Цена в месяц, ₸" required><Input inputMode="decimal" value={price} onChange={(e) => setPrice(e.target.value)} /></Field>
        <Field label="Описание"><Textarea value={description} maxLength={500} onChange={(e) => setDescription(e.target.value)} className="min-h-16" /></Field>
        <Toggle checked={active} onChange={setActive} label="Доступен для новых бизнесов" />
        <div className="flex flex-col gap-2">
          <div className="text-[13px] font-medium text-ink-2">Модули по умолчанию</div>
          {MODULES.map((m) => (
            <label key={m.key} className="flex items-center gap-2.5 text-[14px]">
              <input type="checkbox" className="accent-brand" checked={modules.includes(m.key)}
                onChange={(e) => setModules(e.target.checked ? MODULES.filter((x) => x.key === m.key || modules.includes(x.key)).map((x) => x.key) : modules.filter((x) => x !== m.key))} />
              <span className="text-ink-2">{m.icon}</span>{m.label}
            </label>
          ))}
          <p className="text-[12px] text-ink-3">Изменение тарифа не меняет модули уже подключённых бизнесов. Бизнесы без своей цены будут платить новую цену со следующей оплаты.</p>
        </div>
        {error && <p className="text-[13px] text-danger">{error}</p>}
      </div>
    </Drawer>
  )
}
