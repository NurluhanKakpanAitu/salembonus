import { useEffect, useRef, useState } from 'react'
import jsQR from 'jsqr'
import { Keyboard, X } from 'lucide-react'

type Mode = 'camera' | 'manual'

/**
 * Камерадан QR оқитын толық экранды сканер.
 * Камера қолжетімсіз болса қолмен код енгізуге ауысады.
 */
export function QrScanner({
  open,
  onClose,
  onDetect,
  busy,
  error,
}: {
  open: boolean
  onClose: () => void
  onDetect: (code: string) => void
  busy?: boolean
  error?: string | null
}) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const [mode, setMode] = useState<Mode>('camera')
  const [cameraError, setCameraError] = useState<string | null>(null)
  const [manualCode, setManualCode] = useState('')
  const detectedRef = useRef(false)

  useEffect(() => {
    if (!open) {
      setMode('camera')
      setCameraError(null)
      setManualCode('')
      detectedRef.current = false
      return
    }
    if (mode !== 'camera') return

    let stream: MediaStream | null = null
    let frame = 0
    let cancelled = false

    const tick = () => {
      frame = requestAnimationFrame(tick)
      const video = videoRef.current
      const canvas = canvasRef.current
      if (!video || !canvas || video.readyState !== video.HAVE_ENOUGH_DATA || detectedRef.current) return

      const w = video.videoWidth
      const h = video.videoHeight
      if (!w || !h) return
      canvas.width = w
      canvas.height = h
      const ctx = canvas.getContext('2d', { willReadFrequently: true })
      if (!ctx) return
      ctx.drawImage(video, 0, 0, w, h)
      const result = jsQR(ctx.getImageData(0, 0, w, h).data, w, h, { inversionAttempts: 'dontInvert' })
      if (result?.data) {
        detectedRef.current = true
        navigator.vibrate?.(60)
        onDetect(result.data.trim())
      }
    }

    ;(async () => {
      try {
        stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: { ideal: 'environment' } },
          audio: false,
        })
        if (cancelled) {
          stream.getTracks().forEach((t) => t.stop())
          return
        }
        const video = videoRef.current
        if (!video) return
        video.srcObject = stream
        video.setAttribute('playsinline', 'true')
        await video.play()
        frame = requestAnimationFrame(tick)
      } catch {
        setCameraError('Камераға қолжетімділік берілмеді. Кодты қолмен енгізіңіз.')
        setMode('manual')
      }
    })()

    return () => {
      cancelled = true
      cancelAnimationFrame(frame)
      stream?.getTracks().forEach((t) => t.stop())
    }
  }, [open, mode, onDetect])

  // Сәтсіз әрекеттен кейін қайта сканерлеуге рұқсат
  useEffect(() => {
    if (error) detectedRef.current = false
  }, [error])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-black">
      <div className="flex items-center justify-between px-4 pt-[max(16px,env(safe-area-inset-top))] text-white">
        <div className="text-[15px] font-semibold">Дүкен QR кодын сканерлеңіз</div>
        <button type="button" aria-label="Жабу" onClick={onClose} className="flex size-10 items-center justify-center rounded-full bg-white/15">
          <X size={20} />
        </button>
      </div>

      {mode === 'camera' ? (
        <div className="relative flex flex-1 items-center justify-center overflow-hidden">
          <video ref={videoRef} className="absolute inset-0 size-full object-cover" muted playsInline />
          <canvas ref={canvasRef} className="hidden" />
          <div className="relative size-[70vw] max-w-[300px] rounded-[28px] border-4 border-white/80 shadow-[0_0_0_100vmax_rgba(0,0,0,0.45)]" />
          <div className="absolute bottom-10 left-0 right-0 px-8 text-center text-sm text-white/80">
            {busy ? 'Дүкен қосылуда…' : 'QR кодты жақтау ішіне келтіріңіз'}
          </div>
        </div>
      ) : (
        <div className="flex flex-1 flex-col justify-center px-6">
          <label className="text-sm text-white/70" htmlFor="store-code">Дүкен коды</label>
          <input
            id="store-code"
            autoFocus
            autoCapitalize="characters"
            value={manualCode}
            onChange={(e) => setManualCode(e.target.value.toUpperCase())}
            placeholder="MKMAUTO"
            className="mt-2 h-14 w-full rounded-2xl bg-white/10 px-4 text-lg font-semibold tracking-widest text-white outline-none placeholder:text-white/30"
          />
          <p className="mt-2 text-xs text-white/50">Код дүкендегі плакатта QR кодтың астында жазылған</p>
          <button
            type="button"
            disabled={manualCode.trim().length < 3 || busy}
            onClick={() => onDetect(manualCode.trim())}
            className="mt-5 h-14 w-full rounded-2xl bg-brand text-[15px] font-semibold text-white disabled:opacity-50"
          >
            {busy ? 'Қосылуда…' : 'Қосу'}
          </button>
        </div>
      )}

      {(error || cameraError) && (
        <div className="mx-4 mb-3 rounded-2xl bg-danger/90 px-4 py-3 text-center text-sm text-white">
          {error || cameraError}
        </div>
      )}

      <div className="pb-[max(20px,env(safe-area-inset-bottom))] pt-2 text-center">
        <button
          type="button"
          onClick={() => setMode(mode === 'camera' ? 'manual' : 'camera')}
          className="inline-flex items-center gap-2 text-sm font-medium text-white/80"
        >
          <Keyboard size={16} />
          {mode === 'camera' ? 'Кодты қолмен енгізу' : 'Камерамен сканерлеу'}
        </button>
      </div>
    </div>
  )
}
