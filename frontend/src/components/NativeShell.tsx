import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { WifiOff } from 'lucide-react'
import { initNative } from '../lib/native'
import { useT } from '../lib/i18n'

/**
 * Қосымшаның жалпы қабаты: нативті баптау (бір рет) және интернет жоқ кездегі жолақ.
 * Жолақ сайтта да көрінеді — PWA да желісіз ашылады.
 */
export function NativeShell() {
  const navigate = useNavigate()
  const online = useOnline()
  const t = useT()

  useEffect(() => {
    void initNative(() => {
      // React Router тарихтағы орынды history.state.idx-те сақтайды: 0 — бірінші бет.
      if (((window.history.state as { idx?: number } | null)?.idx ?? 0) > 0) {
        navigate(-1)
        return true
      }
      return false
    })
    // Бір рет қана: батырма тыңдаушысы қосымшаның бүкіл өміріне.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (online) return null
  return (
    <div
      role="status"
      className="fixed inset-x-0 top-0 z-[60] mx-auto flex max-w-[480px] items-center justify-center gap-2 bg-ink px-4 pb-2 pt-[max(8px,env(safe-area-inset-top))] text-[13px] font-semibold text-bg"
    >
      <WifiOff size={15} />
      {t('offline.banner')}
    </div>
  )
}

function useOnline() {
  const [online, setOnline] = useState(() => navigator.onLine)
  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    window.addEventListener('online', update)
    window.addEventListener('offline', update)
    return () => {
      window.removeEventListener('online', update)
      window.removeEventListener('offline', update)
    }
  }, [])
  return online
}
