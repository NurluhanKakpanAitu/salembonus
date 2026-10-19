import { useEffect, useRef } from 'react'

/**
 * Штрихкод сканері «пернетақта» режимінде: кодты тез теріп, Enter басады. Теру өрісте болмаса —
 * жылдам енгізілген 4+ таңба + Enter штрихкод деп танылады. Өрісте тұрғанда сканер мәтінді сол өріске
 * жазады — онда өрістің өз өңдеуі істейді.
 */
export function useBarcodeScanner(onScan: (code: string) => void, enabled = true) {
  const handler = useRef(onScan)
  handler.current = onScan
  useEffect(() => {
    if (!enabled) return
    let buffer = ''
    let last = 0
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement
      if (target.closest('input, textarea, select, [contenteditable=true]')) return
      const now = Date.now()
      if (now - last > 60) buffer = ''
      last = now
      if (e.key === 'Enter') {
        if (buffer.length >= 4) handler.current(buffer)
        buffer = ''
      } else if (e.key.length === 1) buffer += e.key
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [enabled])
}
