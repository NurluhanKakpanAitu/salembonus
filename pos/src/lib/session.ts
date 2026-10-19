import { clearSession } from './auth'
import { isKiosk, markSignedOut, setTerminalToken, terminalToken } from './kiosk'
import { setLocked } from './register'
import { staffApi } from './staffApi'

/**
 * Сеансты аяқтау (шығу немесе кассир ауысуы). Бұғат та алынады: келесі кассир
 * бұғатталған экранға емес, кіру бетіне түседі.
 *
 * Касса вкладкасында: терминал сеансы болса — тек сол жабылады (кабинеттің cookie-сеансы қалады);
 * иесі кабинеттен кірген болса — cookie-сеансы да жабылады (ортақ кассада ашық кабинет қалмауы керек).
 * Қалай болса да вкладка «шықтым» күйіне түседі: әрі қарай тек PIN.
 */
export async function endSession() {
  const terminal = terminalToken()
  try {
    await staffApi.logout(terminal)
  } catch {
    /* сервер жауап бермесе де, браузердегі сеанс тазаланады */
  } finally {
    if (isKiosk()) {
      setTerminalToken(null)
      markSignedOut()
    }
    setLocked(false)
    clearSession()
  }
}
