import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Archive, ArchiveRestore, ArrowDownLeft, ArrowUpRight, Pencil, Plus, Tags } from 'lucide-react'
import { Button } from '../ui/Button'
import { Modal } from '../ui/Modal'
import { RowMenu } from '../ui/RowMenu'
import { toast } from '../ui/Toast'
import { financeApi, financeKeys } from '../../lib/financeApi'
import type { FinanceCategory, FinanceOperation } from '../../lib/financeTypes'
import { useT, type TranslationKey } from '../../lib/i18n'
import { reportError, useFinanceMeta, useFinancePermissions, type FinanceFilters } from './financeUi'
import { CategoryForm, OperationForm } from './OperationForm'
import { OperationsJournal } from './OperationsJournal'

/**
 * «Доходы и расходы» (ТЗ «Финансы» §7): тек дербес қолмен операциялардың реестрі (сатылым, жеткізушіге төлем, қарыз төлемі, аударым, инкассация жоқ).
 * Черновик — қалдыққа әсер етпейді; проведение серверде; санаттар — тип, P&L-класс, мұрағат.
 */
export function IncomeExpenseTab({ filters, storeIds }: { filters: FinanceFilters; storeIds?: string }) {
  const t = useT()
  const qc = useQueryClient()
  const { canOperate, canManage } = useFinancePermissions()
  const meta = useFinanceMeta(storeIds)
  const [form, setForm] = useState<{ kind: 'Income' | 'Expense'; draft?: FinanceOperation } | null>(null)
  const [categories, setCategories] = useState(false)
  const invalidate = () => void qc.invalidateQueries({ queryKey: financeKeys.all })

  return (
    <div className="flex flex-col gap-3">
      <OperationsJournal filters={filters} storeIds={storeIds} fixed={{ manualOnly: true }} show={['type', 'category', 'account', 'status', 'counterparty', 'search']}
        emptyText={t('fin.ie.empty')} onEditDraft={(op) => setForm({ kind: op.type === 'Income' ? 'Income' : 'Expense', draft: op })}
        actions={canOperate ? (
          <>
            {canManage && <Button variant="secondary" className="h-10! px-3!" icon={<Tags size={16} />} onClick={() => setCategories(true)}>{t('fin.categories')}</Button>}
            <Button variant="secondary" className="h-10! px-3! text-success!" icon={<ArrowDownLeft size={16} />} onClick={() => setForm({ kind: 'Income' })}>{t('fin.type.Income')}</Button>
            <Button className="h-10! px-3!" icon={<ArrowUpRight size={16} />} onClick={() => setForm({ kind: 'Expense' })}>{t('fin.type.Expense')}</Button>
          </>
        ) : undefined} />
      {form && (
        <OperationForm accounts={meta.data?.accounts ?? []} categories={meta.data?.categories ?? []} kind={form.kind} initial={form.draft}
          onClose={() => setForm(null)} onSaved={() => { setForm(null); invalidate() }} onCategoriesChanged={() => void meta.refetch()} />
      )}
      {categories && <CategoriesModal categories={meta.data?.categories ?? []} onClose={() => setCategories(false)} onChanged={() => { void meta.refetch(); invalidate() }} />}
    </div>
  )
}

/** Санаттар (§15.1): тізім, қосу, өңдеу, мұрағаттау/қайтару. Мұрағаттағы санат тарихта қалады. */
export function CategoriesModal({ categories, onClose, onChanged }: { categories: FinanceCategory[]; onClose: () => void; onChanged: () => void }) {
  const t = useT()
  const [editing, setEditing] = useState<{ type: 'Income' | 'Expense'; c?: FinanceCategory } | null>(null)
  const toggle = async (c: FinanceCategory) => {
    try { await financeApi.setCategoryActive(c.id, !c.isActive); toast(c.isActive ? t('fin.categoryArchived') : t('fin.categoryRestored')); onChanged() }
    catch (err) { reportError(err, t('fin.failed')) }
  }
  const group = (type: 'Income' | 'Expense') => (
    <div>
      <div className="mb-1 flex items-center justify-between">
        <h3 className="text-[14px] font-bold">{t(`fin.type.${type}`)}</h3>
        <Button variant="ghost" className="h-8! px-2! text-[13px]!" icon={<Plus size={14} />} onClick={() => setEditing({ type })}>{t('fin.addCategory')}</Button>
      </div>
      <ul className="divide-y divide-line rounded-xl border border-line">
        {categories.filter((c) => c.type === type).map((c) => (
          <li key={c.id} className={`flex items-center gap-3 px-3 py-2 text-[14px] ${c.isActive ? '' : 'text-ink-3'}`}>
            <span className="min-w-0 flex-1 truncate">{c.name}{!c.isActive && <span className="ml-1 text-[11px]">({t('fin.archived')})</span>}</span>
            <span className="text-[12px] text-ink-3">{t(`fin.pnl.class.${c.pnlClass}` as TranslationKey)}</span>
            <RowMenu items={[
              { label: t('fin.editCategory'), icon: <Pencil size={16} />, onClick: () => setEditing({ type, c }) },
              c.isActive ? { label: t('fin.archive'), icon: <Archive size={16} />, danger: true, onClick: () => void toggle(c) }
                : { label: t('fin.restore'), icon: <ArchiveRestore size={16} />, onClick: () => void toggle(c) },
            ]} />
          </li>
        ))}
        {categories.filter((c) => c.type === type).length === 0 && <li className="px-3 py-3 text-[13px] text-ink-3">—</li>}
      </ul>
    </div>
  )
  return (
    <Modal title={t('fin.categories')} onClose={onClose} width={560}>
      <div className="flex flex-col gap-5">{group('Income')}{group('Expense')}</div>
      {editing && <CategoryForm type={editing.type} initial={editing.c} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); onChanged() }} />}
    </Modal>
  )
}
