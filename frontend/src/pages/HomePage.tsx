import { Avatar, PageHeader } from '../components/PageHeader'

export function HomePage() {
  return (
    <>
      <PageHeader
        right={
          <div className="flex items-center gap-2.5">
            <div className="text-right">
              <div className="text-xs text-ink-2">Сәлем,</div>
              <div className="text-[15px] font-bold">Мақсадбек!</div>
            </div>
            <Avatar />
          </div>
        }
      />
      <section className="mt-4 rounded-[22px] bg-dark p-5 text-white">
        <div className="text-[13px] text-gray-400">Бонус балансы</div>
        <div className="text-[34px] font-extrabold tracking-tight">12 450 Б</div>
        <div className="text-sm text-gray-400">≈ 12 450 ₸</div>
      </section>
      <p className="mt-6 text-sm text-ink-2">Басты бет: карта-карусель, деңгей, операциялар осында болады.</p>
    </>
  )
}
