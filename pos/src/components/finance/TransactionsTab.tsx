import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { ArrowLeftRight, ChevronDown, Plus } from 'lucide-react'
import { Button } from '../ui/Button'
import { financeKeys } from '../../lib/financeApi'
import { useT } from '../../lib/i18n'
import { useFinanceMeta, useFinancePermissions, type FinanceFilters } from './financeUi'
import { OperationForm } from './OperationForm'
import { OperationsJournal } from './OperationsJournal'
import { TransferModal } from './TransferModal'

/**
 * «Транзакции» (ТЗ «Финансы» §8): барлық ақша қозғалысының бірыңғай журналы — іздеу/қарау, екінші дерек көзі емес.
 * «Новая операция» — тек рұқсат етілген дербес операциялар (кіріс, шығыс, аударым).
 */
export function TransactionsTab({ filters, storeIds }: { filters: FinanceFilters; storeIds?: string }) {
  const t = useT()
  const qc = useQueryClient()
  const { canOperate } = useFinancePermissions()
  const meta = useFinanceMeta(storeIds)
  const [menu, setMenu] = useState(false)
  const [form, setForm] = useState<'Income' | 'Expense' | 'transfer' | null>(null)
  const invalidate = () => void qc.invalidateQueries({ queryKey: financeKeys.all })
  return (
    <div className="flex flex-col gap-3">
      <OperationsJournal filters={filters} storeIds={storeIds}
        actions={canOperate ? (
          <span className="relative">
            <Button className="h-10! px-3!" icon={<Plus size={16} />} iconRight={<ChevronDown size={14} />} onClick={() => setMenu((m) => !m)}>{t('fin.newOperation')}</Button>
            {menu && (
              <>
                <div className="fixed inset-0 z-10" onClick={() => setMenu(false)} aria-hidden="true" />
                <ul role="menu" className="absolute right-0 top-full z-20 mt-1 w-56 rounded-xl border border-line bg-surface p-1 shadow-lg">
                  {([['Income', t('fin.newIncome')], ['Expense', t('fin.newExpense')], ['transfer', t('fin.transfer')]] as const).map(([k, label]) => (
                    <li key={k}><button type="button" role="menuitem" onClick={() => { setMenu(false); setForm(k) }} className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-[14px] hover:bg-field">
                      {k === 'transfer' ? <ArrowLeftRight size={16} /> : <Plus size={16} />}{label}
                    </button></li>
                  ))}
                </ul>
              </>
            )}
          </span>
        ) : undefined} />
      {form === 'transfer' && <TransferModal accounts={meta.data?.accounts ?? []} onClose={() => setForm(null)} onDone={() => { setForm(null); invalidate() }} />}
      {(form === 'Income' || form === 'Expense') && (
        <OperationForm accounts={meta.data?.accounts ?? []} categories={meta.data?.categories ?? []} kind={form} onClose={() => setForm(null)}
          onSaved={() => { setForm(null); invalidate() }} onCategoriesChanged={() => void meta.refetch()} />
      )}
    </div>
  )
}
