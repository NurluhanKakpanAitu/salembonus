import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, Lock, Phone } from 'lucide-react'
import { AuthLayout } from '../components/AuthLayout'
import { Button } from '../components/ui/Button'
import { CodeInput } from '../components/ui/CodeInput'
import { TextField } from '../components/ui/TextField'
import { ApiError } from '../lib/api'
import { useT } from '../lib/i18n'
import { formatPhoneInput, isCompletePhone, phoneDigits } from '../lib/phone'
import { staffApi } from '../lib/staffApi'
import type { PasswordPolicy } from '../lib/types'

type Step = 'phone' | 'code' | 'password'
type Errors = Partial<Record<'phone' | 'code' | 'newPassword' | 'confirmPassword' | 'form', string>>

/**
 * Құпиясөзді қалпына келтіру (ТЗ «Авторизация» §5–7): нөмір → WhatsApp-қа 4 таңбалы код →
 * жаңа құпиясөз. Жаңа құпиясөз формасы тек код серверде расталғаннан кейін ашылады.
 */
export function ForgotPasswordPage() {
  const t = useT()
  const navigate = useNavigate()
  const [step, setStep] = useState<Step>('phone')
  const [phone, setPhone] = useState('+7 ')
  const [code, setCode] = useState('')
  const [codeLength, setCodeLength] = useState(4)
  const [devCode, setDevCode] = useState<string | null>(null)
  const [resendAt, setResendAt] = useState(0)
  const [now, setNow] = useState(() => Date.now())
  const [resetToken, setResetToken] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [policy, setPolicy] = useState<PasswordPolicy>({ minLength: 8, requireLetterAndDigit: true })
  const [errors, setErrors] = useState<Errors>({})
  const [loading, setLoading] = useState(false)

  // Қайта жіберуге дейінгі кері санақ.
  useEffect(() => {
    if (step !== 'code' || resendAt <= Date.now()) return
    const id = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(id)
  }, [step, resendAt])

  useEffect(() => {
    if (step === 'password') staffApi.passwordPolicy().then(setPolicy).catch(() => undefined)
  }, [step])

  /** Сервер қатесін сәйкес өріске, өріс белгісіз болса — форманың жалпы қатесіне. */
  const fail = (err: unknown, fallbackField: keyof Errors = 'form') => {
    if (err instanceof ApiError && err.status === 429) setErrors({ form: t('login.tooMany') })
    else if (err instanceof ApiError)
      setErrors({ [(err.field as keyof Errors) ?? fallbackField]: err.message })
    else setErrors({ form: t('login.failed') })
  }

  const sendCode = async () => {
    setErrors({})
    setLoading(true)
    try {
      const res = await staffApi.requestReset(phone)
      setCodeLength(res.codeLength)
      setDevCode(res.devCode)
      setResendAt(Date.now() + res.retryAfterSeconds * 1000)
      setNow(Date.now())
      setCode('')
      setStep('code')
    } catch (err) {
      fail(err, step === 'code' ? 'code' : 'phone')
    } finally {
      setLoading(false)
    }
  }

  const submitPhone = (e: FormEvent) => {
    e.preventDefault()
    if (phoneDigits(phone).length <= 1) return setErrors({ phone: t('login.phoneRequired') })
    if (!isCompletePhone(phone)) return setErrors({ phone: t('login.phoneInvalid') })
    void sendCode()
  }

  const submitCode = async (e: FormEvent) => {
    e.preventDefault()
    setErrors({})
    setLoading(true)
    try {
      const res = await staffApi.verifyReset(phone, code)
      setResetToken(res.resetToken)
      setStep('password')
    } catch (err) {
      fail(err, 'code')
    } finally {
      setLoading(false)
    }
  }

  const submitPassword = async (e: FormEvent) => {
    e.preventDefault()
    const next: Errors = {}
    if (password.length < policy.minLength) next.newPassword = t('reset.tooShort', { min: policy.minLength })
    else if (policy.requireLetterAndDigit && !(/\p{L}/u.test(password) && /\d/.test(password)))
      next.newPassword = t('reset.weak')
    if (!next.newPassword && password !== confirm) next.confirmPassword = t('reset.mismatch')
    setErrors(next)
    if (next.newPassword || next.confirmPassword) return

    setLoading(true)
    try {
      await staffApi.completeReset(resetToken, password, confirm)
      navigate('/login', { replace: true, state: { passwordChanged: true } })
    } catch (err) {
      fail(err)
    } finally {
      setLoading(false)
    }
  }

  const secondsLeft = Math.max(0, Math.ceil((resendAt - now) / 1000))
  const timer = `${Math.floor(secondsLeft / 60)}:${String(secondsLeft % 60).padStart(2, '0')}`

  const formError = errors.form && (
    <p role="alert" className="mt-4 rounded-xl bg-danger-soft px-4 py-3 text-[14px] text-danger">
      {errors.form}
    </p>
  )

  return (
    <AuthLayout>
      <div className="w-full max-w-[410px]">
        <Link to="/login" className="inline-flex items-center gap-1.5 text-[14px] font-medium text-ink-2 hover:text-ink">
          <ArrowLeft size={17} /> {t('reset.backToLogin')}
        </Link>

        {step === 'phone' && (
          <form onSubmit={submitPhone} noValidate className="mt-6">
            <h1 className="text-center text-[26px] font-extrabold tracking-tight">{t('reset.title')}</h1>
            <p className="mt-1.5 text-center text-[15px] text-ink-2">{t('reset.phoneHint')}</p>
            <TextField
              className="mt-8"
              label={t('login.phone')}
              icon={<Phone size={19} />}
              inputMode="tel"
              autoComplete="tel"
              autoFocus
              placeholder="+7 777 123 45 67"
              value={phone}
              onChange={(e) => {
                setPhone(formatPhoneInput(e.target.value))
                if (errors.phone || errors.form) setErrors({})
              }}
              error={errors.phone}
            />
            {formError}
            <Button type="submit" loading={loading} className="mt-6 h-13 w-full text-[16px]">
              {t('reset.getCode')}
            </Button>
          </form>
        )}

        {step === 'code' && (
          <form onSubmit={submitCode} noValidate className="mt-6">
            <h1 className="text-center text-[26px] font-extrabold tracking-tight">{t('reset.codeTitle')}</h1>
            <p className="mt-1.5 text-center text-[15px] text-ink-2">
              {/* Нөмір жолға бөлінбесін: бос орындар — бөлінбейтін бос орын. */}
              {t('reset.codeHint', { length: codeLength, phone: phone.replaceAll(' ', '\u00a0') })}
            </p>
            <div className="mt-8">
              <CodeInput
                length={codeLength}
                value={code}
                onChange={(v) => {
                  setCode(v)
                  if (errors.code || errors.form) setErrors({})
                }}
                error={!!errors.code}
                autoFocus
                label={t('reset.code')}
              />
              {errors.code && <p className="mt-2.5 text-center text-[13px] text-danger">{errors.code}</p>}
              {import.meta.env.DEV && devCode && (
                <p className="mt-2.5 text-center text-[12px] text-ink-3">{t('reset.devCode', { code: devCode })}</p>
              )}
            </div>
            {formError}
            <Button type="submit" loading={loading} disabled={code.length !== codeLength} className="mt-6 h-13 w-full text-[16px]">
              {t('reset.confirm')}
            </Button>
            <div className="mt-4 flex items-center justify-between text-[14px]">
              <button
                type="button"
                onClick={() => {
                  setStep('phone')
                  setErrors({})
                }}
                className="font-medium text-ink-2 hover:text-ink"
              >
                {t('reset.changePhone')}
              </button>
              {secondsLeft > 0 ? (
                <span className="text-ink-3">{t('reset.resendIn', { time: timer })}</span>
              ) : (
                <button type="button" disabled={loading} onClick={() => void sendCode()} className="font-semibold text-brand">
                  {t('reset.resend')}
                </button>
              )}
            </div>
          </form>
        )}

        {step === 'password' && (
          <form onSubmit={submitPassword} noValidate className="mt-6">
            <h1 className="text-center text-[26px] font-extrabold tracking-tight">{t('reset.newTitle')}</h1>
            <p className="mt-1.5 text-center text-[15px] text-ink-2">{t('reset.newHint')}</p>
            <div className="mt-8 flex flex-col gap-4">
              <div>
                <TextField
                  label={t('reset.newPassword')}
                  icon={<Lock size={19} />}
                  secret
                  showLabel={t('login.showPassword')}
                  hideLabel={t('login.hidePassword')}
                  autoComplete="new-password"
                  autoFocus
                  value={password}
                  onChange={(e) => {
                    setPassword(e.target.value)
                    if (errors.newPassword || errors.form) setErrors({})
                  }}
                  error={errors.newPassword}
                />
                {!errors.newPassword && (
                  <p className="mt-1.5 text-[12px] text-ink-3">
                    {t(policy.requireLetterAndDigit ? 'reset.policyLettersDigits' : 'reset.policy', { min: policy.minLength })}
                  </p>
                )}
              </div>
              <TextField
                label={t('reset.repeatPassword')}
                icon={<Lock size={19} />}
                secret
                showLabel={t('login.showPassword')}
                hideLabel={t('login.hidePassword')}
                autoComplete="new-password"
                value={confirm}
                onChange={(e) => {
                  setConfirm(e.target.value)
                  if (errors.confirmPassword || errors.form) setErrors({})
                }}
                error={errors.confirmPassword}
              />
            </div>
            {formError}
            <Button type="submit" loading={loading} className="mt-6 h-13 w-full text-[16px]">
              {t('reset.save')}
            </Button>
          </form>
        )}
      </div>
    </AuthLayout>
  )
}
