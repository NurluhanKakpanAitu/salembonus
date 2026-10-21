import { useEffect, useRef, useState, type ReactNode } from 'react'
import {
  ArrowRight, BarChart3, Boxes, Calculator, Check, ChevronDown, Cloud, Gift, Layers, Loader2, Menu, Package, PartyPopper, QrCode, Bell, ShieldCheck,
  ShoppingCart, Sparkles, Store, Upload, Users, Wallet, X, Crown, Smartphone, Receipt, RefreshCw,
} from 'lucide-react'
import { COPY, type Lang } from './copy'
import { Phone, PosMockup } from './Mockups'
import { AppMark, SalemWordmark } from './Brand'

const LANG_KEY = 'salemtech.lang'
const POS_URL = 'https://pos.salemtech.kz'
const BONUS_URL = 'https://salembonus.kz'
const MODULE_ICONS: Record<string, ReactNode> = {
  cashier: <Calculator size={20} />, products: <Package size={20} />, warehouse: <Boxes size={20} />, sales: <ShoppingCart size={20} />,
  statistics: <BarChart3 size={20} />, finance: <Wallet size={20} />, bonus: <Gift size={20} />, clients: <Users size={20} />,
}

interface Plan { id: string; name: string; monthlyPrice: number; modules: string[]; description: string | null }

function initialLang(): Lang {
  try {
    const saved = localStorage.getItem(LANG_KEY)
    if (saved === 'ru' || saved === 'kk') return saved
  } catch { /* жеке режим */ }
  return navigator.language?.toLowerCase().startsWith('kk') ? 'kk' : 'ru'
}

/** Экранға шыққанда жұмсақ пайда болу (қозғалысты азайту баптауы құрметтеледі — CSS-те). */
function Reveal({ children, className = '', delay = 0 }: { children: ReactNode; className?: string; delay?: number }) {
  const ref = useRef<HTMLDivElement>(null)
  const [shown, setShown] = useState(false)
  useEffect(() => {
    const el = ref.current
    if (!el || typeof IntersectionObserver === 'undefined') { setShown(true); return }
    const io = new IntersectionObserver(([e]) => { if (e.isIntersecting) { setShown(true); io.disconnect() } }, { threshold: 0.12 })
    io.observe(el)
    return () => io.disconnect()
  }, [])
  return <div ref={ref} className={`reveal ${shown ? 'is-visible' : ''} ${className}`} style={{ transitionDelay: `${delay}ms` }}>{children}</div>
}

function Eyebrow({ children, tone = 'blue' }: { children: ReactNode; tone?: 'blue' | 'red' }) {
  return <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-[13px] font-semibold ${tone === 'red' ? 'bg-[#e31e24]/10 text-[#d0161c]' : 'bg-[#1570ff]/10 text-[#1570ff]'}`}>{children}</span>
}

function SectionTitle({ eyebrow, brand, title, subtitle, tone, center = true }: {
  eyebrow: string; brand?: ReactNode; title: string; subtitle?: string; tone?: 'blue' | 'red'; center?: boolean
}) {
  return (
    <Reveal className={`flex flex-col gap-3 ${center ? 'items-center text-center' : ''}`}>
      {brand ?? <Eyebrow tone={tone}>{eyebrow}</Eyebrow>}
      <h2 className="max-w-3xl text-[30px] font-extrabold leading-[1.15] tracking-tight text-[#0b1220] sm:text-[40px]">{title}</h2>
      {subtitle && <p className="max-w-2xl text-[16px] leading-relaxed text-[#5b6576] sm:text-[18px]">{subtitle}</p>}
    </Reveal>
  )
}

/** SalemTech белгісі — SalemPos пен SalemBonus белгілерімен бір отбасы: «Salem» + көк «Tech», «Sa» астында күлкі-дуга. */
function Logo({ dark }: { dark?: boolean }) {
  return <SalemWordmark word="Tech" size={24} tone={dark ? 'light' : 'dark'} color={dark ? '#3b9bff' : '#1570ff'} />
}

/** Өнім белгісі: қосымша белгісі + өнім сөзі (SalemPos, SalemBonus). */
function ProductLogo({ word, dark, id }: { word: string; dark?: boolean; id: string }) {
  return (
    <span className="inline-flex items-center gap-2.5">
      <AppMark size={36} id={id} />
      <SalemWordmark word={word} size={24} tone={dark ? 'light' : 'dark'} color={dark ? '#3b9bff' : '#1570ff'} />
    </span>
  )
}

/** salemtech.kz: SalemPos (касса мен есеп) және SalemBonus (клиенттің бонус қосымшасы) — бір лендинг, орысша/қазақша. */
export function Landing() {
  const [lang, setLang] = useState<Lang>(initialLang)
  const [menu, setMenu] = useState(false)
  const [plan, setPlan] = useState<string | null>(null)
  const c = COPY[lang]
  useEffect(() => {
    document.documentElement.lang = lang
    try { localStorage.setItem(LANG_KEY, lang) } catch { /* жеке режим */ }
  }, [lang])

  const nav = [
    { href: '#how', label: c.nav.how }, { href: '#features', label: c.nav.features }, { href: '#bonus', label: c.nav.bonus },
    { href: '#pricing', label: c.nav.pricing }, { href: '#faq', label: c.nav.faq },
  ]
  const choosePlan = (name: string) => { setPlan(name); document.getElementById('request')?.scrollIntoView({ behavior: 'smooth' }) }

  return (
    <div className="min-h-full overflow-x-clip bg-white text-[#0b1220]">
      {/* ---------- Шапка ---------- */}
      <header className="sticky top-0 z-40 border-b border-[#e6e9f0]/80 bg-white/80 backdrop-blur-xl">
        <div className="mx-auto flex h-16 max-w-7xl items-center gap-6 px-4 sm:px-6">
          <a href="#top" aria-label="SalemTech"><Logo /></a>
          <nav className="hidden flex-1 items-center gap-4 lg:flex xl:gap-6">
            {nav.map((n) => <a key={n.href} href={n.href} className="whitespace-nowrap text-[14px] font-medium text-[#5b6576] transition-colors hover:text-[#0b1220]">{n.label}</a>)}
          </nav>
          <div className="ml-auto flex items-center gap-2">
            <div className="flex rounded-lg bg-[#f3f5f9] p-0.5 text-[12px] font-bold" role="group" aria-label="Language">
              {(['ru', 'kk'] as Lang[]).map((l) => (
                <button key={l} type="button" onClick={() => setLang(l)} aria-pressed={lang === l}
                  className={`rounded-md px-2 py-1 ${lang === l ? 'bg-white text-[#0b1220] shadow-sm' : 'text-[#5b6576]'}`}>{l === 'ru' ? 'RU' : 'ҚАЗ'}</button>
              ))}
            </div>
            <a href={POS_URL} className="hidden rounded-xl px-3 py-2 text-[14px] font-semibold text-[#0b1220] hover:bg-[#f3f5f9] sm:inline-flex">{c.nav.login}</a>
            <a href="#request" className="hidden rounded-xl bg-[#0b1220] px-4 py-2 text-[14px] font-semibold text-white transition-colors hover:bg-[#1570ff] sm:inline-flex">{c.nav.cta}</a>
            <button type="button" className="rounded-lg p-2 lg:hidden" onClick={() => setMenu((m) => !m)} aria-label="Menu" aria-expanded={menu}>{menu ? <X size={22} /> : <Menu size={22} />}</button>
          </div>
        </div>
        {menu && (
          <div className="border-t border-[#e6e9f0] bg-white px-4 py-3 lg:hidden">
            {nav.map((n) => <a key={n.href} href={n.href} onClick={() => setMenu(false)} className="block rounded-lg px-2 py-2.5 text-[15px] font-medium">{n.label}</a>)}
            <div className="mt-2 grid grid-cols-2 gap-2">
              <a href={POS_URL} className="rounded-xl border border-[#e6e9f0] py-2.5 text-center text-[14px] font-semibold">{c.nav.login}</a>
              <a href="#request" onClick={() => setMenu(false)} className="rounded-xl bg-[#0b1220] py-2.5 text-center text-[14px] font-semibold text-white">{c.nav.cta}</a>
            </div>
          </div>
        )}
      </header>

      {/* ---------- Hero ---------- */}
      <section id="top" className="relative">
        <div className="pointer-events-none absolute inset-0 -z-10 overflow-hidden">
          <div className="bg-grid absolute inset-0 [mask-image:radial-gradient(ellipse_at_top,black,transparent_70%)]" />
          <div className="absolute -left-40 -top-40 size-[520px] rounded-full bg-[#1570ff]/20 blur-[120px]" />
          <div className="absolute -right-32 top-40 size-[460px] rounded-full bg-[#e31e24]/15 blur-[120px]" />
        </div>
        <div className="mx-auto grid max-w-7xl items-center gap-12 px-4 pb-20 pt-12 sm:px-6 lg:grid-cols-[1.05fr_1fr] lg:pb-28 lg:pt-20">
          <Reveal className="flex flex-col items-start gap-6">
            <Eyebrow><Sparkles size={14} />{c.hero.badge}</Eyebrow>
            <h1 className="text-[40px] font-extrabold leading-[1.05] tracking-tight sm:text-[56px] lg:text-[64px]">
              {c.hero.title1}<br />
              <span className="bg-gradient-to-r from-[#1570ff] via-[#6d4cff] to-[#e31e24] bg-clip-text text-transparent">{c.hero.title2}</span>
            </h1>
            <p className="max-w-xl text-[17px] leading-relaxed text-[#5b6576] sm:text-[19px]">{c.hero.subtitle}</p>
            <div className="flex flex-wrap gap-3">
              <a href="#request" className="group inline-flex h-13 items-center gap-2 rounded-2xl bg-[#1570ff] px-6 text-[16px] font-semibold text-white shadow-[0_12px_30px_-10px_rgba(21,112,255,0.7)] transition-transform hover:-translate-y-0.5">
                {c.hero.primary}<ArrowRight size={18} className="transition-transform group-hover:translate-x-0.5" />
              </a>
              <a href="#features" className="inline-flex h-13 items-center rounded-2xl border border-[#d9dee8] bg-white px-6 text-[16px] font-semibold transition-colors hover:border-[#0b1220]">{c.hero.secondary}</a>
            </div>
            <ul className="flex flex-col gap-2 text-[15px] text-[#3a4456] sm:flex-row sm:flex-wrap sm:gap-x-6">
              {c.hero.points.map((p) => <li key={p} className="flex items-center gap-2"><span className="flex size-5 items-center justify-center rounded-full bg-[#16a34a]/12 text-[#16a34a]"><Check size={13} strokeWidth={3} /></span>{p}</li>)}
            </ul>
          </Reveal>
          <Reveal delay={150} className="relative">
            <div className="relative mr-10 sm:mr-24">
              <PosMockup c={c} />
              <div className="float absolute -left-4 bottom-10 hidden items-center gap-3 rounded-2xl bg-white p-3 pr-4 shadow-[0_18px_40px_-12px_rgba(11,31,68,0.35)] ring-1 ring-black/5 sm:flex">
                <span className="flex size-10 items-center justify-center rounded-xl bg-[#16a34a]/12 text-[#16a34a]"><RefreshCw size={18} /></span>
                <span><span className="block text-[13px] font-bold">{c.hero.chipStock}</span><span className="block text-[12px] text-[#5b6576]">{c.hero.chipStockSub}</span></span>
              </div>
            </div>
            <div className="absolute -bottom-10 right-0 w-[150px] sm:w-[190px]">
              <Phone src="/landing/app-home.jpg" alt="SalemBonus" />
              <div className="float absolute -left-16 top-16 flex items-center gap-2.5 rounded-2xl bg-white p-2.5 pr-3.5 shadow-[0_18px_40px_-12px_rgba(227,30,36,0.45)] ring-1 ring-black/5 [animation-delay:1.5s]">
                <span className="flex size-9 items-center justify-center rounded-xl bg-[#e31e24] text-white"><Gift size={17} /></span>
                <span><span className="block text-[15px] font-extrabold text-[#16a34a]">{c.hero.chipBonus}</span><span className="block text-[11px] text-[#5b6576]">{c.hero.chipBonusSub}</span></span>
              </div>
            </div>
          </Reveal>
        </div>
      </section>

      {/* ---------- Кімге ---------- */}
      <section className="border-y border-[#eef1f6] bg-[#fafbfd]">
        <div className="mx-auto flex max-w-7xl flex-col items-center gap-4 px-4 py-8 sm:px-6 lg:flex-row">
          <span className="shrink-0 text-[13px] font-semibold uppercase tracking-wider text-[#98a1b1]">{c.fits.title}</span>
          <div className="flex flex-wrap justify-center gap-2 lg:justify-start">
            {c.fits.items.map((i) => <span key={i} className="rounded-full border border-[#e6e9f0] bg-white px-3.5 py-1.5 text-[14px] font-medium text-[#3a4456]">{i}</span>)}
          </div>
        </div>
      </section>

      {/* ---------- Қалай жұмыс істейді ---------- */}
      <section id="how" className="scroll-mt-20 py-20 sm:py-28">
        <div className="mx-auto max-w-7xl px-4 sm:px-6">
          <SectionTitle eyebrow={c.how.eyebrow} title={c.how.title} subtitle={c.how.subtitle} />
          <div className="relative mt-14 grid gap-5 md:grid-cols-3">
            <div className="absolute left-[16%] right-[16%] top-9 hidden h-px bg-gradient-to-r from-[#1570ff] via-[#6d4cff] to-[#e31e24] opacity-40 md:block" />
            {c.how.steps.map((s, i) => (
              <Reveal key={s.title} delay={i * 120} className="relative flex flex-col items-center rounded-3xl border border-[#e6e9f0] bg-white p-7 text-center shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
                <span className={`relative z-10 flex size-[72px] items-center justify-center rounded-2xl text-white shadow-lg ${['bg-[#1570ff]', 'bg-[#6d4cff]', 'bg-[#e31e24]'][i]}`}>
                  {[<Receipt key="r" size={30} />, <BarChart3 key="b" size={30} />, <Smartphone key="s" size={30} />][i]}
                </span>
                <span className="mt-5 text-[12px] font-bold uppercase tracking-wider text-[#98a1b1]">0{i + 1}</span>
                <h3 className="mt-1 text-[20px] font-bold">{s.title}</h3>
                <p className="mt-2 text-[15px] leading-relaxed text-[#5b6576]">{s.text}</p>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ---------- SalemPos модульдері ---------- */}
      <section id="features" className="scroll-mt-20 bg-[#f6f8fc] py-20 sm:py-28">
        <div className="mx-auto max-w-7xl px-4 sm:px-6">
          <SectionTitle eyebrow={c.modules.eyebrow} brand={<ProductLogo word="Pos" id="mark-pos" />} title={c.modules.title} subtitle={c.modules.subtitle} />
          <div className="mt-14 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {c.modules.items.map((m, i) => (
              <Reveal key={m.key} delay={(i % 4) * 80} className="group flex flex-col rounded-3xl border border-[#e6e9f0] bg-white p-6 transition-all hover:-translate-y-1 hover:border-[#1570ff]/40 hover:shadow-[0_20px_40px_-20px_rgba(21,112,255,0.35)]">
                <span className={`flex size-12 items-center justify-center rounded-2xl ${m.key === 'bonus' ? 'bg-[#e31e24]/10 text-[#e31e24]' : 'bg-[#1570ff]/10 text-[#1570ff]'} transition-transform group-hover:scale-110`}>{MODULE_ICONS[m.key]}</span>
                <h3 className="mt-5 text-[19px] font-bold">{m.title}</h3>
                <p className="mt-1 text-[14px] text-[#5b6576]">{m.text}</p>
                <ul className="mt-4 flex flex-col gap-2 text-[14px] text-[#3a4456]">
                  {m.points.map((p) => <li key={p} className="flex gap-2"><Check size={16} className="mt-0.5 shrink-0 text-[#1570ff]" />{p}</li>)}
                </ul>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ---------- SalemBonus ---------- */}
      <section id="bonus" className="scroll-mt-20 relative overflow-hidden bg-[#0b1220] py-20 text-white sm:py-28">
        <div className="pointer-events-none absolute inset-0">
          <div className="absolute -right-40 top-0 size-[560px] rounded-full bg-[#e31e24]/30 blur-[140px]" />
          <div className="absolute -left-40 bottom-0 size-[480px] rounded-full bg-[#1570ff]/25 blur-[140px]" />
        </div>
        <div className="relative mx-auto grid max-w-7xl items-center gap-14 px-4 sm:px-6 lg:grid-cols-2">
          <Reveal className="flex flex-col items-start gap-5">
            <ProductLogo word="Bonus" dark id="mark-bonus" />
            <h2 className="text-[32px] font-extrabold leading-[1.12] tracking-tight sm:text-[44px]">{c.bonus.title}</h2>
            <p className="max-w-xl text-[17px] leading-relaxed text-white/70">{c.bonus.subtitle}</p>
            <div className="flex flex-wrap gap-2">
              {c.bonus.statuses.map((s, i) => (
                <span key={s} className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-[13px] font-semibold ${i === 3 ? 'bg-[#f5b301]/15 text-[#f5b301] ring-1 ring-[#f5b301]/40' : 'bg-white/10 text-white/85'}`}>
                  {i === 3 && <Crown size={13} />}{s} · {[2, 4, 6, 8][i]}%
                </span>
              ))}
            </div>
            <div className="mt-2 grid w-full gap-4 sm:grid-cols-2">
              {[{ title: c.bonus.forClients, items: c.bonus.clients, icons: [<Layers key="1" size={16} />, <QrCode key="2" size={16} />, <Bell key="3" size={16} />] },
                { title: c.bonus.forBusiness, items: c.bonus.business, icons: [<Crown key="1" size={16} />, <PartyPopper key="2" size={16} />, <Sparkles key="3" size={16} />] }].map((col) => (
                <div key={col.title} className="rounded-3xl bg-white/[0.06] p-5 ring-1 ring-white/10">
                  <div className="mb-3 text-[13px] font-bold uppercase tracking-wider text-white/50">{col.title}</div>
                  <ul className="flex flex-col gap-3 text-[15px]">
                    {col.items.map((t, i) => <li key={t} className="flex gap-3"><span className="mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-lg bg-[#e31e24] text-white">{col.icons[i]}</span><span className="text-white/85">{t}</span></li>)}
                  </ul>
                </div>
              ))}
            </div>
            <a href={BONUS_URL} target="_blank" rel="noopener" className="mt-2 inline-flex h-12 items-center gap-2 rounded-2xl bg-white px-5 text-[15px] font-semibold text-[#0b1220] transition-transform hover:-translate-y-0.5">
              {c.bonus.open}<ArrowRight size={17} />
            </a>
          </Reveal>
          <Reveal delay={150} className="relative mx-auto flex w-full max-w-md justify-center gap-5 sm:gap-7">
            <Phone src="/landing/app-cards.jpg" alt="SalemBonus" className="mt-14 w-[46%] rotate-[-4deg]" />
            <Phone src="/landing/app-home.jpg" alt="SalemBonus" className="w-[46%] rotate-[3deg]" />
          </Reveal>
        </div>
      </section>

      {/* ---------- Неге ---------- */}
      <section className="py-20 sm:py-28">
        <div className="mx-auto max-w-7xl px-4 sm:px-6">
          <SectionTitle eyebrow={c.why.eyebrow} title={c.why.title} />
          <div className="mt-14 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {c.why.items.map((w, i) => (
              <Reveal key={w.title} delay={i * 80} className="rounded-3xl bg-gradient-to-b from-[#f6f8fc] to-white p-6 ring-1 ring-[#e6e9f0]">
                <span className="flex size-11 items-center justify-center rounded-xl bg-[#0b1220] text-white">{[<Cloud key="c" size={20} />, <ShieldCheck key="s" size={20} />, <Store key="t" size={20} />, <Upload key="u" size={20} />][i]}</span>
                <h3 className="mt-4 text-[18px] font-bold">{w.title}</h3>
                <p className="mt-2 text-[14px] leading-relaxed text-[#5b6576]">{w.text}</p>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      <Pricing c={c} onChoose={choosePlan} />
      <Faq c={c} />
      <RequestForm c={c} lang={lang} plan={plan} />

      {/* ---------- Подвал ---------- */}
      <footer className="bg-[#0b1220] text-white/70">
        <div className="mx-auto grid max-w-7xl gap-10 px-4 py-14 sm:px-6 md:grid-cols-[1.5fr_1fr_1fr]">
          <div className="flex flex-col gap-3"><Logo dark /><p className="max-w-sm text-[14px]">{c.footer.tagline}</p></div>
          <div className="flex flex-col gap-2 text-[14px]">
            <span className="mb-1 text-[12px] font-bold uppercase tracking-wider text-white/40">{c.footer.product}</span>
            <a href={POS_URL} className="hover:text-white">{c.footer.posLogin}</a>
            <a href={BONUS_URL} target="_blank" rel="noopener" className="hover:text-white">{c.footer.bonusApp}</a>
          </div>
          <div className="flex flex-col gap-2 text-[14px]">
            <span className="mb-1 text-[12px] font-bold uppercase tracking-wider text-white/40">SalemTech</span>
            <a href="#request" className="hover:text-white">{c.footer.request}</a>
            <a href="#faq" className="hover:text-white">{c.nav.faq}</a>
          </div>
        </div>
        <div className="border-t border-white/10 py-5 text-center text-[13px] text-white/40">© {new Date().getFullYear()} SalemTech. {c.footer.rights}</div>
      </footer>
    </div>
  )
}

// ---------- Тарифтер: әкімшілікте басқарылатын белсенді тарифтер ----------

function Pricing({ c, onChoose }: { c: (typeof COPY)['ru']; onChoose: (name: string) => void }) {
  const [plans, setPlans] = useState<Plan[] | null>(null)
  useEffect(() => {
    fetch('/api/public/v1/plans').then((r) => (r.ok ? r.json() : [])).then((p: Plan[]) => setPlans(p)).catch(() => setPlans([]))
  }, [])
  const popular = plans && plans.length >= 3 ? plans[Math.floor(plans.length / 2)].id : null
  return (
    <section id="pricing" className="scroll-mt-20 bg-[#f6f8fc] py-20 sm:py-28">
      <div className="mx-auto max-w-7xl px-4 sm:px-6">
        <SectionTitle eyebrow={c.pricing.eyebrow} title={c.pricing.title} subtitle={c.pricing.subtitle} />
        {!plans ? (
          <div className="mt-14 flex justify-center text-[#98a1b1]"><Loader2 className="animate-spin" size={28} /><span className="sr-only">{c.pricing.loading}</span></div>
        ) : plans.length === 0 ? (
          <Reveal className="mx-auto mt-14 flex max-w-xl flex-col items-center gap-4 rounded-3xl bg-white p-10 text-center ring-1 ring-[#e6e9f0]">
            <span className="flex size-14 items-center justify-center rounded-2xl bg-[#1570ff]/10 text-[#1570ff]"><Sparkles size={26} /></span>
            <h3 className="text-[22px] font-bold">{c.pricing.custom}</h3>
            <p className="text-[#5b6576]">{c.pricing.customText}</p>
            <a href="#request" className="rounded-2xl bg-[#1570ff] px-6 py-3 font-semibold text-white">{c.nav.cta}</a>
          </Reveal>
        ) : (
          <div className={`mx-auto mt-14 grid gap-5 ${plans.length === 1 ? 'max-w-md' : plans.length === 2 ? 'max-w-3xl md:grid-cols-2' : 'md:grid-cols-3'}`}>
            {plans.map((p, i) => {
              const hot = p.id === popular
              return (
                <Reveal key={p.id} delay={i * 100} className={`relative flex flex-col rounded-3xl p-7 ${hot ? 'bg-[#0b1220] text-white shadow-[0_30px_60px_-20px_rgba(11,18,32,0.5)] md:-translate-y-3' : 'bg-white ring-1 ring-[#e6e9f0]'}`}>
                  {hot && <span className="absolute right-6 top-6 rounded-full bg-gradient-to-r from-[#1570ff] to-[#e31e24] px-3 py-1 text-[12px] font-bold text-white">{c.pricing.popular}</span>}
                  <h3 className="text-[20px] font-bold">{p.name}</h3>
                  {p.description && <p className={`mt-1 text-[14px] ${hot ? 'text-white/60' : 'text-[#5b6576]'}`}>{p.description}</p>}
                  <div className="mt-5 flex items-baseline gap-1.5">
                    <span className="text-[40px] font-extrabold tracking-tight">{new Intl.NumberFormat('ru-RU').format(p.monthlyPrice)} ₸</span>
                    <span className={hot ? 'text-white/60' : 'text-[#5b6576]'}>{c.pricing.month}</span>
                  </div>
                  <div className={`mt-6 text-[12px] font-bold uppercase tracking-wider ${hot ? 'text-white/40' : 'text-[#98a1b1]'}`}>{c.pricing.included}</div>
                  <ul className="mt-3 flex flex-1 flex-col gap-2.5 text-[15px]">
                    {c.modules.items.map((m) => {
                      const on = p.modules.includes(m.key)
                      return (
                        <li key={m.key} className={`flex items-center gap-2.5 ${on ? '' : hot ? 'text-white/30' : 'text-[#c0c6d2]'}`}>
                          <span className={`flex size-5 items-center justify-center rounded-full ${on ? (hot ? 'bg-white text-[#0b1220]' : 'bg-[#1570ff] text-white') : hot ? 'bg-white/10' : 'bg-[#eef1f6]'}`}>
                            {on ? <Check size={12} strokeWidth={3} /> : <X size={11} strokeWidth={3} />}
                          </span>
                          {m.title}
                        </li>
                      )
                    })}
                  </ul>
                  <button type="button" onClick={() => onChoose(p.name)}
                    className={`mt-7 h-12 rounded-2xl text-[15px] font-semibold transition-colors ${hot ? 'bg-white text-[#0b1220] hover:bg-[#e8f1ff]' : 'bg-[#0b1220] text-white hover:bg-[#1570ff]'}`}>{c.pricing.choose}</button>
                </Reveal>
              )
            })}
          </div>
        )}
        {plans && plans.length > 0 && <p className="mt-8 text-center text-[14px] text-[#5b6576]">{c.pricing.trial}</p>}
      </div>
    </section>
  )
}

// ---------- Сұрақтар ----------

function Faq({ c }: { c: (typeof COPY)['ru'] }) {
  const [open, setOpen] = useState<number | null>(0)
  return (
    <section id="faq" className="scroll-mt-20 py-20 sm:py-28">
      <div className="mx-auto max-w-3xl px-4 sm:px-6">
        <SectionTitle eyebrow={c.faq.eyebrow} title={c.faq.title} />
        <div className="mt-12 flex flex-col gap-3">
          {c.faq.items.map((f, i) => (
            <Reveal key={f.q} delay={i * 50} className="overflow-hidden rounded-2xl border border-[#e6e9f0] bg-white">
              <button type="button" onClick={() => setOpen(open === i ? null : i)} aria-expanded={open === i}
                className="flex w-full items-center gap-4 px-5 py-4 text-left text-[16px] font-semibold">
                <span className="flex-1">{f.q}</span>
                <ChevronDown size={20} className={`shrink-0 text-[#98a1b1] transition-transform ${open === i ? 'rotate-180' : ''}`} />
              </button>
              <div className={`grid transition-all duration-300 ${open === i ? 'grid-rows-[1fr]' : 'grid-rows-[0fr]'}`}>
                <p className="overflow-hidden px-5 text-[15px] leading-relaxed text-[#5b6576]"><span className="block pb-5">{f.a}</span></p>
              </div>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  )
}

// ---------- Өтінім ----------

function RequestForm({ c, lang, plan }: { c: (typeof COPY)['ru']; lang: Lang; plan: string | null }) {
  const empty = { name: '', phone: '+7 ', business: '', city: '', interest: 'both', comment: '', website: '' }
  const [f, setF] = useState(empty)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [state, setState] = useState<'idle' | 'sending' | 'done' | 'error'>('idle')
  useEffect(() => { if (plan) setF((x) => ({ ...x, comment: x.comment.includes(plan) ? x.comment : `${c.form.planComment}: ${plan}${x.comment ? `. ${x.comment}` : ''}` })) }, [plan]) // eslint-disable-line react-hooks/exhaustive-deps
  const patch = (p: Partial<typeof f>) => setF((x) => ({ ...x, ...p }))
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    const err: Record<string, string> = {}
    if (f.name.trim().length < 2) err.name = c.form.nameError
    if (f.phone.replace(/\D/g, '').length < 11) err.phone = c.form.phoneError
    setErrors(err)
    if (Object.keys(err).length) return
    setState('sending')
    try {
      const res = await fetch('/api/public/v1/leads', {
        method: 'POST', headers: { 'Content-Type': 'application/json', 'Accept-Language': lang === 'kk' ? 'kk' : 'ru' },
        body: JSON.stringify({ name: f.name, phone: f.phone, businessName: f.business, city: f.city, interest: f.interest, comment: f.comment, language: lang, website: f.website }),
      })
      if (res.ok) { setState('done'); return }
      const body = await res.json().catch(() => null) as { detail?: string; field?: string } | null
      if (res.status === 400 && body?.field) { setErrors({ [body.field]: body.detail ?? c.form.error }); setState('idle'); return }
      setState('error')
    } catch { setState('error') }
  }
  const input = (key: 'name' | 'phone' | 'business' | 'city', label: string, type = 'text', auto?: string) => (
    <label className="flex flex-col gap-1.5">
      <span className="text-[13px] font-semibold text-[#3a4456]">{label}{(key === 'name' || key === 'phone') && <span className="text-[#e31e24]"> *</span>}</span>
      <input type={type} autoComplete={auto} value={f[key]} onChange={(e) => patch({ [key]: e.target.value })}
        className={`h-12 rounded-xl border bg-white px-4 text-[15px] outline-none transition-colors focus:border-[#1570ff] focus:ring-4 focus:ring-[#1570ff]/10 ${errors[key] ? 'border-[#e5484d]' : 'border-[#d9dee8]'}`} />
      {errors[key] && <span className="text-[13px] text-[#e5484d]">{errors[key]}</span>}
    </label>
  )
  return (
    <section id="request" className="scroll-mt-20 relative overflow-hidden bg-gradient-to-br from-[#eef4ff] via-white to-[#fff1f1] py-20 sm:py-28">
      <div className="mx-auto grid max-w-6xl gap-12 px-4 sm:px-6 lg:grid-cols-[1fr_1.1fr]">
        <Reveal className="flex flex-col items-start gap-4 lg:pt-6">
          <Eyebrow>{c.form.eyebrow}</Eyebrow>
          <h2 className="text-[32px] font-extrabold leading-[1.12] tracking-tight sm:text-[44px]">{c.form.title}</h2>
          <p className="max-w-md text-[17px] leading-relaxed text-[#5b6576]">{c.form.subtitle}</p>
          <div className="mt-4 flex items-center gap-4">
            <Phone src="/landing/app-home.jpg" alt="SalemBonus" className="w-28 rotate-[-6deg]" />
            <div className="w-56"><PosMiniCard c={c} /></div>
          </div>
        </Reveal>
        <Reveal delay={120}>
          <div className="rounded-[28px] bg-white p-6 shadow-[0_30px_80px_-30px_rgba(11,31,68,0.35)] ring-1 ring-[#e6e9f0] sm:p-8">
            {state === 'done' ? (
              <div className="flex flex-col items-center gap-3 py-10 text-center">
                <span className="flex size-16 items-center justify-center rounded-full bg-[#16a34a]/12 text-[#16a34a]"><Check size={32} strokeWidth={3} /></span>
                <h3 className="text-[22px] font-bold">{c.form.success}</h3>
                <p className="text-[#5b6576]">{c.form.successText}</p>
                <button type="button" className="mt-2 text-[14px] font-semibold text-[#1570ff] hover:underline" onClick={() => { setF(empty); setState('idle') }}>{c.form.again}</button>
              </div>
            ) : (
              <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
                <div className="grid gap-4 sm:grid-cols-2">
                  {input('name', c.form.name, 'text', 'name')}
                  {input('phone', c.form.phone, 'tel', 'tel')}
                  {input('business', c.form.business, 'text', 'organization')}
                  {input('city', c.form.city, 'text', 'address-level2')}
                </div>
                <fieldset className="flex flex-col gap-1.5">
                  <legend className="mb-1.5 text-[13px] font-semibold text-[#3a4456]">{c.form.interest}</legend>
                  <div className="grid grid-cols-3 gap-2">
                    {(['pos', 'bonus', 'both'] as const).map((k) => (
                      <button key={k} type="button" onClick={() => patch({ interest: k })} aria-pressed={f.interest === k}
                        className={`h-11 rounded-xl border text-[14px] font-semibold transition-colors ${f.interest === k ? 'border-[#1570ff] bg-[#1570ff]/8 text-[#1570ff]' : 'border-[#d9dee8] text-[#3a4456] hover:border-[#98a1b1]'}`}>
                        {c.form.interests[k]}
                      </button>
                    ))}
                  </div>
                </fieldset>
                <label className="flex flex-col gap-1.5">
                  <span className="text-[13px] font-semibold text-[#3a4456]">{c.form.comment}</span>
                  <textarea value={f.comment} maxLength={1000} placeholder={c.form.commentPlaceholder} onChange={(e) => patch({ comment: e.target.value })}
                    className="min-h-24 rounded-xl border border-[#d9dee8] px-4 py-3 text-[15px] outline-none focus:border-[#1570ff] focus:ring-4 focus:ring-[#1570ff]/10" />
                </label>
                {/* Бот-тұзақ: адамға көрінбейді */}
                <input type="text" tabIndex={-1} autoComplete="off" aria-hidden="true" value={f.website} onChange={(e) => patch({ website: e.target.value })} className="absolute -left-[9999px] h-0 w-0 opacity-0" />
                {state === 'error' && <p className="rounded-xl bg-[#fdecec] px-4 py-3 text-[14px] text-[#e5484d]">{c.form.error}</p>}
                <button type="submit" disabled={state === 'sending'}
                  className="flex h-13 items-center justify-center gap-2 rounded-2xl bg-[#1570ff] text-[16px] font-semibold text-white shadow-[0_12px_30px_-10px_rgba(21,112,255,0.7)] transition-colors hover:bg-[#0b5fe6] disabled:opacity-70">
                  {state === 'sending' ? <Loader2 size={20} className="animate-spin" /> : <>{c.form.submit}<ArrowRight size={18} /></>}
                </button>
                <p className="text-center text-[12px] text-[#98a1b1]">{c.form.consent}</p>
              </form>
            )}
          </div>
        </Reveal>
      </div>
    </section>
  )
}

function PosMiniCard({ c }: { c: (typeof COPY)['ru'] }) {
  return (
    <div className="rounded-2xl bg-white p-4 shadow-[0_18px_40px_-15px_rgba(11,31,68,0.35)] ring-1 ring-black/5">
      <div className="flex items-center justify-between text-[11px] text-[#5b6576]"><span>{c.mock.receipt} №1284</span><span className="rounded-md bg-[#16a34a]/12 px-1.5 py-0.5 font-semibold text-[#16a34a]">✓</span></div>
      <div className="mt-2 flex flex-col gap-1 text-[11px]">
        <div className="flex justify-between gap-2"><span className="truncate">{c.mock.products[0]}</span><span className="shrink-0">12 400 ₸</span></div>
        <div className="flex justify-between gap-2"><span className="truncate">{c.mock.products[2]}</span><span className="shrink-0">2 600 ₸</span></div>
      </div>
      <div className="mt-2 flex justify-between border-t border-dashed border-[#d9dee8] pt-2 text-[13px] font-extrabold"><span>{c.mock.total}</span><span>15 000 ₸</span></div>
      <div className="mt-1 text-right text-[11px] font-semibold text-[#e31e24]">+300 Б SalemBonus</div>
    </div>
  )
}
