import { useEffect } from 'react'
import { useAuth } from '../lib/auth'
import { setLocked, useRegister } from '../lib/register'
import { registerApi } from '../lib/registerApi'

const ACTIVITY = ['pointerdown', 'pointermove', 'keydown', 'wheel', 'touchstart'] as const

/**
 * Автоблок (ТЗ «Касса» §18.1): тек тіркелген кассада, баптаудағы минут әрекетсіз өтсе
 * касса бұғатталады. Сеанс жабылмайды — интерфейс қана жабылады.
 */
export function useIdleLock() {
  const authed = useAuth((s) => s.status === 'authed')
  const register = useRegister((s) => s.register)
  const locked = useRegister((s) => s.locked)

  useEffect(() => {
    const minutes = register?.autoLockMinutes
    if (!authed || !register || !minutes || locked) return

    let timer = 0
    const reset = () => {
      window.clearTimeout(timer)
      timer = window.setTimeout(() => {
        setLocked(true)
        registerApi.lock().catch(() => undefined)
      }, minutes * 60_000)
    }
    ACTIVITY.forEach((e) => window.addEventListener(e, reset, { passive: true }))
    reset()
    return () => {
      window.clearTimeout(timer)
      ACTIVITY.forEach((e) => window.removeEventListener(e, reset))
    }
  }, [authed, register, locked])
}
