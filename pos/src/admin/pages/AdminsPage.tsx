import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { KeyRound, Power, UserPlus } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { confirmDialog } from '../../components/ui/Confirm'
import { Field, Input } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { RowMenu } from '../../components/ui/RowMenu'
import { toast } from '../../components/ui/Toast'
import { EmptyRow, td, th } from '../../components/inventory/inventoryUi'
import { BlockError } from '../../components/stats/statsUi'
import { adminApi, useAdminAuth, type Admin } from '../adminApi'
import { PageHeader, SecretModal, dateTime, errText, reportError } from '../ui'

/** «Администраторы»: SalemTech қызметкерлері — қосу (уақытша құпиясөз), өшіру, құпиясөзді қалпына келтіру. Өзіңді өшіру мүмкін емес. */
export function AdminsPage() {
  const me = useAdminAuth((s) => s.admin)
  const q = useQuery({ queryKey: ['admin', 'admins'], queryFn: adminApi.admins })
  const [adding, setAdding] = useState(false)
  const [secret, setSecret] = useState<{ email: string; password: string } | null>(null)
  const toggle = async (a: Admin) => {
    if (a.isActive && !(await confirmDialog({ message: `Отключить ${a.name}? Он сразу потеряет доступ к панели.`, confirmLabel: 'Отключить', danger: true }))) return
    try { await adminApi.setAdminActive(a.id, !a.isActive); toast(a.isActive ? 'Доступ отключён' : 'Доступ включён'); void q.refetch() } catch (err) { reportError(err) }
  }
  const reset = async (a: Admin) => {
    if (!(await confirmDialog({ message: `Сбросить пароль ${a.name}?`, confirmLabel: 'Сбросить', danger: true }))) return
    try { const r = await adminApi.resetAdminPassword(a.id); setSecret({ email: r.admin.email, password: r.tempPassword }) } catch (err) { reportError(err) }
  }
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Администраторы" subtitle="Сотрудники SalemTech с доступом к панели"
        actions={<Button icon={<UserPlus size={17} />} onClick={() => setAdding(true)}>Добавить</Button>} />
      <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
        <table className="w-full min-w-[640px]">
          <thead className="border-b border-line bg-field/60"><tr><th className={th}>Имя</th><th className={th}>E-mail</th><th className={th}>Последний вход</th><th className={th}>Статус</th><th className="w-10" /></tr></thead>
          <tbody>
            {!q.data && <EmptyRow colSpan={5} text="Загрузка…" />}
            {q.data?.map((a) => (
              <tr key={a.id} className={`border-b border-line last:border-0 ${a.isActive ? '' : 'opacity-50'}`}>
                <td className={`${td} font-semibold`}>{a.name}{a.id === me?.id && <span className="ml-2 text-[12px] font-normal text-ink-3">это вы</span>}</td>
                <td className={td}>{a.email}</td>
                <td className={td}>{dateTime(a.lastLoginAt)}</td>
                <td className={td}>{a.isActive ? 'Активен' : 'Отключён'}</td>
                <td className="px-2">{a.id !== me?.id && <RowMenu items={[
                  { label: 'Сбросить пароль', icon: <KeyRound size={16} />, onClick: () => void reset(a) },
                  { label: a.isActive ? 'Отключить' : 'Включить', icon: <Power size={16} />, danger: a.isActive, onClick: () => void toggle(a) },
                ]} />}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {adding && <AddAdminModal onClose={() => setAdding(false)} onDone={(r) => { setAdding(false); setSecret(r); void q.refetch() }} />}
      {secret && <SecretModal title="Доступ к панели" onClose={() => setSecret(null)}
        lines={[{ label: 'Вход', value: 'https://admin.salemtech.kz' }, { label: 'E-mail', value: secret.email }, { label: 'Пароль', value: secret.password, secret: true }]} />}
    </div>
  )
}

function AddAdminModal({ onClose, onDone }: { onClose: () => void; onDone: (r: { email: string; password: string }) => void }) {
  const [email, setEmail] = useState('')
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try { const r = await adminApi.createAdmin(email.trim(), name.trim()); onDone({ email: r.admin.email, password: r.tempPassword }) } catch (err) { setError(errText(err)) } finally { setBusy(false) }
  }
  return (
    <Modal title="Новый администратор" onClose={onClose} width={440}>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <Field label="Имя" required><Input value={name} maxLength={120} autoFocus onChange={(e) => setName(e.target.value)} /></Field>
        <Field label="E-mail" required><Input type="email" value={email} maxLength={200} onChange={(e) => setEmail(e.target.value)} /></Field>
        {error && <p className="text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2"><Button type="button" variant="secondary" onClick={onClose}>Отмена</Button><Button type="submit" loading={busy} disabled={!email.trim() || !name.trim()}>Создать</Button></div>
      </form>
    </Modal>
  )
}
