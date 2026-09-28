import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { CalendarDays, Check, ChevronDown, KeyRound, Lock, LockKeyhole, LogOut, Menu, Store, UsersRound } from 'lucide-react'
import { activeStore, applyMe, setActiveStore, useAuth } from '../lib/auth'
import { setLocked, useRegister } from '../lib/register'
import { registerApi } from '../lib/registerApi'
import { endSession } from '../lib/session'
import { PasswordModal, PinModal } from '../components/CredentialModals'
import { useT, type TranslationKey } from '../lib/i18n'
import { useLanguageStore, type Language } from '../lib/language'
import { staffApi } from '../lib/staffApi'
import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { NotificationsBell } from '../components/cashier/NotificationsBell'

const MONTHS: Record<Language, string[]> = {
  ru: ['января', 'февраля', 'марта', 'апреля', 'мая', 'июня', 'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря'],
  kk: ['қаңтар', 'ақпан', 'наурыз', 'сәуір', 'мамыр', 'маусым', 'шілде', 'тамыз', 'қыркүйек', 'қазан', 'қараша', 'желтоқсан'],
}

/** Жабылатын қалқымалы мәзір үшін: сыртқа басқанда жабылады. */
function usePopover() {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [open])
  return { open, setOpen, ref }
}

export function Topbar({ title, onMenu }: { title: string; onMenu: () => void }) {
  const t = useT()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const me = useAuth((s) => s.me)
  const store = useAuth(activeStore)
  const lang = useLanguageStore((s) => s.lang)
  const setLang = useLanguageStore((s) => s.set)
  const storeMenu = usePopover()
  const profileMenu = usePopover()
  const register = useRegister((s) => s.register)
  const [modal, setModal] = useState<'pin' | 'password' | null>(null)

  const now = new Date()
  const today = `${t('common.today')}, ${now.getDate()} ${MONTHS[lang][now.getMonth()]} ${now.getFullYear()}`
  const initials = `${me?.firstName?.[0] ?? ''}${me?.lastName?.[0] ?? ''}`.toUpperCase()
  const multiStore = (me?.stores.length ?? 0) > 1

  const changeLanguage = async (next: Language) => {
    setLang(next)
    profileMenu.setOpen(false)
    // Таңдау серверде сақталады — келесі кіргенде сол тілде ашылады (ТЗ §15.4).
    try {
      applyMe(await staffApi.setLanguage(next))
    } catch {
      /* сақталмаса да, интерфейс ауысты — келесі жолы қайта таңдайды */
    }
  }

  const logout = async () => {
    profileMenu.setOpen(false)
    await endSession()
    qc.clear()
    navigate('/login', { replace: true })
  }

  const lockNow = () => {
    profileMenu.setOpen(false)
    setLocked(true)
    registerApi.lock().catch(() => undefined)
  }

  return (
    <header className="flex h-[72px] shrink-0 items-center gap-3 border-b border-line bg-surface px-4 lg:px-6">
      <button type="button" onClick={onMenu} className="text-ink-2 lg:hidden" aria-label="Menu">
        <Menu size={24} />
      </button>
      <h1 className="min-w-0 truncate text-[22px] font-extrabold tracking-tight lg:text-[26px]">{title}</h1>

      <div className="ml-auto flex items-center gap-2 lg:gap-3">
        {/* Дүкен: бірнешеу болса — таңдау (ТЗ «Статистика» §3.3) */}
        <div ref={storeMenu.ref} className="relative hidden md:block">
          <button
            type="button"
            disabled={!multiStore}
            onClick={() => storeMenu.setOpen((o) => !o)}
            className="flex h-11 items-center gap-2.5 rounded-xl border border-line bg-surface px-3.5 text-left disabled:cursor-default"
          >
            <Store size={19} className="text-ink-2" />
            <span className="max-w-[180px] truncate text-[14px] font-semibold">{store?.name}</span>
            {multiStore && <ChevronDown size={16} className="text-ink-3" />}
          </button>
          {storeMenu.open && (
            <ul className="absolute right-0 z-20 mt-2 w-64 overflow-hidden rounded-xl border border-line bg-surface py-1 shadow-lg">
              {me?.stores.map((s) => (
                <li key={s.id}>
                  <button
                    type="button"
                    onClick={() => {
                      setActiveStore(s.id)
                      qc.invalidateQueries()
                      storeMenu.setOpen(false)
                    }}
                    className="flex w-full items-center justify-between gap-2 px-3.5 py-2.5 text-left hover:bg-field"
                  >
                    <span className="min-w-0">
                      <span className="block truncate text-[14px] font-semibold">{s.name}</span>
                      {s.address && <span className="block truncate text-[12px] text-ink-3">{s.address}</span>}
                    </span>
                    {s.id === store?.id && <Check size={16} className="shrink-0 text-brand" />}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="hidden h-11 items-center gap-2.5 rounded-xl border border-line px-3.5 text-[14px] font-medium xl:flex">
          <CalendarDays size={18} className="text-ink-2" />
          {today}
        </div>

        <NotificationsBell />
        <div ref={profileMenu.ref} className="relative">
          <button
            type="button"
            onClick={() => profileMenu.setOpen((o) => !o)}
            className="flex items-center gap-2.5 rounded-xl py-1 pl-1 pr-2 hover:bg-field"
          >
            <span className="flex size-10 items-center justify-center rounded-full bg-brand-soft text-[14px] font-bold text-brand">
              {initials}
            </span>
            <span className="hidden text-left sm:block">
              <span className="block text-[14px] font-semibold leading-tight">{me?.firstName}</span>
              <span className="block text-[12px] text-ink-3">
                {store ? t(`role.${store.role}` as TranslationKey) : ''}
              </span>
            </span>
            <ChevronDown size={16} className="hidden text-ink-3 sm:block" />
          </button>
          {profileMenu.open && (
            <div className="absolute right-0 z-20 mt-2 w-60 overflow-hidden rounded-xl border border-line bg-surface py-1 shadow-lg">
              <div className="border-b border-line px-3.5 py-2.5">
                <div className="truncate text-[14px] font-semibold">{`${me?.firstName} ${me?.lastName}`}</div>
                <div className="truncate text-[12px] text-ink-3">{me?.organizationName}</div>
              </div>
              <div className="px-3.5 pb-1 pt-2.5 text-[12px] font-medium text-ink-3">{t('top.language')}</div>
              {(['ru', 'kk'] as const).map((l) => (
                <button
                  key={l}
                  type="button"
                  onClick={() => void changeLanguage(l)}
                  className="flex w-full items-center justify-between px-3.5 py-2 text-left text-[14px] hover:bg-field"
                >
                  {t(`lang.${l}`)}
                  {l === lang && <Check size={16} className="text-brand" />}
                </button>
              ))}
              <div className="mt-1 border-t border-line py-1">
                <MenuItem icon={<KeyRound size={16} />} onClick={() => { profileMenu.setOpen(false); setModal('pin') }}>
                  {me?.hasPin ? t('top.changePin') : t('top.setPin')}
                </MenuItem>
                <MenuItem icon={<LockKeyhole size={16} />} onClick={() => { profileMenu.setOpen(false); setModal('password') }}>
                  {t('top.changePassword')}
                </MenuItem>
                {register && (
                  <>
                    <MenuItem icon={<Lock size={16} />} onClick={lockNow}>{t('top.lock')}</MenuItem>
                    <MenuItem icon={<UsersRound size={16} />} onClick={() => void logout()}>{t('top.switchCashier')}</MenuItem>
                  </>
                )}
              </div>
              <button
                type="button"
                onClick={() => void logout()}
                className="flex w-full items-center gap-2 border-t border-line px-3.5 py-2.5 text-left text-[14px] text-danger hover:bg-field"
              >
                <LogOut size={16} /> {t('top.logout')}
              </button>
            </div>
          )}
        </div>
      </div>
      {modal === 'pin' && <PinModal hasPin={!!me?.hasPin} onClose={() => setModal(null)} />}
      {modal === 'password' && <PasswordModal onClose={() => setModal(null)} />}
    </header>
  )
}

function MenuItem({ icon, onClick, children }: { icon: ReactNode; onClick: () => void; children: ReactNode }) {
  return (
    <button type="button" onClick={onClick}
      className="flex w-full items-center gap-2 px-3.5 py-2 text-left text-[14px] text-ink hover:bg-field">
      <span className="text-ink-3">{icon}</span> {children}
    </button>
  )
}
