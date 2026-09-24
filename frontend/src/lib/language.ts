import { useSyncExternalStore } from 'react'

export type Language = 'kk' | 'ru'

export const LANGUAGES: { value: Language; label: string; native: string }[] = [
  { value: 'kk', label: 'Қазақша', native: 'ҚАЗ' },
  { value: 'ru', label: 'Русский', native: 'РУС' },
]

const KEY = 'salembonus.language'
const listeners = new Set<() => void>()

function read(): Language {
  try {
    const saved = localStorage.getItem(KEY)
    if (saved === 'kk' || saved === 'ru') return saved
  } catch {
    // құпия режимде localStorage жабық болуы мүмкін
  }
  // Құрылғы тілі: орысша болса ғана орысша, әйтпесе қазақша.
  return navigator.language?.toLowerCase().startsWith('ru') ? 'ru' : 'kk'
}

let current = read()

/** Хук қолдануға болмайтын жерлерде (api, форматтау) ағымдағы тіл. */
export const getLanguage = (): Language => current

export function setLanguage(lang: Language) {
  current = lang
  try {
    localStorage.setItem(KEY, lang)
  } catch {
    // сақтау мүмкін болмаса да қолданба жұмысын жалғастырады
  }
  document.documentElement.lang = lang
  listeners.forEach((l) => l())
}

export function useLanguage(): [Language, (lang: Language) => void] {
  const lang = useSyncExternalStore(
    (cb) => {
      listeners.add(cb)
      return () => listeners.delete(cb)
    },
    () => current,
    () => current,
  )
  return [lang, setLanguage]
}

document.documentElement.lang = current
