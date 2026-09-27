import { useRef, useState } from 'react'
import { Camera, Loader2, Trash2 } from 'lucide-react'
import type { Customer } from '../../lib/api'
import { initials } from '../../lib/format'
import { toSquareDataUrl } from '../../lib/image'
import { useSetAvatar } from '../../lib/queries'
import { useT } from '../../lib/i18n'

/** Профиль фотосы: құрылғыдан таңдап жүктеу немесе өшіру. */
export function AvatarPicker({ me, name }: { me: Customer; name: string }) {
  const t = useT()
  const fileRef = useRef<HTMLInputElement>(null)
  const setAvatar = useSetAvatar()
  const [error, setError] = useState<string | null>(null)

  const pick = async (file: File | undefined) => {
    if (!file) return
    setError(null)
    try {
      await setAvatar.mutateAsync(await toSquareDataUrl(file))
    } catch (err) {
      setError(err instanceof Error ? err.message : t('profile.photoFailed'))
    }
  }

  return (
    <div className="flex flex-col items-center">
      <div className="relative">
        <div className="flex size-24 items-center justify-center overflow-hidden rounded-full bg-violet-soft text-2xl font-bold text-violet">
          {me.avatarUrl ? (
            <img src={me.avatarUrl} alt="" className="size-full object-cover" />
          ) : (
            initials(name) || '·'
          )}
        </div>
        <button
          type="button"
          aria-label={t('profile.uploadPhoto')}
          disabled={setAvatar.isPending}
          onClick={() => fileRef.current?.click()}
          className="absolute bottom-0 right-0 flex size-8 items-center justify-center rounded-full border-2 border-bg bg-brand text-white disabled:opacity-60"
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

      {me.avatarUrl && (
        <button
          type="button"
          disabled={setAvatar.isPending}
          onClick={() => setAvatar.mutate(null)}
          className="mt-2.5 inline-flex items-center gap-1.5 text-xs font-medium text-ink-2 disabled:opacity-50"
        >
          <Trash2 size={13} /> {t('profile.removePhoto')}
        </button>
      )}

      {error && <div className="mt-2 text-xs text-danger">{error}</div>}
    </div>
  )
}
