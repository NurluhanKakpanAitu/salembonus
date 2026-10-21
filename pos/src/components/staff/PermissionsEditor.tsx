import { useT } from '../../lib/i18n'
import { ALL_PERMISSIONS, CASHIER_HIDDEN, defaultPermissions, PERMISSION_GROUPS, permKey } from '../../lib/permissions'
import type { StaffRole } from '../../lib/types'

/**
 * Рұқсат редакторы: модуль бойынша топтар, топ тақырыбындағы жалауша — бәрін қосу/өшіру, «Набор роли» — рөлдің әдепкісі.
 * Кассирге басқару құқықтары көрсетілмейді (сервер де бермейді).
 */
export function PermissionsEditor({ role, value, onChange, disabled }: { role: StaffRole; value: string[]; onChange: (next: string[]) => void; disabled?: boolean }) {
  const t = useT()
  const visible = (p: string) => role !== 'Cashier' || !CASHIER_HIDDEN.has(p)
  const all = ALL_PERMISSIONS.filter(visible)
  const set = new Set(value)
  const toggle = (p: string, on: boolean) => onChange(on ? [...new Set([...value, p])] : value.filter((x) => x !== p))
  const preset = (list: string[]) => onChange(list.filter(visible))
  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2 text-[13px]">
        <span className="text-ink-3">{t('stf.form.selected', { n: value.filter((p) => all.includes(p)).length, total: all.length })}</span>
        <div className="ml-auto flex gap-1.5">
          {[[t('stf.form.preset'), defaultPermissions(role)], [t('stf.form.selectAll'), all], [t('stf.form.clearAll'), []]].map(([label, list]) => (
            <button key={label as string} type="button" disabled={disabled} onClick={() => preset(list as string[])}
              className="rounded-lg border border-line bg-surface px-2.5 py-1 font-semibold text-ink-2 hover:border-brand/50 hover:text-brand disabled:opacity-50">{label as string}</button>
          ))}
        </div>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        {PERMISSION_GROUPS.map((g) => {
          const perms = g.permissions.filter(visible)
          if (perms.length === 0) return null
          const on = perms.filter((p) => set.has(p)).length
          return (
            <fieldset key={g.module} disabled={disabled} className="rounded-xl border border-line p-3">
              <label className="mb-2 flex cursor-pointer items-center gap-2 text-[14px] font-bold">
                <input type="checkbox" className="accent-brand" checked={on === perms.length} ref={(el) => { if (el) el.indeterminate = on > 0 && on < perms.length }}
                  onChange={(e) => onChange(e.target.checked ? [...new Set([...value, ...perms])] : value.filter((x) => !perms.includes(x)))} />
                {t(g.label)}<span className="ml-auto text-[12px] font-medium text-ink-3">{on}/{perms.length}</span>
              </label>
              <div className="flex flex-col gap-1.5">
                {perms.map((p) => (
                  <label key={p} className="flex cursor-pointer items-start gap-2 text-[13px]">
                    <input type="checkbox" className="mt-0.5 accent-brand" checked={set.has(p)} onChange={(e) => toggle(p, e.target.checked)} />
                    <span>{t(permKey(p))}</span>
                  </label>
                ))}
              </div>
            </fieldset>
          )
        })}
      </div>
    </div>
  )
}
