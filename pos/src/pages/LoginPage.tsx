import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { ArrowRight, Lock, Phone } from 'lucide-react'
import { Logo } from '../components/Logo'
import { LanguageSelect } from '../components/LanguageSelect'
import { Button } from '../components/ui/Button'
import { TextField } from '../components/ui/TextField'
import { ApiError } from '../lib/api'
import { applySession, useAuth } from '../lib/auth'
import { useT } from '../lib/i18n'
import { useLanguageStore } from '../lib/language'
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
  const lang = useLanguageStore((s) => s.lang)
  const setLang = useLanguageStore((s) => s.set)

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

      {errors.form && (
        <p role="alert" className="mt-4 rounded-xl bg-danger-soft px-4 py-3 text-[14px] text-danger">
          {errors.form}
        </p>
      )}

      <Button
        type="submit"
        loading={loading}
        className="mt-6 h-13 w-full justify-between! px-6 text-[16px]"
        iconRight={<ArrowRight size={20} />}
      >
        <span className="flex-1 text-center">{t('login.submit')}</span>
      </Button>
    </form>
  )

  return (
    <div className="flex min-h-full flex-col bg-surface lg:flex-row">
      {/* Брендтік аймақ: қара-көк фон, тек логотип (ТЗ §2.1–2.3) */}
      <section className="relative flex shrink-0 flex-col items-center justify-center overflow-hidden bg-[radial-gradient(120%_90%_at_30%_20%,#0d3a8f_0%,#071a3d_45%,#040c1f_100%)] px-6 pb-16 pt-24 lg:w-1/2 lg:pb-0 lg:pt-0">
        <div className="absolute right-4 top-4 lg:hidden">
          <LanguageSelect value={lang} onChange={setLang} tone="light" />
        </div>
        <div className="scale-75 sm:scale-90 lg:scale-100">
          <Logo size={72} tone="light" tagline={t('login.tagline')} />
        </div>
      </section>

      {/* Форма: мобильдіде қара аймақтың үстіне шығатын ақ карточка */}
      <section className="relative -mt-8 flex flex-1 flex-col items-center rounded-t-[28px] bg-surface px-6 pb-10 pt-10 lg:mt-0 lg:justify-center lg:rounded-none lg:pt-0">
        <div className="absolute right-6 top-6 hidden lg:block">
          <LanguageSelect value={lang} onChange={setLang} />
        </div>
        {form}
      </section>
    </div>
  )
}
