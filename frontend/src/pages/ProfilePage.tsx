import { LogOut, User } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { authApi } from '../lib/api'
import { useAuth } from '../lib/auth'
import { PageHeader } from '../components/PageHeader'
import { PageTitle } from '../components/PageTitle'
import { ProfileCard } from '../components/profile/ProfileCard'
import { LanguagePicker } from '../components/profile/LanguagePicker'
import { MenuList, type MenuItem } from '../components/profile/MenuList'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { useMe } from '../lib/queries'

const APP_VERSION = '1.0.0'

export function ProfilePage() {
  const me = useMe()
  const navigate = useNavigate()
  const qc = useQueryClient()

  const logout = async () => {
    const { refreshToken, clear } = useAuth.getState()
    if (refreshToken) await authApi.logout(refreshToken).catch(() => undefined)
    clear()
    qc.clear()
    navigate('/login', { replace: true })
  }

  const menu: MenuItem[] = [
    { icon: User, title: 'Жеке деректер', subtitle: 'Аты-жөні, телефон, туған күні', to: '/profile/edit' },
    { icon: LogOut, title: 'Шығу', subtitle: 'Аккаунттан шығу', danger: true, onClick: () => void logout() },
  ]

  return (
    <>
      <PageHeader />

      <div className="mt-3 flex flex-col gap-4">
        <PageTitle title="Профиль" subtitle="Жеке деректеріңіз және қосымша баптаулар" />

        {me.isPending && <Skeleton className="h-56" />}
        {me.isError && <ErrorBox message={me.error.message} onRetry={() => me.refetch()} />}
        {me.data && <ProfileCard me={me.data} />}

        <MenuList items={menu} />
        <LanguagePicker />

        <footer className="py-2 text-center text-[11px] text-ink-3">
          <div>SalemBonus v{APP_VERSION}</div>
        </footer>
      </div>
    </>
  )
}
