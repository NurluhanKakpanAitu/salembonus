import { useState, type FormEvent } from 'react'
import { CheckCircle2, KeyRound, Lock } from 'lucide-react'
import { Modal } from './ui/Modal'
import { Button } from './ui/Button'
import { TextField } from './ui/TextField'
import { ApiError } from '../lib/api'
import { applyMe } from '../lib/auth'
import { useT } from '../lib/i18n'
import { staffApi } from '../lib/staffApi'

type Errors = Record<string, string | undefined>

function useFormErrors() {
  const t = useT()
  const [errors, setErrors] = useState<Errors>({})
  const fail = (err: unknown) => {
    if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
    else setErrors({ form: err instanceof ApiError ? err.message : t('login.failed') })
  }
  return { errors, setErrors, fail }
}

const digitsOnly = (v: string) => v.replace(/\D/g, '').slice(0, 4)

/** PIN қою не ауыстыру — ағымдағы құпиясөзбен расталады (ТЗ «Касса» §17.8). */
export function PinModal({ hasPin, onClose }: { hasPin: boolean; onClose: () => void }) {
  const t = useT()
  const { errors, setErrors, fail } = useFormErrors()
  const [password, setPassword] = useState('')
  const [pin, setPin] = useState('')
  const [confirm, setConfirm] = useState('')
  const [loading, setLoading] = useState(false)
  const [done, setDone] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const next: Errors = {}
    if (!password) next.currentPassword = t('common.required')
    if (pin.length !== 4) next.pin = t('pin.format')
    else if (pin !== confirm) next.confirmPin = t('pin.mismatch')
    setErrors(next)
    if (Object.values(next).some(Boolean)) return

    setLoading(true)
    try {
      applyMe(await staffApi.setPin(password, pin, confirm))
      setDone(true)
    } catch (err) {
      fail(err)
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title={hasPin ? t('top.changePin') : t('top.setPin')} onClose={onClose}>
      {done ? (
        <Done text={t('pin.saved')} onClose={onClose} />
      ) : (
        <form onSubmit={submit} noValidate className="flex flex-col gap-4">
          <p className="text-[14px] leading-relaxed text-ink-2">{t('pin.hint')}</p>
          <TextField
            label={t('pin.currentPassword')}
            icon={<Lock size={18} />}
            secret
            autoComplete="current-password"
            autoFocus
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors.currentPassword}
          />
          <div className="grid grid-cols-2 gap-3">
            <TextField
              label={t('pin.new')}
              icon={<KeyRound size={18} />}
              secret
              inputMode="numeric"
              autoComplete="off"
              value={pin}
              onChange={(e) => setPin(digitsOnly(e.target.value))}
              error={errors.pin}
            />
            <TextField
              label={t('pin.repeat')}
              icon={<KeyRound size={18} />}
              secret
              inputMode="numeric"
              autoComplete="off"
              value={confirm}
              onChange={(e) => setConfirm(digitsOnly(e.target.value))}
              error={errors.confirmPin}
            />
          </div>
          {errors.form && <p className="text-[14px] text-danger">{errors.form}</p>}
          <Buttons loading={loading} onClose={onClose} submit={t('pin.save')} />
        </form>
      )}
    </Modal>
  )
}

/** Құпиясөзді ауыстыру — ағымдағысын растап (ТЗ «Касса» §17.7). */
export function PasswordModal({ onClose }: { onClose: () => void }) {
  const t = useT()
  const { errors, setErrors, fail } = useFormErrors()
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [loading, setLoading] = useState(false)
  const [done, setDone] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const errs: Errors = {}
    if (!current) errs.currentPassword = t('common.required')
    if (!next) errs.newPassword = t('common.required')
    else if (next !== confirm) errs.confirmPassword = t('reset.mismatch')
    setErrors(errs)
    if (Object.values(errs).some(Boolean)) return

    setLoading(true)
    try {
      await staffApi.changePassword(current, next, confirm)
      setDone(true)
    } catch (err) {
      fail(err)
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title={t('password.title')} onClose={onClose}>
      {done ? (
        <Done text={t('password.saved')} onClose={onClose} />
      ) : (
        <form onSubmit={submit} noValidate className="flex flex-col gap-4">
          <TextField label={t('pin.currentPassword')} icon={<Lock size={18} />} secret autoComplete="current-password"
            autoFocus value={current} onChange={(e) => setCurrent(e.target.value)} error={errors.currentPassword} />
          <TextField label={t('reset.newPassword')} icon={<Lock size={18} />} secret autoComplete="new-password"
            value={next} onChange={(e) => setNext(e.target.value)} error={errors.newPassword} />
          <TextField label={t('reset.repeatPassword')} icon={<Lock size={18} />} secret autoComplete="new-password"
            value={confirm} onChange={(e) => setConfirm(e.target.value)} error={errors.confirmPassword} />
          {errors.form && <p className="text-[14px] text-danger">{errors.form}</p>}
          <Buttons loading={loading} onClose={onClose} submit={t('pin.save')} />
        </form>
      )}
    </Modal>
  )
}

function Buttons({ loading, onClose, submit }: { loading: boolean; onClose: () => void; submit: string }) {
  const t = useT()
  return (
    <div className="mt-2 flex justify-end gap-2">
      <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
      <Button type="submit" loading={loading}>{submit}</Button>
    </div>
  )
}

function Done({ text, onClose }: { text: string; onClose: () => void }) {
  const t = useT()
  return (
    <div className="flex flex-col items-center gap-4 py-2 text-center">
      <CheckCircle2 size={44} className="text-success" />
      <p className="text-[15px] text-ink">{text}</p>
      <Button onClick={onClose} className="w-full">{t('common.close')}</Button>
    </div>
  )
}
