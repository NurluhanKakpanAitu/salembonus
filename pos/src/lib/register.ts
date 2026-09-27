import { create } from 'zustand'
import { registerApi } from './registerApi'
import type { Register } from './types'

const LOCK_KEY = 'salempos.locked'

function readLocked() {
  try {
    return localStorage.getItem(LOCK_KEY) === '1'
  } catch {
    return false
  }
}

interface RegisterState {
  /** undefined — әлі тексерілмеді, null — бұл құрылғы касса емес. */
  register: Register | null | undefined
  /**
   * Касса бұғатталған ба. Браузерде сақталады: бетті жаңартып бұғатты айналып өтуге болмайды
   * (ТЗ «Касса» §18.5 — бұғат сеансты жаппайды, тек интерфейсті жабады).
   */
  locked: boolean
}

export const useRegister = create<RegisterState>(() => ({ register: undefined, locked: readLocked() }))

export async function loadRegister() {
  try {
    useRegister.setState({ register: await registerApi.current() })
  } catch {
    useRegister.setState({ register: null })
  }
}

export function setRegister(register: Register | null) {
  useRegister.setState({ register })
  if (!register) setLocked(false)
}

export function setLocked(locked: boolean) {
  try {
    if (locked) localStorage.setItem(LOCK_KEY, '1')
    else localStorage.removeItem(LOCK_KEY)
  } catch {
    /* сақталмаса — бет жаңарғанда бұғат алынады, бірақ сеанс бәрібір PIN сұрайды */
  }
  useRegister.setState({ locked })
}
