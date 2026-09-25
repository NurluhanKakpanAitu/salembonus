import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Check, Phone } from 'lucide-react'
import { BackHeader } from '../components/BackHeader'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { LocationPicker, locationCode, locationFrom, type LocationValue } from '../components/profile/LocationPicker'
import { ApiError } from '../lib/api'
import { initials } from '../lib/format'
import { useMe, useUpdateMe } from '../lib/queries'
import { useT } from '../lib/i18n'

const inputCls =
  'mt-2 h-13 w-full rounded-2xl border border-line bg-surface px-4 text-base outline-none focus:border-brand'

function formatPhone(p: string) {
  const d = p.replace(/\D/g, '')
  return d.length === 11 ? `+${d[0]} ${d.slice(1, 4)} ${d.slice(4, 7)} ${d.slice(7, 9)} ${d.slice(9)}` : p
}

const EMPTY_LOCATION: LocationValue = { region: null, district: null, settlement: null }

export function ProfileEditPage() {
  const t = useT()
  const me = useMe()
  const update = useUpdateMe()
  const navigate = useNavigate()

  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [birthDate, setBirthDate] = useState('')
  const [location, setLocation] = useState<LocationValue>(EMPTY_LOCATION)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const initial = useMemo(
    () =>
      me.data
        ? {
            firstName: me.data.firstName,
            lastName: me.data.lastName,
            birthDate: me.data.birthDate ?? '',
            katoCode: me.data.katoCode ?? null,
            location: locationFrom(me.data),
          }
        : null,
    [me.data],
  )

  useEffect(() => {
    if (!initial) return
    setFirstName(initial.firstName)
    setLastName(initial.lastName)
    setBirthDate(initial.birthDate)
    setLocation(initial.location)
  }, [initial])

  /** Сақтау батырмасы тек бірдеңе өзгергенде белсенді болады. */
  const isDirty =
    !!initial &&
    (firstName.trim() !== initial.firstName ||
      lastName.trim() !== initial.lastName ||
      birthDate !== initial.birthDate ||
      locationCode(location) !== initial.katoCode)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      await update.mutateAsync({
        firstName: firstName.trim(),
        lastName: lastName.trim() || null,
        email: me.data?.email ?? null,
        birthDate: birthDate || null,
        katoCode: locationCode(location),
      })
      setSaved(true)
      setTimeout(() => navigate(-1), 700)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('profile.saveFailed'))
    }
  }

  return (
    <>
      <BackHeader title={t('profile.personal')} fallback="/profile" />

      {me.isPending && <Skeleton className="mt-4 h-72" />}
      {me.isError && <ErrorBox message={me.error.message} onRetry={() => me.refetch()} />}

      {me.data && (
        <form onSubmit={submit} autoComplete="off" className="mt-4 flex flex-col pb-4">
          <div className="flex justify-center">
            <div className="flex size-20 items-center justify-center overflow-hidden rounded-full bg-violet-soft text-2xl font-bold text-violet">
              {me.data.avatarUrl ? (
                <img src={me.data.avatarUrl} alt="" className="size-full object-cover" />
              ) : (
                initials(`${firstName} ${lastName}`) || '·'
              )}
            </div>
          </div>

          <label className="mt-6 text-xs font-medium text-ink-2" htmlFor="profile-firstname">
            {t('profile.firstName')} *
          </label>
          <input
            id="profile-firstname"
            name="profile-firstname"
            autoComplete="off"
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            className={inputCls}
            placeholder={t('profile.firstNamePlaceholder')}
          />

          <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="profile-lastname">
            {t('profile.lastName')}
          </label>
          <input
            id="profile-lastname"
            name="profile-lastname"
            autoComplete="off"
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            className={inputCls}
            placeholder={t('profile.lastNamePlaceholder')}
          />

          <label className="mt-4 text-xs font-medium text-ink-2" htmlFor="profile-birthdate">
            {t('profile.birthDate')}
          </label>
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
          <p className="mt-1.5 text-xs text-ink-3">{t('profile.birthHint')}</p>

          <LocationPicker value={location} onChange={setLocation} />

          <div className="mt-5 rounded-2xl bg-surface px-4 py-3.5">
            <div className="flex items-center gap-3">
              <Phone size={18} className="shrink-0 text-ink-2" />
              <div className="min-w-0 flex-1">
                <div className="text-xs text-ink-2">{t('auth.phoneLabel')}</div>
                <div className="text-[15px] font-semibold">{formatPhone(me.data.phone)}</div>
              </div>
            </div>
            <p className="mt-2 text-xs text-ink-3">{t('profile.phoneNote')}</p>
          </div>

          {error && <p className="mt-3 text-[13px] text-danger">{error}</p>}

          <button
            type="submit"
            disabled={!isDirty || update.isPending || firstName.trim().length < 2}
            className="mt-6 flex h-13 w-full items-center justify-center gap-2 rounded-2xl bg-brand text-[15px] font-semibold text-white transition active:scale-[0.99] disabled:opacity-50"
          >
            {saved ? (
              <>
                <Check size={20} /> {t('profile.saved')}
              </>
            ) : update.isPending ? (
              t('profile.saving')
            ) : (
              t('profile.save')
            )}
          </button>
        </form>
      )}
    </>
  )
}
