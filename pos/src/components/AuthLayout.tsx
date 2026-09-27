import type { ReactNode } from 'react'
import { Logo } from './Logo'
import { LanguageSelect } from './LanguageSelect'
import { useT } from '../lib/i18n'
import { useLanguageStore } from '../lib/language'

/**
 * Кіру және қалпына келтіру беттерінің ортақ қабығы (ТЗ «Авторизация» §2): сол жақта қара-көк
 * фондағы логотип, оң жақта форма. Мобильдіде — жоғарыда қара аймақ, төменде ақ карточка.
 */
export function AuthLayout({ children }: { children: ReactNode }) {
  const t = useT()
  const lang = useLanguageStore((s) => s.lang)
  const setLang = useLanguageStore((s) => s.set)

  return (
    <div className="flex min-h-full flex-col bg-surface lg:flex-row">
      <section className="relative flex shrink-0 flex-col items-center justify-center overflow-hidden bg-[radial-gradient(120%_90%_at_30%_20%,#0d3a8f_0%,#071a3d_45%,#040c1f_100%)] px-6 pb-16 pt-24 lg:w-1/2 lg:pb-0 lg:pt-0">
        <div className="absolute right-4 top-4 lg:hidden">
          <LanguageSelect value={lang} onChange={setLang} tone="light" />
        </div>
        <div className="scale-75 sm:scale-90 lg:scale-100">
          <Logo size={72} tone="light" tagline={t('login.tagline')} />
        </div>
      </section>

      <section className="relative -mt-8 flex flex-1 flex-col items-center rounded-t-[28px] bg-surface px-6 pb-10 pt-10 lg:mt-0 lg:justify-center lg:rounded-none lg:pt-0">
        <div className="absolute right-6 top-6 hidden lg:block">
          <LanguageSelect value={lang} onChange={setLang} />
        </div>
        {children}
      </section>
    </div>
  )
}
