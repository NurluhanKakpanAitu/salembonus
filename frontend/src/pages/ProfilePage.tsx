import { Bell, ChevronRight, CreditCard, Gift, Globe, Info, Lock, LogOut, Settings, User } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { PageHeader } from '../components/PageHeader'
import { PageTitle } from '../components/PageTitle'
import { IconButton } from '../components/IconButton'
import { ProfileCard } from '../components/profile/ProfileCard'
import { MenuList, type MenuItem } from '../components/profile/MenuList'
import { ErrorBox, Skeleton } from '../components/Skeleton'
import { formatDate } from '../lib/format'
import { useMe } from '../lib/queries'

const APP_VERSION = '1.0.0'

export function ProfilePage() {
  const me = useMe()
  const navigate = useNavigate()

  const menu: MenuItem[] = [
    { icon: User, title: 'Жеке деректер', subtitle: 'Аты-жөні, телефон, туған күні', to: '/profile/edit' },
    { icon: Lock, title: 'Қауіпсіздік', subtitle: 'Құпия сөз, PIN-код, кіру баптаулары', to: '/profile/security' },
    { icon: CreditCard, title: 'Менің карталарым', subtitle: 'Барлық бонус карталар', to: '/cards' },
    { icon: Bell, title: 'Хабарламалар', subtitle: 'Қандай хабарламалар алатыныңызды баптау', to: '/profile/notifications' },
    { icon: Globe, title: 'Тіл', subtitle: 'Қазақша / Русский', to: '/profile/language' },
    { icon: Info, title: 'Жиі қойылатын сұрақтар', subtitle: 'Жауаптар мен көмек', to: '/faq' },
    { icon: LogOut, title: 'Шығу', subtitle: 'Аккаунттан шығу', danger: true, onClick: () => navigate('/') },
  ]

  return (
    <>
      <PageHeader
        right={
          <div className="flex items-center gap-2.5">
            <IconButton icon={Bell} dot label="Хабарламалар" onClick={() => navigate('/notifications')} />
            <IconButton icon={Settings} label="Баптаулар" />
          </div>
        }
      />

      <div className="mt-3 flex flex-col gap-4">
        <PageTitle title="Профиль" subtitle="Жеке деректеріңіз және қосымша баптаулар" />

        {me.isPending && <Skeleton className="h-56" />}
        {me.isError && <ErrorBox message={me.error.message} onRetry={() => me.refetch()} />}
        {me.data && <ProfileCard me={me.data} />}

        {me.data?.birthDate && (
          <section className="flex items-center gap-3 rounded-card bg-surface px-4 py-3.5">
            <div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-violet-soft text-violet">
              <Gift size={22} />
            </div>
            <div className="min-w-0 flex-1">
              <div className="text-sm font-bold leading-tight">Туған күн сыйлығы 3 000 Б</div>
              <div className="mt-0.5 text-xs text-ink-2">Бонус сізді күтеді!</div>
            </div>
            <span className="shrink-0 rounded-[10px] bg-violet-soft px-2.5 py-1.5 text-[11px] font-semibold text-violet">
              {formatDate(me.data.birthDate, false)}
            </span>
            <ChevronRight size={18} className="shrink-0 text-ink-3" />
          </section>
        )}

        <MenuList items={menu} />

        <footer className="py-2 text-center text-[11px] text-ink-3">
          <div>SalemBonus v{APP_VERSION}</div>
          <div>Бір аккаунт — көп мүмкіндіктер</div>
        </footer>
      </div>
    </>
  )
}
