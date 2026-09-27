import { useState } from 'react'
import { Coins, Globe, Info, LogOut, MessageCircleMore, Palette, Pencil, ShoppingBag, Trash2, User } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { authApi } from '../lib/api'
import { useAuth } from '../lib/auth'
import { PageTitle } from '../components/PageTitle'
import { ProfileRow } from '../components/profile/ProfileRow'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { formatNumber, initials } from '../lib/format'
import { avatarSrc } from '../lib/avatar'
import { LANGUAGES, useLanguage } from '../lib/language'
import { useDeleteMe, useMe, useQr } from '../lib/queries'
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
  const [sheet, setSheet] = useState<'language' | 'theme' | 'delete' | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const deleteMe = useDeleteMe()
  const navigate = useNavigate()
  const qc = useQueryClient()

  const logout = async () => {
    const { refreshToken, clear } = useAuth.getState()
    if (refreshToken) await authApi.logout(refreshToken).catch(() => undefined)
    clear()
    qc.clear()
    navigate('/login', { replace: true })
  }

  /** Аккаунтты өшіру: сәтті болса токендер де тазаланып, кіру бетіне қайтарылады. */
  const removeAccount = async () => {
    setDeleteError(null)
    try {
      await deleteMe.mutateAsync()
      useAuth.getState().clear()
      setSheet(null)
      navigate('/login', { replace: true })
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : t('profile.deleteFailed'))
    }
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
                  <img src={avatarSrc(me.data.avatarUrl)} alt="" className="size-full object-cover" />
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

            {/* Тек ақпарат үшін: басылмайды, ешқайда апармайды. */}
            <div
              className="relative flex items-center gap-3.5 overflow-hidden rounded-2xl p-4 text-white"
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
            </div>
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
        <ProfileRow
          icon={Trash2}
          tint="#E5484D"
          title={t('profile.deleteAccount')}
          subtitle={t('profile.deleteAccountHint')}
          danger
          onClick={() => {
            setDeleteError(null)
            setSheet('delete')
          }}
        />

        <nav className="flex justify-center gap-4 pt-1">
          <Link to="/privacy" className="text-[12px] text-ink-3">{t('legal.privacy')}</Link>
          <Link to="/terms" className="text-[12px] text-ink-3">{t('legal.terms')}</Link>
        </nav>

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

      {sheet === 'delete' && (
        <div
          className="fixed inset-0 z-50 flex items-end justify-center bg-black/50"
          onClick={() => !deleteMe.isPending && setSheet(null)}
          role="dialog"
          aria-modal="true"
        >
          <div
            className="w-full max-w-[480px] rounded-t-[28px] bg-surface px-5 pb-[max(20px,env(safe-area-inset-bottom))] pt-3"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mx-auto mb-4 h-1 w-10 rounded-full bg-muted" />
            <div className="mx-auto mb-3 flex size-12 items-center justify-center rounded-full bg-danger-soft text-danger">
              <Trash2 size={22} />
            </div>
            <h2 className="text-center text-[18px] font-extrabold">{t('profile.deleteTitle')}</h2>
            <p className="mt-2 text-center text-[14px] leading-snug text-ink-2">{t('profile.deleteBody')}</p>

            {deleteError && <p className="mt-3 text-center text-[13px] text-danger">{deleteError}</p>}

            <button
              type="button"
              disabled={deleteMe.isPending}
              onClick={() => void removeAccount()}
              className="mt-5 flex h-13 w-full items-center justify-center rounded-2xl bg-danger text-[15px] font-bold text-white active:scale-[0.99] disabled:opacity-60"
            >
              {deleteMe.isPending ? t('profile.deleting') : t('profile.deleteConfirm')}
            </button>
            <button
              type="button"
              disabled={deleteMe.isPending}
              onClick={() => setSheet(null)}
              className="mt-2 flex h-13 w-full items-center justify-center rounded-2xl text-[15px] font-semibold text-ink-2 disabled:opacity-60"
            >
              {t('common.cancel')}
            </button>
          </div>
        </div>
      )}
    </>
  )
}
