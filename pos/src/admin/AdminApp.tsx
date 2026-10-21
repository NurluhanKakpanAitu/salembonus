import { lazy, Suspense, useState } from 'react'
import { NavLink, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { Building2, CreditCard, Inbox, KeyRound, LayoutDashboard, Loader2, LogOut, Menu, ShieldCheck, Tags, X } from 'lucide-react'
import { Button } from '../components/ui/Button'
import { Field, Input } from '../components/ui/Field'
import { Modal } from '../components/ui/Modal'
import { toast } from '../components/ui/Toast'
import { adminApi, logout, setSession, useAdminAuth } from './adminApi'
import { errText } from './ui'

const DashboardPage = lazy(() => import('./pages/DashboardPage').then((m) => ({ default: m.DashboardPage })))
const BusinessesPage = lazy(() => import('./pages/BusinessesPage').then((m) => ({ default: m.BusinessesPage })))
const BusinessPage = lazy(() => import('./pages/BusinessPage').then((m) => ({ default: m.BusinessPage })))
const PaymentsPage = lazy(() => import('./pages/PaymentsPage').then((m) => ({ default: m.PaymentsPage })))
const PlansPage = lazy(() => import('./pages/PlansPage').then((m) => ({ default: m.PlansPage })))
const LeadsPage = lazy(() => import('./pages/LeadsPage').then((m) => ({ default: m.LeadsPage })))
const AdminsPage = lazy(() => import('./pages/AdminsPage').then((m) => ({ default: m.AdminsPage })))

const NAV = [
  { to: '/', label: 'Обзор', icon: LayoutDashboard, end: true },
  { to: '/businesses', label: 'Бизнесы', icon: Building2 },
  { to: '/leads', label: 'Заявки', icon: Inbox },
  { to: '/payments', label: 'Оплаты', icon: CreditCard },
  { to: '/plans', label: 'Тарифы', icon: Tags },
  { to: '/admins', label: 'Администраторы', icon: ShieldCheck },
]

/** SalemTech әкімшілігі (admin.salemtech.kz): бизнестер, абонент төлемдері, модульдер, тарифтер, әкімшілер. */
export function AdminApp() {
  const token = useAdminAuth((s) => s.token)
  if (!token) return <LoginPage />
  return (
    <Routes>
      <Route element={<Shell />}>
        <Route index element={<DashboardPage />} />
        <Route path="businesses" element={<BusinessesPage />} />
        <Route path="businesses/:id" element={<BusinessPage />} />
        <Route path="payments" element={<PaymentsPage />} />
        <Route path="leads" element={<LeadsPage />} />
        <Route path="plans" element={<PlansPage />} />
        <Route path="admins" element={<AdminsPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

function Shell() {
  const admin = useAdminAuth((s) => s.admin)
  const [open, setOpen] = useState(false)
  const [password, setPassword] = useState(false)
  const link = ({ isActive }: { isActive: boolean }) =>
    `flex h-11 items-center gap-3 rounded-xl px-3 text-[14px] font-medium ${isActive ? 'bg-brand text-white' : 'text-nav-ink hover:bg-nav-2 hover:text-white'}`
  return (
    <div className="flex h-dvh overflow-hidden bg-bg">
      {open && <div className="fixed inset-0 z-30 bg-black/40 lg:hidden" onClick={() => setOpen(false)} aria-hidden="true" />}
      <aside className={`fixed inset-y-0 left-0 z-40 flex w-64 flex-col bg-nav transition-transform lg:static lg:translate-x-0 ${open ? 'translate-x-0' : '-translate-x-full'}`}>
        <div className="flex h-[72px] items-center gap-2 px-5">
          <span className="text-[20px] font-extrabold text-white">Salem<span className="text-brand">Tech</span></span>
          <span className="rounded-md bg-nav-2 px-1.5 py-0.5 text-[11px] font-bold uppercase tracking-wide text-nav-ink">admin</span>
          <button type="button" className="ml-auto text-nav-ink lg:hidden" onClick={() => setOpen(false)} aria-label="Закрыть меню"><X size={20} /></button>
        </div>
        <nav className="flex-1 px-3 py-2">
          <ul className="flex flex-col gap-1">
            {NAV.map((n) => (
              <li key={n.to}><NavLink to={n.to} end={n.end} className={link} onClick={() => setOpen(false)}><n.icon size={19} />{n.label}</NavLink></li>
            ))}
          </ul>
        </nav>
        <div className="border-t border-nav-2 p-3 text-[13px] text-nav-ink">
          <div className="truncate px-2 font-semibold text-white">{admin?.name}</div>
          <div className="truncate px-2 text-[12px]">{admin?.email}</div>
          <button type="button" className="mt-2 flex h-10 w-full items-center gap-3 rounded-xl px-2 hover:bg-nav-2 hover:text-white" onClick={() => setPassword(true)}><KeyRound size={17} />Сменить пароль</button>
          <button type="button" className="flex h-10 w-full items-center gap-3 rounded-xl px-2 hover:bg-nav-2 hover:text-white" onClick={logout}><LogOut size={17} />Выйти</button>
        </div>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 items-center gap-3 border-b border-line bg-surface px-4 lg:hidden">
          <button type="button" onClick={() => setOpen(true)} aria-label="Меню"><Menu size={22} /></button>
          <span className="font-extrabold">Salem<span className="text-brand">Tech</span> admin</span>
        </header>
        <main className="min-h-0 flex-1 overflow-y-auto p-4 lg:p-6">
          <Suspense fallback={<div className="flex justify-center py-20 text-ink-3"><Loader2 className="animate-spin" size={28} /></div>}>
            <Outlet />
          </Suspense>
        </main>
      </div>
      {password && <PasswordModal onClose={() => setPassword(false)} />}
    </div>
  )
}

function LoginPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!email.trim() || !password) { setError('Введите e-mail и пароль'); return }
    setBusy(true)
    setError(null)
    try {
      const s = await adminApi.login(email.trim(), password)
      setSession(s.accessToken, s.expiresAt, s.admin)
    } catch (err) { setError(errText(err)) } finally { setBusy(false) }
  }
  return (
    <div className="flex min-h-dvh items-center justify-center bg-nav p-4">
      <form onSubmit={submit} className="flex w-full max-w-sm flex-col gap-4 rounded-3xl bg-surface p-7 shadow-2xl">
        <div>
          <div className="text-[24px] font-extrabold">Salem<span className="text-brand">Tech</span></div>
          <div className="text-[14px] text-ink-2">Панель администратора</div>
        </div>
        <Field label="E-mail"><Input type="email" autoComplete="username" value={email} autoFocus onChange={(e) => setEmail(e.target.value)} /></Field>
        <Field label="Пароль"><Input type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} /></Field>
        {error && <p className="rounded-xl bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>}
        <Button type="submit" loading={busy}>Войти</Button>
        <p className="text-center text-[12px] text-ink-3">Доступ только для сотрудников SalemTech</p>
      </form>
    </div>
  )
}

function PasswordModal({ onClose }: { onClose: () => void }) {
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try { await adminApi.changePassword(current, next); toast('Пароль изменён'); onClose() } catch (err) { setError(errText(err)) } finally { setBusy(false) }
  }
  return (
    <Modal title="Смена пароля" onClose={onClose} width={420}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <Field label="Текущий пароль"><Input type="password" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} /></Field>
        <Field label="Новый пароль" hint="Не менее 8 символов, буквы и цифры"><Input type="password" autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>Отмена</Button>
          <Button type="submit" loading={busy} disabled={!current || next.length < 8}>Сохранить</Button>
        </div>
      </form>
    </Modal>
  )
}
