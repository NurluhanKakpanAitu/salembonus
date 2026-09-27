import { clearSession } from './auth'
import { setLocked } from './register'
import { staffApi } from './staffApi'

/**
 * Сеансты аяқтау (шығу немесе кассир ауысуы). Бұғат та алынады: келесі кассир
 * бұғатталған экранға емес, кіру бетіне түседі.
 */
export async function endSession() {
  try {
    await staffApi.logout()
  } catch {
    /* сервер жауап бермесе де, браузердегі сеанс тазаланады */
  } finally {
    setLocked(false)
    clearSession()
  }
}
