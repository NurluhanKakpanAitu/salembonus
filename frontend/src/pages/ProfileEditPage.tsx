import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Check, Phone } from 'lucide-react'
import { BackHeader } from '../components/BackHeader'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { ApiError } from '../lib/api'
import { initials } from '../lib/format'
import { useMe, useUpdateMe } from '../lib/queries'

const inputCls =
  'mt-2 h-13 w-full rounded-2xl border border-line bg-surface px-4 text-base outline-none focus:border-brand'

function formatPhone(p: string) {
  const d = p.replace(/\D/g, '')
  return d.length === 11 ? `+${d[0]} ${d.slice(1, 4)} ${d.slice(4, 7)} ${d.slice(7, 9)} ${d.slice(9)}` : p
}

export function ProfileEditPage() {
  const me = useMe()
  const update = useUpdateMe()
  const navigate = useNavigate()

  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [birthDate, setBirthDate] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  // Телефон аты ретінде сақталса (әлі толтырылмаған профиль), бос өріс көрсетеміз
  const initial = useMemo(
    () =>
      me.data
        ? {
            fullName: me.data.fullName.startsWith('+') ? '' : me.data.fullName,
            email: me.data.email ?? '',
            birthDate: me.data.birthDate ?? '',
          }
        : null,
    [me.data],
  )

  useEffect(() => {
    if (!initial) return
    setFullName(initial.fullName)
    setEmail(initial.email)
    setBirthDate(initial.birthDate)
  }, [initial])

  /** Сақтау батырмасы тек бірдеңе өзгергенде белсенді болады. */
  const isDirty =
    !!initial &&
    (fullName.trim() !== initial.fullName ||
      email.trim() !== initial.email ||
      birthDate !== initial.birthDate)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      await update.mutateAsync({
        fullName: fullName.trim(),
        email: email.trim() || null,
        birthDate: birthDate || null,
      })
      setSaved(true)
      setTimeout(() => navigate(-1), 700)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Сақтау мүмкін болмады')
    }
  }

  return (
    <>
      <BackHeader title="Жеке деректер" fallback="/profile" />

      {me.isPending && <Skeleton className="mt-4 h-72" />}
      {me.isError && <ErrorBox message={me.error.message} onRetry={() => me.refetch()} />}

      {me.data && (
        <form onSubmit={submit} autoComplete="off" className="mt-4 flex flex-col">
          <div className="flex justify-center">
            <div className="flex size-20 items-center justify-center rounded-full bg-violet-soft text-2xl font-bold text-violet">
              {initials(fullName) || '·'}
            </div>
          </div>

          <label className="mt-6 text-xs font-medium text-ink-2" htmlFor="profile-fullname">Аты-жөні *</label>
          <input
            id="profile-fullname"
            name="profile-fullname"
            autoComplete="off"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            className={inputCls}
            placeholder="Мақсадбек Абдужаббаров"
          />

          <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="profile-email">Email</label>
          <input
            id="profile-email"
            name="profile-email"
            type="email"
            autoComplete="off"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className={inputCls}
            placeholder="name@example.kz"
          />

          <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="profile-birthdate">Туған күні</label>
          <input
            id="profile-birthdate"
            name="profile-birthdate"
            type="date"
            autoComplete="off"
            value={birthDate}
            onChange={(e) => setBirthDate(e.target.value)}
            className={inputCls}
            max={new Date().toISOString().slice(0, 10)}
          />
          <p className="mt-1.5 text-xs text-ink-3">Туған күніңізде серіктес дүкендерден сыйлық бонус аласыз</p>

          <div className="mt-5 rounded-2xl bg-surface px-4 py-3.5">
            <div className="flex items-center gap-3">
              <Phone size={18} className="shrink-0 text-ink-2" />
              <div className="min-w-0 flex-1">
                <div className="text-xs text-ink-2">Телефон нөмірі</div>
                <div className="text-[15px] font-semibold">{formatPhone(me.data.phone)}</div>
              </div>
            </div>
            <p className="mt-2 text-xs text-ink-3">
              Нөмір аккаунтқа кіру үшін қолданылады, оны өзгерту үшін қолдау қызметіне жазыңыз.
            </p>
          </div>

          {error && <p className="mt-3 text-[13px] text-danger">{error}</p>}

          <button
            type="submit"
            disabled={!isDirty || update.isPending || fullName.trim().length < 2}
            className="mt-6 flex h-13 w-full items-center justify-center gap-2 rounded-2xl bg-brand text-[15px] font-semibold text-white transition active:scale-[0.99] disabled:opacity-50"
          >
            {saved ? <><Check size={20} /> Сақталды</> : update.isPending ? 'Сақталуда…' : 'Сақтау'}
          </button>
        </form>
      )}
    </>
  )
}
