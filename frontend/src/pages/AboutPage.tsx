import { BackHeader } from '../components/BackHeader'
import { LogoMark } from '../components/Brand'
import { useT } from '../lib/i18n'

const APP_VERSION = '1.0.0'
const SITE = 'salembonus.kz'

export function AboutPage() {
  const t = useT()

  return (
    <>
      <BackHeader title={t('profile.about')} fallback="/profile" />

      <div className="mt-4 flex flex-col items-center gap-4">
        <LogoMark size={76} />
        <div className="text-center">
          <div className="text-[19px] font-extrabold">SalemBonus</div>
          <div className="mt-0.5 text-xs text-ink-2">{t('about.version', { version: APP_VERSION })}</div>
        </div>

        <p className="rounded-card bg-surface p-4 text-[13px] leading-relaxed text-ink-2">{t('about.text')}</p>

        <a
          href={`https://${SITE}`}
          target="_blank"
          rel="noreferrer"
          className="text-[13px] font-semibold text-brand"
        >
          {SITE}
        </a>
      </div>
    </>
  )
}
