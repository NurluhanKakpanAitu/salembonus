import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { AuthLayout, FieldError, PrimaryButton } from '../../components/auth/AuthLayout'
import { ApiError } from '../../lib/api'
import { useUpdateMe } from '../../lib/queries'
import { useT } from '../../lib/i18n'

const inputCls = 'mt-2 h-13 w-full rounded-2xl border border-line bg-surface px-4 text-base outline-none focus:border-brand'

/** Жаңа тұтынушы бірінші кіргенде атын толтырады. */
export function WelcomePage() {
  const t = useT()
  const navigate = useNavigate()
  const update = useUpdateMe()
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [birthDate, setBirthDate] = useState('')
  const [error, setError] = useState<string | null>(null)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      await update.mutateAsync({ fullName: fullName.trim(), email: email.trim() || null, birthDate: birthDate || null })
      navigate('/', { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('profile.saveFailed'))
    }
  }

  return (
    <AuthLayout title={t('auth.welcome.title')} subtitle={t('auth.welcome.subtitle')}>
      <form onSubmit={submit} autoComplete="off" className="flex flex-1 flex-col">
        <label className="text-xs font-medium text-ink-2" htmlFor="welcome-fullname">{t('profile.fullName')} *</label>
        <input id="welcome-fullname" name="welcome-fullname" autoFocus autoComplete="off" value={fullName} onChange={(e) => setFullName(e.target.value)} className={inputCls} placeholder={t('profile.namePlaceholder')} />

        <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="welcome-email">Email</label>
        <input id="welcome-email" name="welcome-email" type="email" autoComplete="off" value={email} onChange={(e) => setEmail(e.target.value)} className={inputCls} placeholder={t('profile.emailPlaceholder')} />

        <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="welcome-birthdate">{t('profile.birthDate')}</label>
        <input id="welcome-birthdate" name="welcome-birthdate" type="date" autoComplete="off" value={birthDate} onChange={(e) => setBirthDate(e.target.value)} className={inputCls} max={new Date().toISOString().slice(0, 10)} />
        <p className="mt-1.5 text-xs text-ink-3">{t('profile.birthHint')}</p>

        <FieldError message={error} />
        <PrimaryButton loading={update.isPending} disabled={fullName.trim().length < 2}>{t('auth.continue')}</PrimaryButton>
        <button type="button" onClick={() => navigate('/', { replace: true })} className="mt-4 text-center text-sm text-ink-2">
          {t('auth.later')}
        </button>
      </form>
    </AuthLayout>
  )
}
