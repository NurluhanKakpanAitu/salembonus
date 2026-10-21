import { useQuery } from '@tanstack/react-query'
import { BadgeCheck, CalendarClock, CircleDollarSign, ExternalLink, Package } from 'lucide-react'
import { BlockError, Skeleton } from '../stats/statsUi'
import { Section } from '../staff/staffUi'
import { useT, type TranslationKey } from '../../lib/i18n'
import { tenge } from '../../lib/money'
import { settingsApi } from '../../lib/staffAdminApi'

const TONES: Record<string, string> = {
  trial: 'bg-violet-50 text-violet-700', active: 'bg-success-soft text-success', dueSoon: 'bg-amber-50 text-amber-700',
  overdue: 'bg-danger-soft text-danger', unpaid: 'bg-danger-soft text-danger', suspended: 'bg-field text-ink-2',
}

/** «Настройка → Тариф и модули»: тариф, төлем күйі мен мерзімі, қосылған модульдер (тек оқуға — өзгерту SalemTech арқылы). */
export function PlanSection() {
  const t = useT()
  const q = useQuery({ queryKey: ['settings', 'plan'], queryFn: settingsApi.plan })
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  if (!q.data) return <Skeleton className="h-64" />
  const p = q.data
  const date = (iso: string) => new Date(iso).toLocaleDateString('ru-RU')
  return (
    <div className="flex flex-col gap-4">
      <div className="grid gap-3 md:grid-cols-3">
        <div className="rounded-2xl border border-line bg-surface p-4">
          <div className="flex items-center gap-2 text-[13px] font-medium text-ink-2"><BadgeCheck size={16} className="text-brand" />{t('set.plan.plan')}</div>
          <div className="mt-2 text-[22px] font-extrabold">{p.planName ?? t('set.plan.noPlan')}</div>
        </div>
        <div className="rounded-2xl border border-line bg-surface p-4">
          <div className="flex items-center gap-2 text-[13px] font-medium text-ink-2"><CircleDollarSign size={16} className="text-brand" />{t('set.plan.fee')}</div>
          <div className="mt-2 text-[22px] font-extrabold">{tenge(p.monthlyFee)}</div>
        </div>
        <div className="rounded-2xl border border-line bg-surface p-4">
          <div className="flex items-center gap-2 text-[13px] font-medium text-ink-2"><CalendarClock size={16} className="text-brand" />{t('set.plan.status')}</div>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <span className={`rounded-full px-3 py-1 text-[14px] font-bold ${TONES[p.status]}`}>{t(`set.plan.status.${p.status}` as TranslationKey)}</span>
            {p.daysLeft != null && <span className="text-[13px] text-ink-2">{t('set.plan.daysLeft', { n: p.daysLeft })}</span>}
            {p.daysOverdue != null && <span className="text-[13px] text-danger">{t('set.plan.daysOverdue', { n: p.daysOverdue })}</span>}
          </div>
          <div className="mt-1 text-[12px] text-ink-3">
            {p.paidUntil ? `${t('set.plan.paidUntil')}: ${date(p.paidUntil)}` : p.trialUntil ? `${t('set.plan.trialUntil')}: ${date(p.trialUntil)}` : ''}
          </div>
        </div>
      </div>

      <Section title={t('set.plan.modules')} hint={t('set.plan.modulesHint')}
        action={<a href={p.supportUrl} target="_blank" rel="noopener" className="inline-flex items-center gap-1.5 text-[13px] font-semibold text-brand hover:underline">{t('set.plan.contact')}<ExternalLink size={14} /></a>}>
        <ul className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
          {p.modules.map((m) => (
            <li key={m.key} className={`flex items-center gap-3 rounded-xl border p-3 ${m.enabled ? 'border-line' : 'border-dashed border-line opacity-60'}`}>
              <span className={`flex size-9 shrink-0 items-center justify-center rounded-xl ${m.enabled ? 'bg-brand-soft text-brand' : 'bg-field text-ink-3'}`}><Package size={16} /></span>
              <span className="min-w-0"><span className="block font-semibold">{t(`set.plan.module.${m.key}` as TranslationKey)}</span><span className={`block text-[12px] ${m.enabled ? 'text-success' : 'text-ink-3'}`}>{m.enabled ? t('set.plan.on') : t('set.plan.off')}</span></span>
            </li>
          ))}
        </ul>
      </Section>
    </div>
  )
}
