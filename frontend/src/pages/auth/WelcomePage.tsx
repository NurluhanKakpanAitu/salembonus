import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { AuthLayout, FieldError, PrimaryButton } from '../../components/auth/AuthLayout'
import { ApiError } from '../../lib/api'
import { useUpdateMe } from '../../lib/queries'

const inputCls = 'mt-2 h-13 w-full rounded-2xl border border-line bg-surface px-4 text-base outline-none focus:border-brand'

/** Жаңа тұтынушы бірінші кіргенде атын толтырады. */
export function WelcomePage() {
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
      setError(err instanceof ApiError ? err.message : 'Сақтау мүмкін болмады')
    }
  }

  return (
    <AuthLayout title="Танысайық" subtitle="Бонустар мен туған күн сыйлығы үшін деректеріңізді толтырыңыз">
      <form onSubmit={submit} autoComplete="off" className="flex flex-1 flex-col">
        <label className="text-xs font-medium text-ink-2" htmlFor="welcome-fullname">Аты-жөні *</label>
        <input id="welcome-fullname" name="welcome-fullname" autoFocus autoComplete="off" value={fullName} onChange={(e) => setFullName(e.target.value)} className={inputCls} placeholder="Мақсадбек Абдужаббаров" />

        <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="welcome-email">Email</label>
        <input id="welcome-email" name="welcome-email" type="email" autoComplete="off" value={email} onChange={(e) => setEmail(e.target.value)} className={inputCls} placeholder="name@example.kz" />

        <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="welcome-birthdate">Туған күні</label>
        <input id="welcome-birthdate" name="welcome-birthdate" type="date" autoComplete="off" value={birthDate} onChange={(e) => setBirthDate(e.target.value)} className={inputCls} max={new Date().toISOString().slice(0, 10)} />
        <p className="mt-1.5 text-xs text-ink-3">Туған күніңізде серіктес дүкендерден сыйлық бонус аласыз</p>

        <FieldError message={error} />
        <PrimaryButton loading={update.isPending} disabled={fullName.trim().length < 2}>Жалғастыру</PrimaryButton>
        <button type="button" onClick={() => navigate('/', { replace: true })} className="mt-4 text-center text-sm text-ink-2">
          Кейінірек толтырамын
        </button>
      </form>
    </AuthLayout>
  )
}
