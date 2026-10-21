import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Archive, ArchiveRestore, Copy, GitMerge, History, Loader2, MessageSquare, Pencil, PlusCircle, ShoppingCart, Smartphone, StickyNote } from 'lucide-react'
import { Button } from '../ui/Button'
import { Drawer } from '../ui/Drawer'
import { RowMenu, type RowMenuItem } from '../ui/RowMenu'
import { toast } from '../ui/Toast'
import { AccrueModal } from '../bonus/CustomerPanel'
import { Pill, StatusBadge } from '../bonus/bonusUi'
import { ApiError } from '../../lib/api'
import type { BonusCustomer } from '../../lib/bonusTypes'
import { clientKeys, clientsApi } from '../../lib/clientsApi'
import type { ClientCard } from '../../lib/clientsTypes'
import { dateOnly } from '../../lib/dates'
import { useT } from '../../lib/i18n'
import { ArchiveModal, HistoryDrawer, MergeModal, MessageModal } from './ClientModals'
import { ActivityPane, BonusPane, DebtsPane, DocumentsPane, NotesPane, OverviewPane, PurchasesPane, ReceiptsPane, SaleOverlay } from './ProfileTabs'
import { ActivityPill, Avatar, clientNo, useClientMeta } from './clientsUi'

type Tab = 'overview' | 'purchases' | 'receipts' | 'debts' | 'bonus' | 'activity' | 'documents' | 'notes'

function toBonusCustomer(card: ClientCard): BonusCustomer {
  const c = card.client
  return {
    customerId: c.customerId, name: c.name, phone: c.phone, cardCode: null, statusId: card.bonus.statusId, statusName: card.bonus.statusName,
    statusColor: card.bonus.statusColor, statusManual: false, balance: card.bonus.balance, totalSpent: c.amount, birthDate: c.birthDate, registeredAt: c.registeredAt,
    lastOperationAt: null, optOut: card.bonus.optOut,
  }
}

/**
 * Клиент профилі (ТЗ «Клиенты» §7–§13): оң жақ панель — тақырып (аты, мәртебе, ID, телефон, ⋯ мәзір), сегіз бөлім, төменде негізгі әрекеттер
 * («Создать продажу», «Начислить бонусы», «Отправить сообщение»). Мұрағаттағы карточка — тек оқу (қалпына келтіруге болады).
 */
export function ClientProfile({ id, storeIds, onClose, onEdit, onOpen, initialTab = 'overview', initialModal, messageTemplate }: {
  id: string; storeIds?: string; onClose: () => void; onEdit: (id: string) => void; onOpen: (id: string) => void; initialTab?: Tab
  initialModal?: 'message' | 'archive' | null; messageTemplate?: string
}) {
  const t = useT()
  const qc = useQueryClient()
  const nav = useNavigate()
  const meta = useClientMeta()
  const can = meta.data?.can
  const [tab, setTab] = useState<Tab>(initialTab)
  const [modal, setModal] = useState<'archive' | 'merge' | 'message' | 'history' | 'accrue' | null>(initialModal ?? (messageTemplate ? 'message' : null))
  const [sale, setSale] = useState<string | null>(null)
  const key = ['clients', 'card', id, storeIds]
  const q = useQuery({ queryKey: key, queryFn: () => clientsApi.card(id, storeIds) })
  const card = q.data
  const c = card?.client
  const archived = c?.isArchived ?? false
  const changed = (next?: ClientCard) => {
    if (next) qc.setQueryData(key, next)
    void qc.invalidateQueries({ queryKey: clientKeys.all })
  }

  const restore = async () => {
    try { changed(await clientsApi.restore(id)); toast(t('cli.restored')) } catch (err) { toast(err instanceof ApiError ? err.message : t('cli.failed'), 'error') }
  }

  const tabs: { key: Tab; label: string; show: boolean }[] = [
    { key: 'overview', label: t('cli.tab.overview'), show: true },
    { key: 'purchases', label: t('cli.tab.purchases'), show: true },
    { key: 'receipts', label: t('cli.tab.receipts'), show: true },
    { key: 'debts', label: t('cli.tab.debts'), show: !!can?.debts },
    { key: 'bonus', label: t('cli.tab.bonus'), show: !!can?.bonusView },
    { key: 'activity', label: t('cli.tab.activity'), show: true },
    { key: 'documents', label: t('cli.tab.documents'), show: true },
    { key: 'notes', label: `${t('cli.tab.notes')}${card?.notes ? ` (${card.notes})` : ''}`, show: true },
  ]

  const menu: RowMenuItem[] = card ? [
    ...(can?.edit && !archived ? [{ label: t('cli.menu.edit'), icon: <Pencil size={16} />, onClick: () => onEdit(id) }] : []),
    { label: t('cli.menu.copyId'), icon: <Copy size={16} />, onClick: () => { void navigator.clipboard?.writeText(clientNo(card.client.number)); toast(t('cli.menu.copied')) } },
    ...(can?.edit ? [{ label: t('cli.menu.addNote'), icon: <StickyNote size={16} />, onClick: () => setTab('notes') }] : []),
    { label: t('cli.menu.history'), icon: <History size={16} />, onClick: () => setModal('history') },
    ...(can?.merge && !archived ? [{ label: t('cli.menu.merge'), icon: <GitMerge size={16} />, onClick: () => setModal('merge') }] : []),
    ...(can?.archive && !archived ? [{ label: t('cli.menu.archive'), icon: <Archive size={16} />, danger: true, onClick: () => setModal('archive') }] : []),
    ...(can?.archive && archived && !card.mergedIntoId ? [{ label: t('cli.menu.restore'), icon: <ArchiveRestore size={16} />, onClick: () => void restore() }] : []),
  ] : []

  const title = c ? (
    <span className="flex min-w-0 items-center gap-3">
      <Avatar name={c.name} size={44} />
      <span className="min-w-0">
        <span className="block truncate">{c.name}</span>
        <span className="mt-0.5 flex flex-wrap items-center gap-1.5 text-[12px] font-normal">
          {card!.bonus.hasCard && <StatusBadge name={card!.bonus.statusName} color={card!.bonus.statusColor} />}
          <ActivityPill activity={c.activity} />
          {c.isNew && <Pill tone="violet">{t('cli.badge.new')}</Pill>}
          {c.hasApp && <Pill tone="blue"><Smartphone size={11} className="mr-0.5 inline" />{t('cli.badge.app')}</Pill>}
          {archived && <Pill tone="gray">{t('cli.archived')}</Pill>}
        </span>
      </span>
    </span>
  ) : t('cli.prof.title')

  return (
    <Drawer title={title} subtitle={c ? `ID ${clientNo(c.number)}${c.phone ? ` · ${c.phone}` : ''}` : undefined} onClose={onClose} width={600}
      footer={card && !archived && (
        <>
          {can?.edit && <Button variant="secondary" className="h-10! px-3.5! text-[14px]!" icon={<MessageSquare size={16} />} onClick={() => setModal('message')}>{t('cli.prof.message')}</Button>}
          {can?.bonusAccrue && card.bonus.hasCard && <Button variant="secondary" className="h-10! px-3.5! text-[14px]!" icon={<PlusCircle size={16} />} onClick={() => setModal('accrue')}>{t('cli.prof.accrue')}</Button>}
          {can?.salesCreate && <Button className="h-10! px-3.5! text-[14px]!" icon={<ShoppingCart size={16} />} onClick={() => nav(`/cashier?customer=${card.client.customerId}`)}>{t('cli.prof.sale')}</Button>}
        </>
      )}>
      {!card ? (
        q.isError ? <p className="py-16 text-center text-[13px] text-danger">{q.error instanceof ApiError ? q.error.message : t('cli.failed')}</p>
          : <div className="py-16 text-center text-ink-3"><Loader2 className="mx-auto animate-spin" size={24} /></div>
      ) : (
        <div className="flex flex-col gap-4">
          {archived && (
            <div className="rounded-xl border border-line bg-field px-3 py-2 text-[13px] text-ink-2">
              {card.mergedIntoId ? (
                <>{t('cli.prof.mergedInto', { id: clientNo(card.mergedIntoNumber ?? 0) })} <button type="button" className="font-semibold text-brand hover:underline" onClick={() => onOpen(card.mergedIntoId!)}>{t('cli.prof.openMain')}</button></>
              ) : t('cli.prof.archivedInfo', { date: card.archivedAt ? dateOnly(card.archivedAt) : '—', by: card.archivedBy ?? '—', reason: card.archiveReason ?? '—' })}
            </div>
          )}
          <div className="flex items-center gap-2">
            <nav className="-mx-1 flex min-w-0 flex-1 gap-1 overflow-x-auto border-b border-line px-1 [scrollbar-width:none]">
              {tabs.filter((x) => x.show).map((x) => (
                <button key={x.key} type="button" onClick={() => setTab(x.key)}
                  className={`-mb-px shrink-0 border-b-2 px-2.5 py-2 text-[13px] font-semibold whitespace-nowrap ${tab === x.key ? 'border-brand text-brand' : 'border-transparent text-ink-2 hover:text-ink'}`}>
                  {x.label}
                </button>
              ))}
            </nav>
            <RowMenu items={menu} />
          </div>
          {tab === 'overview' && <OverviewPane card={card} onSale={setSale} />}
          {tab === 'purchases' && <PurchasesPane card={card} storeIds={storeIds} onSale={setSale} />}
          {tab === 'receipts' && <ReceiptsPane card={card} storeIds={storeIds} onSale={setSale} />}
          {tab === 'debts' && <DebtsPane card={card} storeIds={storeIds} />}
          {tab === 'bonus' && <BonusPane card={card} onAccrue={can?.bonusAccrue && !archived ? () => setModal('accrue') : undefined} />}
          {tab === 'activity' && <ActivityPane card={card} storeIds={storeIds} />}
          {tab === 'documents' && <DocumentsPane card={card} storeIds={storeIds} onSale={setSale} />}
          {tab === 'notes' && <NotesPane card={card} canEdit={!!can?.edit} />}
        </div>
      )}
      {card && modal === 'archive' && <ArchiveModal card={card} onClose={() => setModal(null)} onDone={(next) => { setModal(null); changed(next); toast(t('cli.archive.done')) }} />}
      {card && modal === 'merge' && <MergeModal card={card} storeIds={storeIds} onClose={() => setModal(null)} onDone={(next) => { setModal(null); changed(); if (next.client.id !== id) onOpen(next.client.id); else qc.setQueryData(key, next) }} />}
      {card && modal === 'message' && <MessageModal card={card} template={messageTemplate === 'birthday' ? t('cli.bday.template', { name: card.client.firstName || card.client.name }) : messageTemplate} onClose={() => setModal(null)} onSent={() => { setModal(null); void qc.invalidateQueries({ queryKey: ['clients', 'activity', id] }) }} />}
      {modal === 'history' && <HistoryDrawer clientId={id} onClose={() => setModal(null)} />}
      {card && modal === 'accrue' && <AccrueModal customer={toBonusCustomer(card)} onClose={() => setModal(null)} onDone={() => { setModal(null); changed(); void qc.invalidateQueries({ queryKey: ['bonus'] }) }} />}
      {sale && <SaleOverlay saleId={sale} onClose={() => setSale(null)} />}
    </Drawer>
  )
}
