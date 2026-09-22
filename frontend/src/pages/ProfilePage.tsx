import { Bell, CreditCard, Globe, Info, Lock, LogOut, Settings, User } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { authApi } from '../lib/api'
import { useAuth } from '../lib/auth'
import { PageHeader } from '../components/PageHeader'
import { PageTitle } from '../components/PageTitle'
import { IconButton } from '../components/IconButton'
import { ProfileCard } from '../components/profile/ProfileCard'
import { MenuList, type MenuItem } from '../components/profile/MenuList'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { useMe, useUnreadCount } from '../lib/queries'

const APP_VERSION = '1.0.0'

export function ProfilePage() {
  const me = useMe()
  const navigate = useNavigate()
  const hasUnread = (useUnreadCount().data ?? 0) > 0
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
    { icon: Lock, title: 'Қауіпсіздік', subtitle: 'Құпия сөз, PIN-код, кіру баптаулары', to: '/profile/security' },
    { icon: CreditCard, title: 'Менің карталарым', subtitle: 'Барлық бонус карталар', to: '/cards' },
    { icon: Bell, title: 'Хабарламалар', subtitle: 'Қандай хабарламалар алатыныңызды баптау', to: '/profile/notifications' },
    { icon: Globe, title: 'Тіл', subtitle: 'Қазақша / Русский', to: '/profile/language' },
    { icon: Info, title: 'Жиі қойылатын сұрақтар', subtitle: 'Жауаптар мен көмек', to: '/faq' },
    { icon: LogOut, title: 'Шығу', subtitle: 'Аккаунттан шығу', danger: true, onClick: () => void logout() },
  ]

  return (
    <>
      <PageHeader
        right={
          <div className="flex items-center gap-2.5">
            <IconButton icon={Bell} dot={hasUnread} label="Хабарламалар" onClick={() => navigate('/notifications')} />
            <IconButton icon={Settings} label="Баптаулар" />
          </div>
        }
      />

      <div className="mt-3 flex flex-col gap-4">
        <PageTitle title="Профиль" subtitle="Жеке деректеріңіз және қосымша баптаулар" />

        {me.isPending && <Skeleton className="h-56" />}
        {me.isError && <ErrorBox message={me.error.message} onRetry={() => me.refetch()} />}
        {me.data && <ProfileCard me={me.data} />}

        <MenuList items={menu} />

        <footer className="py-2 text-center text-[11px] text-ink-3">
          <div>SalemBonus v{APP_VERSION}</div>
          <div>Бір аккаунт — көп мүмкіндіктер</div>
        </footer>
      </div>
    </>
  )
}
