import { useState } from 'react'
import { ChevronRight, Coins, Globe, Info, LogOut, MessageCircleMore, Palette, Pencil, ShoppingBag, User } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { authApi } from '../lib/api'
import { useAuth } from '../lib/auth'
import { PageTitle } from '../components/PageTitle'
import { ProfileRow } from '../components/profile/ProfileRow'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { formatNumber, initials } from '../lib/format'
import { LANGUAGES, useLanguage } from '../lib/language'
import { useMe, useQr } from '../lib/queries'
import { useT, type TranslationKey } from '../lib/i18n'
import { OptionSheet } from '../components/OptionSheet'
import { THEME_MODES, useThemeMode } from '../lib/themeMode'

const APP_VERSION = '1.0.0'

export function ProfilePage() {
  const t = useT()
  const me = useMe()
  const qr = useQr()
  const [lang, setLang] = useLanguage()
  const [themeMode, setThemeMode] = useThemeMode()
  const [sheet, setSheet] = useState<'language' | 'theme' | null>(null)
  const navigate = useNavigate()
  const qc = useQueryClient()

  const logout = async () => {
    const { refreshToken, clear } = useAuth.getState()
    if (refreshToken) await authApi.logout(refreshToken).catch(() => undefined)
    clear()
    qc.clear()
    navigate('/login', { replace: true })
  }

  const currentLanguage = LANGUAGES.find((l) => l.value === lang)!
  const themeLabel = (mode: (typeof THEME_MODES)[number]) => t(`theme.${mode}` as TranslationKey)

  return (
    <>
      <div className="mt-2 flex flex-col gap-3">
        <PageTitle title={t('profile.title')} subtitle={t('profile.subtitle')} />

        {me.isPending && <Skeleton className="h-24" />}
        {me.isError && <ErrorBox message={me.error.message} onRetry={() => me.refetch()} />}

        {me.data && (
          <>
            <Link to="/profile/edit" className="flex items-center gap-3 rounded-2xl bg-surface p-3.5 active:scale-[0.99]">
              <div className="flex size-14 shrink-0 items-center justify-center overflow-hidden rounded-full bg-violet-soft text-lg font-bold text-violet">
                {me.data.avatarUrl ? (
                  <img src={me.data.avatarUrl} alt="" className="size-full object-cover" />
                ) : (
                  initials(me.data.fullName)
                )}
              </div>
              <div className="min-w-0 flex-1">
                <div className="truncate text-[15px] font-extrabold uppercase leading-tight">{me.data.fullName}</div>
                <div className="mt-0.5 truncate text-xs text-ink-2">
                  {qr.data ? t('profile.idLabel', { code: qr.data.code }) : '…'}
                </div>
              </div>
              <span className="flex shrink-0 items-center gap-1.5 rounded-full bg-brand-soft px-3 py-2 text-[12px] font-semibold text-brand">
                <Pencil size={13} /> {t('profile.edit')}
              </span>
            </Link>

            <Link
              to="/cards"
              className="relative flex items-center gap-3.5 overflow-hidden rounded-2xl p-4 text-white active:scale-[0.99]"
              style={{ background: 'linear-gradient(135deg, #3B9BFF 0%, #0A84F8 55%, #0062CC 100%)' }}
            >
              <div className="flex size-14 shrink-0 items-center justify-center rounded-full bg-white/20">
                <Coins size={26} />
              </div>
              <div className="min-w-0 flex-1">
                <div className="text-[15px] font-bold leading-tight">{t('profile.availableBonuses')}</div>
                <div className="mt-0.5 text-xs text-white/75">{t('profile.totalBalance')}</div>
                <div className="mt-1.5 flex items-baseline gap-1.5 font-extrabold leading-none tracking-[-0.02em]">
                  <span className="text-[30px]">{formatNumber(me.data.totalBalance)}</span>
                  <span className="text-[17px]">{t('common.bonusUnit')}</span>
                </div>
              </div>
              <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-white/20">
                <ChevronRight size={20} />
              </span>
            </Link>
          </>
        )}

        <ProfileRow icon={User} tint="#6D5DF6" title={t('profile.personal')} subtitle={t('profile.personalHint')} to="/profile/edit" />
        <ProfileRow icon={ShoppingBag} tint="#16A34A" title={t('profile.purchases')} subtitle={t('profile.purchasesHint')} to="/transactions" />
        <ProfileRow icon={MessageCircleMore} tint="#E5484D" title={t('profile.messages')} subtitle={t('profile.messagesHint')} to="/notifications" />
        <ProfileRow
          icon={Globe}
          tint="#0A84F8"
          title={currentLanguage.label}
          subtitle={t('profile.languageHint')}
          onClick={() => setSheet('language')}
        />
        <ProfileRow
          icon={Palette}
          tint="#F5B301"
          title={t('profile.theme')}
          subtitle={themeLabel(themeMode)}
          onClick={() => setSheet('theme')}
        />
        <ProfileRow icon={Info} tint="#6D5DF6" title={t('profile.about')} subtitle={t('profile.aboutHint')} to="/about" />
        <ProfileRow icon={LogOut} tint="#E5484D" title={t('profile.logout')} subtitle={t('profile.logoutHint')} danger onClick={() => void logout()} />

        <footer className="py-2 text-center text-[11px] text-ink-3">SalemBonus v{APP_VERSION}</footer>
      </div>

      <OptionSheet
        open={sheet === 'language'}
        title={t('profile.languageHint')}
        value={lang}
        options={LANGUAGES.map((l) => ({ value: l.value, label: l.label }))}
        onSelect={setLang}
        onClose={() => setSheet(null)}
      />
      <OptionSheet
        open={sheet === 'theme'}
        title={t('profile.theme')}
        value={themeMode}
        options={THEME_MODES.map((m) => ({ value: m, label: themeLabel(m) }))}
        onSelect={setThemeMode}
        onClose={() => setSheet(null)}
      />
    </>
  )
}
