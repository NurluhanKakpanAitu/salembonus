import { Outlet } from 'react-router-dom'
import { TabBar } from './TabBar'

export function AppShell() {
  return (
    <div className="mx-auto flex min-h-full max-w-[480px] flex-col bg-bg">
      <main className="flex-1 px-4 pb-24 pt-[max(8px,env(safe-area-inset-top))]">
        <Outlet />
      </main>
      <TabBar />
    </div>
  )
}
