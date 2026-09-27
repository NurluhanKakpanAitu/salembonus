import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Loader2, Lock, UsersRound } from 'lucide-react'
import { Logo } from './Logo'
import { Avatar } from './CashierPicker'
import { CodeInput } from './ui/CodeInput'
import { ApiError } from '../lib/api'
import { activeStore, useAuth } from '../lib/auth'
import { useT, type TranslationKey } from '../lib/i18n'
import { setLocked } from '../lib/register'
import { registerApi } from '../lib/registerApi'
import { endSession } from '../lib/session'

/**
 * «Касса заблокирована» (ТЗ «Касса» §18). PIN дұрыс болса — интерфейс сол күйінде ашылады
 * (себет, ашық бет жоғалмайды). Қате PIN саны серверде шектеулі.
 */
export function LockScreen() {
  const t = useT()
  const navigate = useNavigate()
  const me = useAuth((s) => s.me)
  const store = useAuth(activeStore)
  const [pin, setPin] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const unlock = async (value: string) => {
    setLoading(true)
    setError(null)
    try {
      await registerApi.unlock(value)
      setLocked(false)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('login.failed'))
      setPin('')
    } finally {
      setLoading(false)
    }
  }

  const switchCashier = async () => {
    await endSession()
    navigate('/login', { replace: true })
  }

  return (
    <div className="fixed inset-0 z-[60] flex flex-col items-center justify-center bg-[radial-gradient(120%_90%_at_30%_20%,#0d3a8f_0%,#071a3d_45%,#040c1f_100%)] px-6 text-white">
      <div className="absolute left-6 top-6"><Logo size={26} /></div>

      <span className="flex size-14 items-center justify-center rounded-2xl bg-white/10"><Lock size={26} /></span>
      <h1 className="mt-4 text-[26px] font-extrabold tracking-tight">{t('lock.title')}</h1>

      {me && (
        <div className="mt-6 flex items-center gap-3 rounded-2xl bg-white/8 px-4 py-2.5">
          <Avatar c={me} size={40} />
          <div className="text-left">
            <div className="text-[15px] font-semibold">{`${me.firstName} ${me.lastName}`}</div>
            <div className="text-[12px] text-white/60">{store ? t(`role.${store.role}` as TranslationKey) : ''}</div>
          </div>
        </div>
      )}

      {me?.hasPin ? (
        <>
          <p className="mt-6 text-[15px] text-white/75">{t('lock.hint')}</p>
          <div className="mt-4">
            <CodeInput value={pin} onChange={(v) => { setPin(v); setError(null); if (v.length === 4) void unlock(v) }}
              mask dark autoFocus error={!!error} label={t('lock.pin')} />
          </div>
          <div className="mt-3 min-h-5 text-center text-[14px]">
            {loading ? <Loader2 size={18} className="mx-auto animate-spin text-white/60" /> : error && <span className="text-[#ff8a8e]">{error}</span>}
          </div>
        </>
      ) : (
        <p className="mt-6 max-w-sm text-center text-[15px] text-white/75">{t('lock.noPin')}</p>
      )}

      <button type="button" onClick={() => void switchCashier()}
        className="mt-6 inline-flex h-11 items-center gap-2 rounded-xl border border-white/15 bg-white/10 px-5 text-[15px] font-semibold hover:bg-white/15">
        <UsersRound size={18} /> {me?.hasPin ? t('top.switchCashier') : t('lock.passwordLogin')}
      </button>
    </div>
  )
}
