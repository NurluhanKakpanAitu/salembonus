import type { Customer } from '../../lib/api'
import { useKatoChildren, useKatoSettlements } from '../../lib/queries'
import { useT } from '../../lib/i18n'

export interface LocationValue {
  region: string | null
  district: string | null
  settlement: string | null
}

/** Профильдегі сақталған жолдан бастапқы мәнді жинайды. */
export function locationFrom(me: Customer): LocationValue {
  const path = me.katoPath
  const district = path[1]?.code ?? null
  const deepest = me.katoCode ?? null
  return {
    region: path[0]?.code ?? null,
    district,
    settlement: deepest && deepest !== district && deepest !== path[0]?.code ? deepest : null,
  }
}

/** Ең төменгі таңдалған код — серверге сол сақталады. */
export const locationCode = (v: LocationValue) => v.settlement ?? v.district ?? v.region ?? null

const selectCls =
  'mt-2 h-13 w-full rounded-2xl border border-line bg-surface px-4 text-base outline-none focus:border-brand'

/**
 * Облыс → аудан/қала → елді мекен. Тізім ҚР ресми КАТО жіктеуішінен алынады,
 * ауылдық округ сияқты әкімшілік бірліктер көрсетілмейді.
 */
export function LocationPicker({ value, onChange }: { value: LocationValue; onChange: (v: LocationValue) => void }) {
  const t = useT()
  const regions = useKatoChildren(null)
  const districts = useKatoChildren(value.region, !!value.region)
  const settlements = useKatoSettlements(value.district)

  return (
    <>
      <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="profile-region">{t('profile.region')}</label>
      <select
        id="profile-region"
        value={value.region ?? ''}
        onChange={(e) => onChange({ region: e.target.value || null, district: null, settlement: null })}
        className={selectCls}
      >
        <option value="">{t('profile.choose')}</option>
        {regions.data?.map((r) => (
          <option key={r.code} value={r.code}>{r.name}</option>
        ))}
      </select>

      {value.region && (districts.data?.length ?? 0) > 0 && (
        <>
          <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="profile-district">{t('profile.district')}</label>
          <select
            id="profile-district"
            value={value.district ?? ''}
            onChange={(e) => onChange({ ...value, district: e.target.value || null, settlement: null })}
            className={selectCls}
          >
            <option value="">{t('profile.choose')}</option>
            {districts.data?.map((d) => (
              <option key={d.code} value={d.code}>{d.name}</option>
            ))}
          </select>
        </>
      )}

      {value.district && (settlements.data?.length ?? 0) > 0 && (
        <>
          <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="profile-settlement">{t('profile.settlement')}</label>
          <select
            id="profile-settlement"
            value={value.settlement ?? ''}
            onChange={(e) => onChange({ ...value, settlement: e.target.value || null })}
            className={selectCls}
          >
            <option value="">{t('profile.choose')}</option>
            {settlements.data?.map((s) => (
              <option key={s.code} value={s.code}>{s.name}</option>
            ))}
          </select>
        </>
      )}
    </>
  )
}
