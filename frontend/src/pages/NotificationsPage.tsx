import { Avatar, PageHeader } from '../components/PageHeader'

export function NotificationsPage() {
  return (
    <>
      <PageHeader right={<Avatar />} />
      <h1 className="mt-4 text-[26px] font-extrabold tracking-tight">Хабарламалар</h1>
      <p className="mt-2 text-sm text-ink-2">Бұл экран дизайн бойынша толтырылады.</p>
    </>
  )
}
