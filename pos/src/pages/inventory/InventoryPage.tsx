import { lazy, Suspense } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Loader2, PackageCheck, PackageX, AlertTriangle, Boxes, Package } from 'lucide-react'
import { NavLink, Navigate, Outlet, Route, Routes, useSearchParams } from 'react-router-dom'
import { StatCard, useInventoryPermissions } from '../../components/inventory/inventoryUi'
import { activeStore, useAuth } from '../../lib/auth'
import { inventoryApi, inventoryKeys } from '../../lib/inventoryApi'
import { num } from '../../lib/money'
import { useT, type TranslationKey } from '../../lib/i18n'

const StockTab = lazy(() => import('../../components/inventory/StockTab').then((m) => ({ default: m.StockTab })))
const WarehousesTab = lazy(() => import('../../components/inventory/WarehousesTab').then((m) => ({ default: m.WarehousesTab })))
const ReceiptsTab = lazy(() => import('../../components/inventory/ReceiptsTab').then((m) => ({ default: m.ReceiptsTab })))
const ReceiptForm = lazy(() => import('../../components/inventory/ReceiptForm').then((m) => ({ default: m.ReceiptForm })))
const WriteOffsTab = lazy(() => import('../../components/inventory/WriteOffsTab').then((m) => ({ default: m.WriteOffsTab })))
const WriteOffForm = lazy(() => import('../../components/inventory/WriteOffForm').then((m) => ({ default: m.WriteOffForm })))
const TransfersTab = lazy(() => import('../../components/inventory/TransfersTab').then((m) => ({ default: m.TransfersTab })))
const TransferForm = lazy(() => import('../../components/inventory/TransferForm').then((m) => ({ default: m.TransferForm })))
const SuppliersTab = lazy(() => import('../../components/inventory/SuppliersTab').then((m) => ({ default: m.SuppliersTab })))
const SupplierCard = lazy(() => import('../../components/inventory/SuppliersTab').then((m) => ({ default: m.SupplierCard })))
const MovementsTab = lazy(() => import('../../components/inventory/MovementsTab').then((m) => ({ default: m.MovementsTab })))

const TABS: { path: string; label: TranslationKey; perm?: 'receive' | 'writeOff' | 'suppliers' }[] = [
  { path: '/warehouse', label: 'inv.tab.stock' },
  { path: '/warehouse/warehouses', label: 'inv.tab.warehouses' },
  { path: '/warehouse/receipts', label: 'inv.tab.receipts', perm: 'receive' },
  { path: '/warehouse/transfers', label: 'inv.tab.transfers' },
  { path: '/warehouse/write-offs', label: 'inv.tab.writeOffs', perm: 'writeOff' },
  { path: '/warehouse/suppliers', label: 'inv.tab.suppliers', perm: 'suppliers' },
  { path: '/warehouse/movements', label: 'inv.tab.movements' },
]

const spinner = <div className="flex justify-center py-20 text-ink-3"><Loader2 className="animate-spin" size={28} /></div>

/**
 * «Склад» (ТЗ «Склад» §2, §4): KPI, жеті қойынды бір жұмыс аймағында; құжат формалары — сол аймақтағы бөлек беттер.
 * Ақшалай көрсеткіштер бас экранда жоқ (§4) — олар «Финансы»-да.
 */
export function InventoryPage() {
  return (
    <Suspense fallback={spinner}>
      <Routes>
        <Route path="receipts/new" element={<ReceiptForm />} />
        <Route path="receipts/quick" element={<ReceiptForm quick />} />
        <Route path="receipts/:id" element={<ReceiptForm />} />
        <Route path="write-offs/new" element={<WriteOffForm />} />
        <Route path="write-offs/:id" element={<WriteOffForm />} />
        <Route path="transfers/new" element={<TransferForm />} />
        <Route path="transfers/:id" element={<TransferForm />} />
        <Route path="suppliers/:id" element={<SupplierCard />} />
        <Route element={<TabsLayout />}>
          <Route index element={<StockTab />} />
          <Route path="warehouses" element={<WarehousesTab />} />
          <Route path="receipts" element={<ReceiptsTab />} />
          <Route path="transfers" element={<TransfersTab />} />
          <Route path="write-offs" element={<WriteOffsTab />} />
          <Route path="suppliers" element={<SuppliersTab />} />
          <Route path="movements" element={<MovementsTab />} />
        </Route>
        <Route path="*" element={<Navigate to="/warehouse" replace />} />
      </Routes>
    </Suspense>
  )
}

function TabsLayout() {
  const t = useT()
  const perms = useInventoryPermissions()
  const store = useAuth(activeStore)
  const [params] = useSearchParams()
  const warehouseId = params.get('warehouseId') ?? undefined
  const kpi = useQuery({ queryKey: [...inventoryKeys.kpi, store?.id, warehouseId], queryFn: () => inventoryApi.kpi(warehouseId) })
  const k = kpi.data
  const tabs = TABS.filter((tab) => !tab.perm || perms[tab.perm] || perms.view && tab.perm === 'suppliers' && perms.receive)

  return (
    <div>
      <div className="mb-4 grid grid-cols-2 gap-3 xl:grid-cols-4">
        <StatCard icon={<Package size={20} />} label={t('inv.kpi.kinds')} value={k ? num(k.productKinds) : '…'} hint={t('inv.kpi.kindsHint')} />
        <StatCard icon={<Boxes size={20} />} label={t('inv.kpi.units')} value={k ? num(k.totalUnits) : '…'} hint={t('inv.kpi.unitsHint')} tone="success" />
        <StatCard icon={<PackageCheck size={20} />} label={t('inv.kpi.withStock')} value={k ? num(k.withStock) : '…'} tone="violet"
          hint={k && k.productKinds > 0 ? t('inv.kpi.share', { p: Math.round((k.withStock / k.productKinds) * 100) }) : undefined} />
        <StatCard icon={k && k.belowMin > 0 ? <AlertTriangle size={20} /> : <PackageX size={20} />} label={t('inv.kpi.withoutStock')} value={k ? num(k.withoutStock) : '…'} tone="amber"
          hint={k ? t('inv.kpi.belowMin', { n: k.belowMin }) : undefined} />
      </div>
      <nav className="-mx-4 mb-5 overflow-x-auto border-b border-line px-4 lg:-mx-6 lg:px-6">
        <ul className="flex gap-6">
          {tabs.map((tab) => (
            <li key={tab.path}>
              <NavLink to={tab.path} end={tab.path === '/warehouse'}
                className={({ isActive }) => `block whitespace-nowrap border-b-2 pb-3 text-[15px] font-medium transition-colors ${
                  isActive ? 'border-brand text-brand' : 'border-transparent text-ink hover:text-brand'}`}>
                {t(tab.label)}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
      <Suspense fallback={spinner}><Outlet /></Suspense>
    </div>
  )
}
