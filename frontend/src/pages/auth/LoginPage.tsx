import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { AuthLayout, FieldError, PrimaryButton } from '../../components/auth/AuthLayout'
import { authApi, ApiError } from '../../lib/api'
import { useAuth } from '../../lib/auth'
import { formatPhoneInput, isCompletePhone, toE164 } from '../../lib/phone'
import { useT } from '../../lib/i18n'

export function LoginPage() {
  const t = useT()
  const loggedIn = useAuth((s) => !!s.accessToken)
  const setPendingPhone = useAuth((s) => s.setPendingPhone)
  const navigate = useNavigate()
  const [phone, setPhone] = useState('+7 ')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  if (loggedIn) return <Navigate to="/" replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!isCompletePhone(phone)) {
      setError(t('auth.phoneIncomplete'))
      return
    }
    setLoading(true)
    setError(null)
    try {
      const e164 = toE164(phone)
      const res = await authApi.requestCode(e164)
      setPendingPhone(e164)
      navigate('/verify', { state: { devCode: res.devCode, retryAfter: res.retryAfterSeconds } })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('auth.networkError'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthLayout title={t('auth.login.title')} subtitle={t('auth.login.subtitle')}>
      <form onSubmit={submit} className="flex flex-1 flex-col">
        <label className="text-xs font-medium text-ink-2" htmlFor="phone">{t('auth.phoneLabel')}</label>
        <input
          id="phone"
          type="tel"
          inputMode="tel"
          autoComplete="tel"
          autoFocus
          value={phone}
          onChange={(e) => setPhone(formatPhoneInput(e.target.value))}
          className="mt-2 h-14 w-full rounded-2xl border border-line bg-surface px-4 text-xl font-semibold tracking-wide outline-none focus:border-brand"
        />
        <FieldError message={error} />
        <PrimaryButton loading={loading} disabled={!isCompletePhone(phone)}>{t('auth.getCode')}</PrimaryButton>
        <p className="mt-auto pt-8 text-center text-xs text-ink-3">
          {t('auth.terms')}
        </p>
      </form>
    </AuthLayout>
  )
}
