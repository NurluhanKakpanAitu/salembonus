import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { ArrowRight, CheckCircle2, Lock, Phone } from 'lucide-react'
import { AuthLayout } from '../components/AuthLayout'
import { CashierPicker } from '../components/CashierPicker'
import { useRegister } from '../lib/register'
import type { StaffSession } from '../lib/types'
import { Button } from '../components/ui/Button'
import { TextField } from '../components/ui/TextField'
import { ApiError } from '../lib/api'
import { applySession, useAuth } from '../lib/auth'
import { useT } from '../lib/i18n'
import { formatPhoneInput, isCompletePhone, phoneDigits } from '../lib/phone'
import { staffApi } from '../lib/staffApi'

type Errors = { phone?: string; password?: string; form?: string }

/**
 * Кіру беті (ТЗ «Авторизация»). Тек телефон мен құпиясөз: тіркелу, Google және басқа
 * сыртқы кіру жоқ (§13). Өрістер серверге жібермей тұрып тексеріледі (§4).
 */
export function LoginPage() {
  const t = useT()
  const navigate = useNavigate()
  const status = useAuth((s) => s.status)
  const startPage = useAuth((s) => s.me?.startPage)
  // Қалпына келтіруден кейін: «Пароль успешно изменён» (ТЗ §7.10).
  const passwordChanged = (useLocation().state as { passwordChanged?: boolean } | null)?.passwordChanged

  const register = useRegister((s) => s.register)
  const [mode, setMode] = useState<'pin' | 'password'>('pin')
  const [phone, setPhone] = useState('+7 ')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<Errors>({})
  const [loading, setLoading] = useState(false)

  if (status === 'authed' && startPage) return <Navigate to={`/${startPage}`} replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const next: Errors = {}
    if (phoneDigits(phone).length <= 1) next.phone = t('login.phoneRequired')
    else if (!isCompletePhone(phone)) next.phone = t('login.phoneInvalid')
    if (!password) next.password = t('login.passwordRequired')
    setErrors(next)
    if (next.phone || next.password) return

    setLoading(true)
    try {
      const session = await staffApi.login(phone, password)
      applySession(session)
      navigate(`/${session.me.startPage}`, { replace: true })
    } catch (err) {
      if (err instanceof ApiError && err.status === 429) setErrors({ form: t('login.tooMany') })
      else if (err instanceof ApiError && (err.field === 'phone' || err.field === 'password'))
        setErrors({ [err.field]: err.message })
      else setErrors({ form: err instanceof ApiError ? err.message : t('login.failed') })
    } finally {
      setLoading(false)
    }
  }

  const form = (
    <form onSubmit={submit} noValidate className="w-full max-w-[410px]">
      <h1 className="text-center text-[30px] font-extrabold tracking-tight text-ink">{t('login.title')}</h1>
      <p className="mt-1.5 text-center text-[15px] text-ink-2">{t('login.subtitle')}</p>

      {passwordChanged && (
        <p role="status" className="mt-6 flex items-center gap-2.5 rounded-xl bg-success-soft px-4 py-3 text-[14px] text-success">
          <CheckCircle2 size={18} className="shrink-0" /> {t('login.passwordChanged')}
        </p>
      )}

      <div className="mt-8 flex flex-col gap-4">
        <TextField
          label={t('login.phone')}
          icon={<Phone size={19} />}
          inputMode="tel"
          autoComplete="tel"
          placeholder="+7 777 123 45 67"
          value={phone}
          onChange={(e) => {
            setPhone(formatPhoneInput(e.target.value))
            if (errors.phone || errors.form) setErrors((p) => ({ ...p, phone: undefined, form: undefined }))
          }}
          error={errors.phone}
        />
        <TextField
          label={t('login.password')}
          icon={<Lock size={19} />}
          secret
          showLabel={t('login.showPassword')}
          hideLabel={t('login.hidePassword')}
          autoComplete="current-password"
          placeholder="••••••••"
          value={password}
          onChange={(e) => {
            setPassword(e.target.value)
            if (errors.password || errors.form) setErrors((p) => ({ ...p, password: undefined, form: undefined }))
          }}
          error={errors.password}
        />
      </div>

      <div className="mt-2.5 text-right">
        <Link to="/forgot-password" className="text-[14px] font-semibold text-brand hover:underline">
          {t('login.forgot')}
        </Link>
      </div>

      {errors.form && (
        <p role="alert" className="mt-4 rounded-xl bg-danger-soft px-4 py-3 text-[14px] text-danger">
          {errors.form}
        </p>
      )}

      <Button
        type="submit"
        loading={loading}
        className="mt-5 h-13 w-full justify-between! px-6 text-[16px]"
        iconRight={<ArrowRight size={20} />}
      >
        <span className="flex-1 text-center">{t('login.submit')}</span>
      </Button>
    </form>
  )

  const signedIn = (session: StaffSession) => {
    applySession(session)
    navigate(`/${session.me.startPage}`, { replace: true })
  }

  // Тіркелген кассада әдепкі бойынша қызметкерлер тізімі мен PIN (ТЗ «Касса» §15).
  if (!register) return <AuthLayout>{form}</AuthLayout>

  return (
    <AuthLayout>
      <div className="w-full max-w-[410px]">
        <div role="tablist" className="mb-8 grid grid-cols-2 rounded-xl bg-field p-1">
          {(['pin', 'password'] as const).map((m) => (
            <button key={m} type="button" role="tab" aria-selected={mode === m} onClick={() => setMode(m)}
              className={`h-10 rounded-lg text-[14px] font-semibold transition-colors ${mode === m ? 'bg-surface text-ink shadow-sm' : 'text-ink-2'}`}>
              {m === 'pin' ? t('login.byPin') : t('login.byPassword')}
            </button>
          ))}
        </div>
        {mode === 'pin' ? <CashierPicker register={register} onSignedIn={signedIn} /> : form}
      </div>
    </AuthLayout>
  )
}
