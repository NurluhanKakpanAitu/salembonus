import { useSyncExternalStore } from 'react'

export type ThemeMode = 'system' | 'light' | 'dark'

export const THEME_MODES: ThemeMode[] = ['system', 'light', 'dark']

const KEY = 'salembonus.theme'
const listeners = new Set<() => void>()
const darkQuery = window.matchMedia('(prefers-color-scheme: dark)')

function read(): ThemeMode {
  try {
    const saved = localStorage.getItem(KEY)
    if (saved === 'light' || saved === 'dark' || saved === 'system') return saved
  } catch {
    // құпия режимде localStorage жабық болуы мүмкін
  }
  return 'system'
}

let current = read()

/** "system" таңдалса құрылғының баптауы бойынша нақты тема. */
export const resolveTheme = (mode: ThemeMode): 'light' | 'dark' =>
  mode === 'system' ? (darkQuery.matches ? 'dark' : 'light') : mode

function apply() {
  const theme = resolveTheme(current)
  document.documentElement.dataset.theme = theme
  const meta = document.querySelector('meta[name="theme-color"]')
  if (meta) meta.setAttribute('content', theme === 'dark' ? '#0f1116' : '#0a84f8')
}

export function setThemeMode(mode: ThemeMode) {
  current = mode
  try {
    localStorage.setItem(KEY, mode)
  } catch {
    // сақтау мүмкін болмаса да қолданба жұмысын жалғастырады
  }
  apply()
  listeners.forEach((l) => l())
}

// Құрылғының баптауы ауысса, "system" режимінде тема да ауысады.
darkQuery.addEventListener('change', () => {
  if (current !== 'system') return
  apply()
  listeners.forEach((l) => l())
})

export function useThemeMode(): [ThemeMode, (mode: ThemeMode) => void] {
  const mode = useSyncExternalStore(
    (cb) => {
      listeners.add(cb)
      return () => listeners.delete(cb)
    },
    () => current,
    () => current,
  )
  return [mode, setThemeMode]
}

apply()
