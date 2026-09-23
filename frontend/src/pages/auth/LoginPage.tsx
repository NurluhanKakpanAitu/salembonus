import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { AuthLayout, FieldError, PrimaryButton } from '../../components/auth/AuthLayout'
import { InstallBanner } from '../../components/InstallBanner'
import { authApi, ApiError } from '../../lib/api'
import { useAuth } from '../../lib/auth'
import { formatPhoneInput, isCompletePhone, toE164 } from '../../lib/phone'

export function LoginPage() {
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
      setError('Нөмірді толық енгізіңіз')
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
      setError(err instanceof ApiError ? err.message : 'Байланыс қатесі, қайталап көріңіз')
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthLayout title="Кіру" subtitle="Телефон нөміріңізге SMS код жібереміз">
      <form onSubmit={submit} className="flex flex-1 flex-col">
        <label className="text-xs font-medium text-ink-2" htmlFor="phone">Телефон нөмірі</label>
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
        <PrimaryButton loading={loading} disabled={!isCompletePhone(phone)}>Код алу</PrimaryButton>
        <div className="mt-6"><InstallBanner /></div>
        <p className="mt-auto pt-8 text-center text-xs text-ink-3">
          Жалғастыра отырып, сіз қызмет шарттарымен және құпиялылық саясатымен келісесіз
        </p>
      </form>
    </AuthLayout>
  )
}
