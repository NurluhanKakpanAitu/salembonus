import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { AuthLayout, FieldError, PrimaryButton } from '../../components/auth/AuthLayout'
import { authApi, ApiError } from '../../lib/api'
import { useAuth } from '../../lib/auth'
import { formatPhoneInput } from '../../lib/phone'
import { useT } from '../../lib/i18n'

const CODE_LENGTH = 4

export function VerifyPage() {
  const t = useT()
  const phone = useAuth((s) => s.pendingPhone)
  const setTokens = useAuth((s) => s.setTokens)
  const navigate = useNavigate()
  const { state } = useLocation() as { state?: { devCode?: string | null; retryAfter?: number } }
  const [code, setCode] = useState('')
  const [devCode, setDevCode] = useState<string | null>(state?.devCode ?? null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [retryIn, setRetryIn] = useState(state?.retryAfter ?? 60)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (retryIn <= 0) return
    const t = setInterval(() => setRetryIn((s) => s - 1), 1000)
    return () => clearInterval(t)
  }, [retryIn])

  if (!phone) return <Navigate to="/login" replace />

  const verify = async (value: string) => {
    setLoading(true)
    setError(null)
    try {
      const tokens = await authApi.verify(phone, value)
      setTokens(tokens)
      navigate(tokens.profileCompleted ? '/' : '/welcome', { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('auth.networkError'))
      setCode('')
      inputRef.current?.focus()
    } finally {
      setLoading(false)
    }
  }

  const onChange = (raw: string) => {
    const v = raw.replace(/\D/g, '').slice(0, CODE_LENGTH)
    setCode(v)
    if (v.length === CODE_LENGTH) void verify(v)
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (code.length === CODE_LENGTH) void verify(code)
  }

  const resend = async () => {
    setError(null)
    try {
      const res = await authApi.requestCode(phone)
      setDevCode(res.devCode)
      setRetryIn(res.retryAfterSeconds)
      setCode('')
      inputRef.current?.focus()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('auth.networkErrorShort'))
    }
  }

  return (
    <AuthLayout title={t('auth.verify.title')} subtitle={t('auth.verify.subtitle', { phone: formatPhoneInput(phone) })}>
      <form onSubmit={submit} className="flex flex-1 flex-col">
        <div className="relative" onClick={() => inputRef.current?.focus()}>
          <div className="flex justify-center gap-3">
            {Array.from({ length: CODE_LENGTH }).map((_, i) => (
              <div
                key={i}
                className={`flex h-16 w-14 items-center justify-center rounded-2xl border-2 bg-surface text-2xl font-bold ${
                  i === code.length ? 'border-brand' : 'border-line'
                }`}
              >
                {code[i] ?? ''}
              </div>
            ))}
          </div>
          <input
            ref={inputRef}
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            autoFocus
            value={code}
            onChange={(e) => onChange(e.target.value)}
            className="absolute inset-0 opacity-0"
            aria-label={t('auth.smsCode')}
          />
        </div>
        <FieldError message={error} />
        {devCode && (
          <p className="mt-3 rounded-xl bg-violet-soft px-3 py-2 text-center text-xs text-violet">
            {t('auth.devCode')} <b className="tracking-widest">{devCode}</b>
          </p>
        )}
        <PrimaryButton loading={loading} disabled={code.length !== CODE_LENGTH}>{t('auth.enter')}</PrimaryButton>
        <div className="mt-4 text-center text-sm">
          {retryIn > 0 ? (
            <span className="text-ink-3">{t('auth.resendIn', { seconds: retryIn })}</span>
          ) : (
            <button type="button" onClick={resend} className="font-semibold text-brand">{t('auth.resend')}</button>
          )}
        </div>
        <button type="button" onClick={() => navigate('/login')} className="mt-auto pt-8 text-center text-sm text-ink-2">
          {t('auth.changePhone')}
        </button>
      </form>
    </AuthLayout>
  )
}
