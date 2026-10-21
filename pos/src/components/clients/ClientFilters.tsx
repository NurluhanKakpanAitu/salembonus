import { useState } from 'react'
import { RotateCcw } from 'lucide-react'
import { Button } from '../ui/Button'
import { Drawer } from '../ui/Drawer'
import { Field, Input, Select } from '../ui/Field'
import { SOURCES, QUICK_FILTERS, type Channel, type ClientFilter, type ClientGender, type ClientSource, type QuickFilter } from '../../lib/clientsTypes'
import { useLanguageStore } from '../../lib/language'
import { useT, type TranslationKey } from '../../lib/i18n'
import { KatoPicker } from './KatoPicker'
import { compact } from './clientsUi'

const numOrNull = (v: string) => (v.trim() === '' || Number.isNaN(Number(v.replace(',', '.'))) ? null : Number(v.replace(',', '.')))

function Range({ label, min, max, onChange, type = 'number' }: {
  label: string; min: string | number | null | undefined; max: string | number | null | undefined; onChange: (min: string, max: string) => void; type?: 'number' | 'date'
}) {
  const t = useT()
  return (
    <Field label={label}>
      <div className="grid grid-cols-2 gap-2">
        <Input type={type} inputMode={type === 'number' ? 'decimal' : undefined} placeholder={t('cli.f.from')} value={min ?? ''} onChange={(e) => onChange(e.target.value, String(max ?? ''))} />
        <Input type={type} inputMode={type === 'number' ? 'decimal' : undefined} placeholder={t('cli.f.to')} value={max ?? ''} onChange={(e) => onChange(String(min ?? ''), e.target.value)} />
      </div>
    </Field>
  )
}

/** Кеңейтілген сүзгі шарттары (ТЗ «Клиенты» §4.3) — тізімде де, сегментте де бірдей. */
export function FiltersForm({ value, onChange, statuses, withQuick, withSearch }: {
  value: ClientFilter
  onChange: (f: ClientFilter) => void
  statuses?: { id: string; name: string }[]
  withQuick?: boolean
  withSearch?: boolean
}) {
  const t = useT()
  const lang = useLanguageStore((s) => s.lang)
  const f = value
  const patch = (p: Partial<ClientFilter>) => onChange({ ...f, ...p })
  const months = Array.from({ length: 12 }, (_, i) => new Date(2024, i, 1).toLocaleDateString(lang === 'kk' ? 'kk-KZ' : 'ru-RU', { month: 'long' }))
  const katoPath = !f.kato ? [] : /^\d{2}0{7}$/.test(f.kato) ? [f.kato] : [f.kato.slice(0, 2) + '0000000', f.kato]
  return (
    <div className="flex flex-col gap-4">
      {withSearch && <Field label={t('cli.f.search')}><Input value={f.search ?? ''} onChange={(e) => patch({ search: e.target.value || null })} placeholder={t('cli.searchPlaceholder')} /></Field>}
      {withQuick && (
        <Field label={t('cli.f.quick')}>
          <Select value={f.quick ?? ''} onChange={(e) => patch({ quick: (e.target.value || null) as QuickFilter | null })}>
            <option value="">{t('cli.f.any')}</option>
            {[...QUICK_FILTERS.filter((q) => q !== 'archived'), 'cooling' as QuickFilter].map((q) => <option key={q} value={q}>{t(`cli.quick.${q}` as TranslationKey)}</option>)}
          </Select>
        </Field>
      )}
      {statuses && statuses.length > 0 && (
        <Field label={t('cli.f.status')}>
          <div className="flex flex-wrap gap-2">
            {statuses.map((s) => {
              const on = f.statusIds?.includes(s.id) ?? false
              return (
                <button key={s.id} type="button" onClick={() => patch({ statusIds: on ? (f.statusIds ?? []).filter((x) => x !== s.id) : [...(f.statusIds ?? []), s.id] })}
                  className={`rounded-full border px-3 py-1 text-[13px] font-medium ${on ? 'border-brand bg-brand-soft text-brand' : 'border-line text-ink-2 hover:border-brand/50'}`}>{s.name}</button>
              )
            })}
          </div>
        </Field>
      )}
      <Range label={t('cli.f.registered')} type="date" min={f.registeredFrom} max={f.registeredTo} onChange={(a, b) => patch({ registeredFrom: a || null, registeredTo: b || null })} />
      <Range label={t('cli.f.lastPurchase')} type="date" min={f.lastPurchaseFrom} max={f.lastPurchaseTo} onChange={(a, b) => patch({ lastPurchaseFrom: a || null, lastPurchaseTo: b || null })} />
      <Range label={t('cli.f.purchases')} min={f.minPurchases} max={f.maxPurchases} onChange={(a, b) => patch({ minPurchases: numOrNull(a), maxPurchases: numOrNull(b) })} />
      <Range label={t('cli.f.amount')} min={f.minAmount} max={f.maxAmount} onChange={(a, b) => patch({ minAmount: numOrNull(a), maxAmount: numOrNull(b) })} />
      <Range label={t('cli.f.average')} min={f.minAverage} max={f.maxAverage} onChange={(a, b) => patch({ minAverage: numOrNull(a), maxAverage: numOrNull(b) })} />
      <Range label={t('cli.f.bonus')} min={f.minBonus} max={f.maxBonus} onChange={(a, b) => patch({ minBonus: numOrNull(a), maxBonus: numOrNull(b) })} />
      <div className="grid grid-cols-2 gap-3">
        <Field label={t('cli.f.debt')}>
          <Select value={f.hasDebt === true ? 'yes' : f.hasDebt === false ? 'no' : ''} onChange={(e) => patch({ hasDebt: e.target.value === 'yes' ? true : e.target.value === 'no' ? false : null })}>
            <option value="">{t('cli.f.any')}</option><option value="yes">{t('cli.f.yes')}</option><option value="no">{t('cli.f.no')}</option>
          </Select>
        </Field>
        <Field label={t('cli.f.overdue')}>
          <Select value={f.overdue === true ? 'yes' : f.overdue === false ? 'no' : ''} onChange={(e) => patch({ overdue: e.target.value === 'yes' ? true : e.target.value === 'no' ? false : null })}>
            <option value="">{t('cli.f.any')}</option><option value="yes">{t('cli.f.yes')}</option><option value="no">{t('cli.f.no')}</option>
          </Select>
        </Field>
        <Field label={t('cli.f.gender')}>
          <Select value={f.gender ?? ''} onChange={(e) => patch({ gender: (e.target.value || null) as ClientGender | null })}>
            <option value="">{t('cli.f.any')}</option>
            {(['Female', 'Male', 'Unknown'] as ClientGender[]).map((g) => <option key={g} value={g}>{t(`cli.gender.${g}` as TranslationKey)}</option>)}
          </Select>
        </Field>
        <Field label={t('cli.f.birthMonth')}>
          <Select value={f.birthMonth ?? ''} onChange={(e) => patch({ birthMonth: e.target.value ? Number(e.target.value) : null })}>
            <option value="">{t('cli.f.any')}</option>
            {months.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
          </Select>
        </Field>
        <Field label={t('cli.f.source')}>
          <Select value={f.source ?? ''} onChange={(e) => patch({ source: (e.target.value || null) as ClientSource | null })}>
            <option value="">{t('cli.f.any')}</option>
            {SOURCES.map((s) => <option key={s} value={s}>{t(`cli.source.${s}` as TranslationKey)}</option>)}
          </Select>
        </Field>
        <Field label={t('cli.f.consent')}>
          <Select value={f.consent ?? ''} onChange={(e) => patch({ consent: (e.target.value || null) as Channel | null })}>
            <option value="">{t('cli.f.any')}</option>
            {(['app', 'sms', 'whatsapp', 'email'] as Channel[]).map((c) => <option key={c} value={c}>{t(`cli.channel.${c}` as TranslationKey)}</option>)}
          </Select>
        </Field>
      </div>
      <Field label={t('cli.f.region')}>
        <KatoPicker levels={2} path={katoPath} onChange={(p) => patch({ kato: p[p.length - 1] ?? null })} />
      </Field>
    </div>
  )
}

export function FiltersDrawer({ value, statuses, onApply, onClose }: {
  value: ClientFilter; statuses?: { id: string; name: string }[]; onApply: (f: ClientFilter) => void; onClose: () => void
}) {
  const t = useT()
  const [draft, setDraft] = useState<ClientFilter>(value)
  return (
    <Drawer title={t('cli.filters')} onClose={onClose} width={480}
      footer={<>
        <Button variant="ghost" icon={<RotateCcw size={16} />} onClick={() => setDraft({})}>{t('cli.f.reset')}</Button>
        <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
        <Button onClick={() => onApply(compact(draft))}>{t('cli.f.apply')}</Button>
      </>}>
      <FiltersForm value={draft} onChange={setDraft} statuses={statuses} />
    </Drawer>
  )
}
