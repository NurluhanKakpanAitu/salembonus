import { Globe } from 'lucide-react'
import { LANGUAGES, useLanguage } from '../../lib/language'
import { useT } from '../../lib/i18n'

/**
 * Тіл таңдау профиль бетінде бірден көрініп тұрады: бөлек бетке кіріп іздеудің қажеті жоқ.
 */
export function LanguagePicker() {
  const [lang, setLang] = useLanguage()
  const t = useT()

  return (
    <section className="rounded-card bg-surface px-4 py-3.5">
      <div className="flex items-center gap-3.5">
        <Globe size={22} strokeWidth={1.8} className="text-ink-2" />
        <div className="flex-1 text-[15px] font-semibold">{t('profile.language')}</div>
        <div className="flex rounded-xl bg-bg p-1">
          {LANGUAGES.map((l) => (
            <button
              key={l.value}
              type="button"
              onClick={() => setLang(l.value)}
              aria-pressed={lang === l.value}
              className={`rounded-lg px-3 py-1.5 text-[13px] font-semibold transition ${
                lang === l.value ? 'bg-brand text-white' : 'text-ink-2'
              }`}
            >
              {l.label}
            </button>
          ))}
        </div>
      </div>
    </section>
  )
}
