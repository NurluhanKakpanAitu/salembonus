import { useEffect, useState } from 'react'
import { ArrowLeft, KeyRound, Loader2 } from 'lucide-react'
import { CodeInput } from './ui/CodeInput'
import { ApiError } from '../lib/api'
import { useT, type TranslationKey } from '../lib/i18n'
import { registerApi } from '../lib/registerApi'
import type { Register, RegisterCashier, StaffSession } from '../lib/types'

/**
 * Тіркелген кассада кіру (ТЗ «Касса» §15): қызметкерлер тізімі → PIN. PIN 4 цифр толған бойда
 * жіберіледі. PIN-і жоқ қызметкер таңдалмайды — ол құпиясөзбен кіріп, PIN қоюы керек.
 */
export function CashierPicker({ register, onSignedIn }: {
  register: Register
  onSignedIn: (session: StaffSession) => void
}) {
  const t = useT()
  const [cashiers, setCashiers] = useState<RegisterCashier[] | null>(null)
  const [selected, setSelected] = useState<RegisterCashier | null>(null)
  const [pin, setPin] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    registerApi.cashiers().then(setCashiers).catch(() => setCashiers([]))
  }, [])

  const submit = async (value: string) => {
    if (!selected || value.length !== 4) return
    setLoading(true)
    setError(null)
    try {
      onSignedIn(await registerApi.pinLogin(selected.id, value))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('login.failed'))
      setPin('')
    } finally {
      setLoading(false)
    }
  }

  if (selected)
    return (
      <div>
        <button type="button" onClick={() => { setSelected(null); setPin(''); setError(null) }}
          className="inline-flex items-center gap-1.5 text-[14px] font-medium text-ink-2 hover:text-ink">
          <ArrowLeft size={17} /> {t('common.back')}
        </button>
        <div className="mt-6 flex flex-col items-center text-center">
          <Avatar c={selected} size={64} />
          <div className="mt-3 text-[18px] font-bold">{`${selected.firstName} ${selected.lastName}`}</div>
          <div className="text-[13px] text-ink-3">{t(`role.${selected.role}` as TranslationKey)}</div>
          <p className="mt-6 text-[15px] text-ink-2">{t('login.enterPin')}</p>
          <div className="mt-4">
            <CodeInput value={pin} onChange={(v) => { setPin(v); setError(null); if (v.length === 4) void submit(v) }}
              mask autoFocus error={!!error} label={t('lock.pin')} />
          </div>
          <div className="mt-3 min-h-5 text-[13px]">
            {loading ? <Loader2 size={18} className="animate-spin text-ink-3" /> : error && <span className="text-danger">{error}</span>}
          </div>
        </div>
      </div>
    )

  return (
    <div>
      <p className="text-center text-[13px] text-ink-3">{`${register.name} · ${register.storeName}`}</p>
      <h2 className="mt-1 text-center text-[17px] font-semibold text-ink-2">{t('login.chooseCashier')}</h2>
      {cashiers === null ? (
        <div className="mt-8 flex justify-center text-ink-3"><Loader2 className="animate-spin" /></div>
      ) : cashiers.length === 0 ? (
        <p className="mt-8 text-center text-[14px] text-ink-3">{t('login.noCashiers')}</p>
      ) : (
        <ul className="mt-5 grid grid-cols-2 gap-3">
          {cashiers.map((c) => (
            <li key={c.id}>
              <button type="button" disabled={!c.hasPin} onClick={() => setSelected(c)}
                className="flex w-full flex-col items-center gap-2 rounded-2xl border border-line bg-surface px-3 py-4 text-center transition-colors hover:border-brand hover:bg-brand-soft/40 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:border-line disabled:hover:bg-surface">
                <Avatar c={c} size={48} />
                <span className="w-full truncate text-[15px] font-semibold">{c.firstName}</span>
                <span className="text-[12px] text-ink-3">
                  {c.hasPin ? t(`role.${c.role}` as TranslationKey) : (
                    <span className="inline-flex items-center gap-1"><KeyRound size={12} /> {t('login.noPin')}</span>
                  )}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

export function Avatar({ c, size }: { c: { firstName: string; lastName: string }; size: number }) {
  return (
    <span className="flex shrink-0 items-center justify-center rounded-full bg-brand-soft font-bold text-brand"
      style={{ width: size, height: size, fontSize: size * 0.34 }}>
      {`${c.firstName[0] ?? ''}${c.lastName[0] ?? ''}`.toUpperCase()}
    </span>
  )
}
