import { useEffect, useState } from 'react'
import { Loader2, Monitor, Unplug } from 'lucide-react'
import { PosScreen } from '../components/cashier/PosScreen'
import { Button } from '../components/ui/Button'
import { ApiError } from '../lib/api'
import { activeStore, can, useAuth } from '../lib/auth'
import { useT } from '../lib/i18n'
import { setRegister, useRegister } from '../lib/register'
import { registerApi } from '../lib/registerApi'
import { Permission, type Register } from '../lib/types'

/**
 * Касса бөлімі. Құрылғы кассаға қосылған болса — касса экраны; әйтпесе — қосу (ТЗ «Касса» §15, §18).
 * Сатылым тек тіркелген кассадан: сервер қай кассадан сатылғанын cookie арқылы біледі.
 */
export function CashierPage() {
  const t = useT()
  const store = useAuth(activeStore)
  const register = useRegister((s) => s.register)
  const canManage = can(store, Permission.SettingsManage)
  const [list, setList] = useState<Register[] | null>(null)
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const onThisStore = register && register.storeId === store?.id

  useEffect(() => {
    if (!onThisStore && canManage) registerApi.list().then(setList).catch(() => setList([]))
  }, [onThisStore, canManage, store?.id])

  const activate = async (id: string) => {
    setBusy(id)
    setError(null)
    try {
      setRegister(await registerApi.activate(id))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('login.failed'))
    } finally {
      setBusy(null)
    }
  }

  const deactivate = async () => {
    if (!window.confirm(t('register.disconnectConfirm'))) return
    setBusy('off')
    try {
      await registerApi.deactivate()
      setRegister(null)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('login.failed'))
    } finally {
      setBusy(null)
    }
  }

  if (onThisStore)
    return <PosScreen menu={canManage ? [{ label: t('register.disconnect'), icon: <Unplug size={16} />, danger: true, onClick: () => void deactivate() }] : []} />

  return (
    <div className="mx-auto max-w-2xl">
        <section className="rounded-2xl border border-line bg-surface p-6">
          <div className="flex items-start gap-4">
            <span className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-brand-soft text-brand">
              <Monitor size={24} />
            </span>
            <div>
              <h2 className="text-[18px] font-bold">{t('register.title')}</h2>
              <p className="mt-1 text-[14px] leading-relaxed text-ink-2">
                {canManage ? t('register.notBoundHint') : t('register.cashierNotBound')}
              </p>
            </div>
          </div>
          {canManage && (
            <div className="mt-5">
              {list === null ? (
                <Loader2 className="animate-spin text-ink-3" />
              ) : list.length === 0 ? (
                <p className="text-[14px] text-ink-3">{t('register.none')}</p>
              ) : (
                <ul className="flex flex-col gap-2">
                  {list.map((r) => (
                    <li key={r.id} className="flex items-center justify-between gap-3 rounded-xl border border-line px-4 py-3">
                      <div className="min-w-0">
                        <div className="text-[15px] font-semibold">{r.name}</div>
                        {r.isBound && <div className="text-[12px] text-ink-3">{t('register.boundElsewhere')}</div>}
                      </div>
                      <Button loading={busy === r.id} disabled={!!busy} onClick={() => void activate(r.id)}>
                        {t('register.connect')}
                      </Button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
          {error && <p className="mt-3 text-[14px] text-danger">{error}</p>}
        </section>
    </div>
  )
}
