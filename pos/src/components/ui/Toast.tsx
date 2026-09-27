import { useEffect } from 'react'
import { create } from 'zustand'
import { CheckCircle2, XCircle } from 'lucide-react'

interface ToastState {
  message: string | null
  tone: 'success' | 'error'
  id: number
}

const useToastStore = create<ToastState>(() => ({ message: null, tone: 'success', id: 0 }))

/** Қысқа хабарлама (ТЗ «Товар» §4: сәтті әрекеттен кейін қысқа хабар). */
export function toast(message: string, tone: 'success' | 'error' = 'success') {
  useToastStore.setState((s) => ({ message, tone, id: s.id + 1 }))
}

export function Toaster() {
  const { message, tone, id } = useToastStore()

  useEffect(() => {
    if (!message) return
    const timer = setTimeout(() => useToastStore.setState({ message: null }), 3200)
    return () => clearTimeout(timer)
  }, [id, message])

  if (!message) return null
  const Icon = tone === 'success' ? CheckCircle2 : XCircle
  return (
    <div role="status" className="fixed bottom-6 left-1/2 z-[70] -translate-x-1/2">
      <div className={`flex items-center gap-2.5 rounded-xl px-4 py-3 text-[14px] font-medium text-white shadow-lg ${
        tone === 'success' ? 'bg-ink' : 'bg-danger'
      }`}>
        <Icon size={18} className={tone === 'success' ? 'text-[#4ade80]' : ''} /> {message}
      </div>
    </div>
  )
}
