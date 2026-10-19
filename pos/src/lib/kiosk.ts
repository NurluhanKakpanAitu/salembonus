/**
 * Касса режимі (ТЗ «Касса»: кассир басқа бөлімдерге кірмейді; X2 POS үлгісі — терминал мен кабинет бөлек).
 *
 * Касса бөлек вкладкада ашылады. Сол вкладкада басқа бөлімнің мекенжайы терілсе де кассаға қайтарады.
 * Белгілер sessionStorage-та: әр вкладкаға бөлек, вкладка жабылғанда жоғалады, кассир ауысса да қалады.
 *
 * Терминал сеансы: PIN-мен кірген кассирдің refresh токені cookie-де емес, осы вкладканың sessionStorage-ында.
 * Сондықтан кассир PIN-мен кірсе, сол браузердегі иесінің кабинет сеансы (httpOnly cookie) бұзылмайды,
 * ал касса вкладкасы жабылса — келесі ашқанда қайта PIN сұралады (терминал үшін дұрыс).
 */
const KIOSK = 'salempos.kiosk'
const SIGNED_OUT = 'salempos.kiosk.signedOut'
const TERMINAL_RT = 'salempos.terminal.rt'

function read(key: string): string | null {
  try {
    return sessionStorage.getItem(key)
  } catch {
    return null
  }
}

function write(key: string, value: string | null) {
  try {
    if (value === null) sessionStorage.removeItem(key)
    else sessionStorage.setItem(key, value)
  } catch {
    /* сақталмаса — бұл вкладкада мәзір жоқ бәрібір; терминал сеансы бет жаңарғанда PIN қайта сұрайды */
  }
}

export const isKiosk = () => read(KIOSK) === '1'
export const enterKiosk = () => write(KIOSK, '1')

/**
 * Кассадан шыққан соң бұл вкладка кабинеттің cookie-сеансына қайта оралмайды: иесі кассада «Выйти» басса,
 * экранда тек PIN қалады, бет жаңарса да иесі өздігінен қайта кірмейді.
 */
export const isSignedOut = () => read(SIGNED_OUT) === '1'
export const markSignedOut = () => write(SIGNED_OUT, '1')
export const clearSignedOut = () => write(SIGNED_OUT, null)

/**
 * Кассаны жабу — кабинетке қайту. Тек кабинеттен кірген иесі/әкімші үшін (терминал сеансы емес):
 * вкладка қайтадан қарапайым кабинет вкладкасына айналады. PIN-мен кірген кассирге бұл жол жоқ.
 */
export function leaveKiosk() {
  write(KIOSK, null)
  write(SIGNED_OUT, null)
  write(TERMINAL_RT, null)
}

export const terminalToken = () => read(TERMINAL_RT)
export const setTerminalToken = (token: string | null) => write(TERMINAL_RT, token)
