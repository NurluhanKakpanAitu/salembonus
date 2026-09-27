import { useEffect, useRef, useState } from 'react'
import { Camera, Check, Loader2, Trash2, X } from 'lucide-react'
import type { Customer } from '../../lib/api'
import { initials } from '../../lib/format'
import { toSquareDataUrl } from '../../lib/image'
import { AVATAR_SEEDS, avatarSrc, presetAvatarUri, presetAvatarValue } from '../../lib/avatar'
import { useSetAvatar } from '../../lib/queries'
import { useT } from '../../lib/i18n'

/** Профиль суреті: құрылғыдан фото жүктеу, дайын аватар таңдау немесе өшіру. */
export function AvatarPicker({ me, name }: { me: Customer; name: string }) {
  const t = useT()
  const fileRef = useRef<HTMLInputElement>(null)
  const setAvatar = useSetAvatar()
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false)
    window.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      window.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [open])

  const upload = async (file: File | undefined) => {
    if (!file) return
    setError(null)
    try {
      await setAvatar.mutateAsync(await toSquareDataUrl(file))
      setOpen(false)
    } catch (err) {
      setError(err instanceof Error ? err.message : t('profile.photoFailed'))
    }
  }

  const choose = async (value: string | null) => {
    setError(null)
    try {
      await setAvatar.mutateAsync(value)
      setOpen(false)
    } catch (err) {
      setError(err instanceof Error ? err.message : t('profile.photoFailed'))
    }
  }

  return (
    <div className="flex flex-col items-center">
      <div className="relative">
        <div className="flex size-24 items-center justify-center overflow-hidden rounded-full bg-violet-soft text-2xl font-bold text-violet">
          {me.avatarUrl ? (
            <img src={avatarSrc(me.avatarUrl)} alt="" className="size-full object-cover" />
          ) : (
            initials(name) || '·'
          )}
        </div>
        <button
          type="button"
          aria-label={t('avatar.change')}
          disabled={setAvatar.isPending}
          onClick={() => setOpen(true)}
          className="absolute bottom-0 right-0 flex size-8 items-center justify-center rounded-full border-2 border-bg bg-brand text-white disabled:opacity-60"
        >
          {setAvatar.isPending ? <Loader2 size={15} className="animate-spin" /> : <Camera size={15} />}
        </button>
      </div>

      {error && <div className="mt-2 text-xs text-danger">{error}</div>}

      <input
        ref={fileRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        className="hidden"
        onChange={(e) => {
          void upload(e.target.files?.[0])
          e.target.value = ''
        }}
      />

      {open && (
        <div
          className="fixed inset-0 z-50 flex items-end justify-center bg-black/50"
          onClick={() => setOpen(false)}
          role="dialog"
          aria-modal="true"
        >
          <div
            className="max-h-[88vh] w-full max-w-[480px] overflow-y-auto rounded-t-[28px] bg-surface px-4 pb-[max(20px,env(safe-area-inset-bottom))] pt-3"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mx-auto mb-3 h-1 w-10 rounded-full bg-muted" />

            <div className="flex items-center justify-between">
              <h2 className="text-[15px] font-bold uppercase tracking-[0.04em]">{t('avatar.title')}</h2>
              <button
                type="button"
                aria-label={t('common.close')}
                onClick={() => setOpen(false)}
                className="flex size-8 items-center justify-center rounded-full bg-muted text-ink-2 active:scale-95"
              >
                <X size={17} />
              </button>
            </div>

            <div className="mt-3 flex flex-col gap-2">
              <button
                type="button"
                onClick={() => fileRef.current?.click()}
                className="flex items-center gap-3 rounded-2xl bg-muted px-4 py-3.5 text-left text-[15px] font-semibold active:scale-[0.99]"
              >
                <Camera size={18} className="text-ink-2" /> {t('profile.uploadPhoto')}
              </button>
              {me.avatarUrl && (
                <button
                  type="button"
                  onClick={() => void choose(null)}
                  className="flex items-center gap-3 rounded-2xl bg-muted px-4 py-3.5 text-left text-[15px] font-semibold text-danger active:scale-[0.99]"
                >
                  <Trash2 size={18} /> {t('profile.removePhoto')}
                </button>
              )}
            </div>

            {error && <p className="mt-3 text-[13px] text-danger">{error}</p>}

            <h3 className="mb-2 mt-5 text-[13px] font-semibold text-ink-2">{t('avatar.presets')}</h3>
            <ul className="grid grid-cols-4 gap-3 pb-1">
              {AVATAR_SEEDS.map((seed) => {
                const value = presetAvatarValue(seed)
                const active = me.avatarUrl === value
                return (
                  <li key={seed}>
                    <button
                      type="button"
                      aria-label={seed}
                      aria-pressed={active}
                      onClick={() => void choose(value)}
                      className={`relative block w-full overflow-hidden rounded-full active:scale-95 ${
                        active ? 'ring-2 ring-brand ring-offset-2 ring-offset-surface' : ''
                      }`}
                    >
                      <img src={presetAvatarUri(seed)} alt="" className="aspect-square w-full" loading="lazy" />
                      {active && (
                        <span className="absolute bottom-0 right-0 flex size-5 items-center justify-center rounded-full bg-brand text-white">
                          <Check size={12} />
                        </span>
                      )}
                    </button>
                  </li>
                )
              })}
            </ul>
          </div>
        </div>
      )}
    </div>
  )
}
