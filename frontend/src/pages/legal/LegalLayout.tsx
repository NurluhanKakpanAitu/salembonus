import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { LogoMark } from '../../components/Brand'
import { useLanguage } from '../../lib/language'
import { useT } from '../../lib/i18n'

/**
 * Құқықтық беттердің ортақ қабығы. Бұл беттер кіру талап етпейді: дүкендердің талабы
 * бойынша қосымшаны орнатпай-ақ ашылуы керек.
 */
export function LegalLayout({ title, updated, children }: {
  title: string
  /** Соңғы өзгерген күні. */
  updated: string
  children: ReactNode
}) {
  const t = useT()

  return (
    <div className="mx-auto min-h-dvh w-full max-w-[720px] px-4 pb-12 pt-6">
      <Link to="/" className="inline-flex items-center gap-2.5">
        <LogoMark size={34} />
        <span className="text-[17px] font-extrabold">SalemBonus</span>
      </Link>

      <h1 className="mt-6 text-[26px] font-extrabold leading-tight tracking-tight">{title}</h1>
      <p className="mt-1 text-[13px] text-ink-3">{t('legal.updated', { date: updated })}</p>

      <div className="mt-6 flex flex-col gap-5 text-[15px] leading-relaxed text-ink">{children}</div>

      <LegalFooter />
    </div>
  )
}

/** Бөлім: тақырып пен мәтін. */
export function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section>
      <h2 className="mb-1.5 text-[17px] font-bold">{title}</h2>
      <div className="flex flex-col gap-2 text-[15px] leading-relaxed text-ink-2">{children}</div>
    </section>
  )
}

function LegalFooter() {
  const t = useT()
  const [lang] = useLanguage()
  const links = [
    { to: '/privacy', label: t('legal.privacy') },
    { to: '/terms', label: t('legal.terms') },
    { to: '/delete-account', label: t('legal.deleteAccount') },
  ]

  return (
    <footer className="mt-10 border-t border-line pt-5">
      <nav className="flex flex-wrap gap-x-4 gap-y-2">
        {links.map((l) => (
          <Link key={l.to} to={l.to} className="text-[13px] font-semibold text-brand">
            {l.label}
          </Link>
        ))}
      </nav>
      <p className="mt-3 text-[12px] text-ink-3">
        {lang === 'kk' ? 'SalemBonus, Қазақстан' : 'SalemBonus, Казахстан'} · support@salembonus.kz
      </p>
    </footer>
  )
}
