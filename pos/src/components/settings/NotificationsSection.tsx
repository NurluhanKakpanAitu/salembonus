import { Button } from '../ui/Button'
import { Field, Input, Toggle } from '../ui/Field'
import { BlockError, Skeleton } from '../stats/statsUi'
import { Section } from '../staff/staffUi'
import { useT } from '../../lib/i18n'
import { useCashierSettings } from './PaymentsSection'

/** «Настройка → Уведомления»: кассирге көрсетілетін оқиғалар және «тауар аз» шегі. */
export function NotificationsSection() {
  const t = useT()
  const { q, f, patch, reset, dirty, busy, save } = useCashierSettings()
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  if (!q.data || !f) return <Skeleton className="h-72" />
  return (
    <div className="flex flex-col gap-4">
      <Section title={t('set.notif.events')} hint={t('set.notif.hint')}>
        <div className="flex flex-col gap-3">
          <Toggle checked={f.notifyOutOfStock} onChange={(v) => patch({ notifyOutOfStock: v })} label={t('settings.notifyOutOfStock')} />
          <Toggle checked={f.notifyLowStock} onChange={(v) => patch({ notifyLowStock: v })} label={t('settings.notifyLowStock')} />
          {f.notifyLowStock && (
            <div className="pl-14"><Field label={t('settings.lowStockThreshold')}><Input className="w-28" inputMode="numeric" value={f.lowStockThreshold} onChange={(e) => patch({ lowStockThreshold: Number(e.target.value.replace(/\D/g, '')) || 0 })} /></Field></div>
          )}
          <Toggle checked={f.notifyDebt} onChange={(v) => patch({ notifyDebt: v })} label={t('settings.notifyDebt')} />
          <Toggle checked={f.notifyReturn} onChange={(v) => patch({ notifyReturn: v })} label={t('settings.notifyReturn')} />
          <Toggle checked={f.notifyCashierChange} onChange={(v) => patch({ notifyCashierChange: v })} label={t('settings.notifyCashierChange')} />
        </div>
      </Section>
      <div className="sticky bottom-0 flex justify-end gap-2 rounded-2xl border border-line bg-surface/95 p-3 backdrop-blur">
        <Button variant="secondary" disabled={!dirty} onClick={reset}>{t('set.cancel')}</Button>
        <Button loading={busy} disabled={!dirty} onClick={() => void save(t)}>{t('set.save')}</Button>
      </div>
    </div>
  )
}
