import { useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api'
import { useT } from '../../lib/i18n'
import { Select } from '../ui/Field'

interface KatoNode { code: string; name: string; level: number; hasChildren: boolean }

const children = (parent: string | null) => api<KatoNode[]>(`/kato/children${parent ? `?parent=${encodeURIComponent(parent)}` : ''}`)

/** КАТО бойынша таңдалған жол: облыс → аудан/қала → елді мекен (ең төменгі таңдалғаны — мән). */
export function KatoPicker({ path, onChange, levels = 3 }: { path: string[]; onChange: (path: string[]) => void; levels?: 1 | 2 | 3 }) {
  const t = useT()
  const [region, district, settlement] = path
  const regions = useQuery({ queryKey: ['kato', 'children', null], queryFn: () => children(null), staleTime: 3600_000 })
  const districts = useQuery({ queryKey: ['kato', 'children', region], queryFn: () => children(region!), enabled: !!region && levels > 1, staleTime: 3600_000 })
  const settlements = useQuery({ queryKey: ['kato', 'children', district], queryFn: () => children(district!), enabled: !!district && levels > 2, staleTime: 3600_000 })
  const cls = `grid grid-cols-1 gap-2 ${levels === 3 ? 'sm:grid-cols-3' : levels === 2 ? 'sm:grid-cols-2' : ''}`
  return (
    <div className={cls}>
      <Select value={region ?? ''} onChange={(e) => onChange(e.target.value ? [e.target.value] : [])} aria-label={t('cli.kato.region')}>
        <option value="">{t('cli.kato.region')}</option>
        {(regions.data ?? []).map((n) => <option key={n.code} value={n.code}>{n.name}</option>)}
      </Select>
      {levels > 1 && (
        <Select value={district ?? ''} disabled={!region || (districts.data?.length ?? 0) === 0} onChange={(e) => onChange(e.target.value ? [region!, e.target.value] : [region!])} aria-label={t('cli.kato.district')}>
          <option value="">{t('cli.kato.district')}</option>
          {(districts.data ?? []).map((n) => <option key={n.code} value={n.code}>{n.name}</option>)}
        </Select>
      )}
      {levels > 2 && (
        <Select value={settlement ?? ''} disabled={!district || (settlements.data?.length ?? 0) === 0} onChange={(e) => onChange(e.target.value ? [region!, district!, e.target.value] : [region!, district!])} aria-label={t('cli.kato.settlement')}>
          <option value="">{t('cli.kato.settlement')}</option>
          {(settlements.data ?? []).map((n) => <option key={n.code} value={n.code}>{n.name}</option>)}
        </Select>
      )}
    </div>
  )
}
