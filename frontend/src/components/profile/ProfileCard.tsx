import { useRef, useState } from 'react'
import { Camera, Loader2, Trash2 } from 'lucide-react'
import type { Customer } from '../../lib/api'
import { initials } from '../../lib/format'
import { toSquareDataUrl } from '../../lib/image'
import { useSetAvatar } from '../../lib/queries'

export function ProfileCard({ me }: { me: Customer }) {
  const fileRef = useRef<HTMLInputElement>(null)
  const setAvatar = useSetAvatar()
  const [error, setError] = useState<string | null>(null)

  const pick = async (file: File | undefined) => {
    if (!file) return
    setError(null)
    try {
      await setAvatar.mutateAsync(await toSquareDataUrl(file))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Фотоны жүктеу мүмкін болмады')
    }
  }

  return (
    <section className="rounded-card bg-surface p-5">
      <div className="flex flex-col items-center text-center">
        <div className="relative">
          <div className="flex size-24 items-center justify-center overflow-hidden rounded-full bg-violet-soft text-2xl font-bold text-violet">
            {me.avatarUrl ? (
              <img src={me.avatarUrl} alt="" className="size-full object-cover" />
            ) : (
              initials(me.fullName)
            )}
          </div>
          <button
            type="button"
            aria-label="Фото жүктеу"
            disabled={setAvatar.isPending}
            onClick={() => fileRef.current?.click()}
            className="absolute bottom-0 right-0 flex size-8 items-center justify-center rounded-full border-2 border-surface bg-brand text-white disabled:opacity-60"
          >
            {setAvatar.isPending ? <Loader2 size={15} className="animate-spin" /> : <Camera size={15} />}
          </button>
          <input
            ref={fileRef}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            className="hidden"
            onChange={(e) => {
              void pick(e.target.files?.[0])
              e.target.value = ''
            }}
          />
        </div>

        <div className="mt-3 text-[17px] font-bold leading-tight">{me.fullName}</div>
        <div className="mt-1 text-sm text-ink-2">{formatPhone(me.phone)}</div>

        {me.avatarUrl && (
          <button
            type="button"
            disabled={setAvatar.isPending}
            onClick={() => setAvatar.mutate(null)}
            className="mt-3 inline-flex items-center gap-1.5 text-xs font-medium text-ink-2 disabled:opacity-50"
          >
            <Trash2 size={13} /> Фотоны өшіру
          </button>
        )}

        {error && <div className="mt-3 text-xs text-danger">{error}</div>}
      </div>
    </section>
  )
}

function formatPhone(p: string) {
  const d = p.replace(/\D/g, '')
  if (d.length === 11) return `+${d[0]} ${d.slice(1, 4)} ${d.slice(4, 7)} ${d.slice(7, 9)} ${d.slice(9)}`
  return p
}
