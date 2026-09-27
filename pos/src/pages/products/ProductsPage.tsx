import { NavLink, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { NodeTreeTable } from '../../components/catalog/NodeTreeTable'
import { ProductListTab } from '../../components/products/ProductListTab'
import { ProductFormPage } from './ProductFormPage'
import { BrandsTab, CharacteristicsTab, UnitsTab } from '../../components/catalog/DictionaryTabs'
import { useT, type TranslationKey } from '../../lib/i18n'

// Абсолютті жолдар: splat-маршрут (/products/*) ішінде салыстырмалы сілтеме ағымдағы URL-ге
// қатысты шешіледі де, қойындылар дұрыс белгіленбейді.
const TABS: { path: string; label: TranslationKey }[] = [
  { path: '/products', label: 'products.tab.list' },
  { path: '/products/categories', label: 'products.tab.categories' },
  { path: '/products/groups', label: 'products.tab.groups' },
  { path: '/products/brands', label: 'products.tab.brands' },
  { path: '/products/units', label: 'products.tab.units' },
  { path: '/products/characteristics', label: 'products.tab.characteristics' },
]

/**
 * «Товар» бөлімі (ТЗ «Товар» §3): алты қойынды бір жұмыс аймағында ашылады,
 * бүкіл бетті жабатын терезе қолданылмайды. Тауар карточкасы — сол аймақтағы жеке бет.
 */
export function ProductsPage() {
  return (
    <Routes>
      <Route path="new" element={<ProductFormPage />} />
      <Route element={<TabsLayout />}>
        <Route index element={<ProductListTab />} />
        <Route path="categories" element={<NodeTreeTable mode="categories" />} />
        <Route path="groups" element={<NodeTreeTable mode="groups" />} />
        <Route path="brands" element={<BrandsTab />} />
        <Route path="units" element={<UnitsTab />} />
        <Route path="characteristics" element={<CharacteristicsTab />} />
      </Route>
      <Route path=":id" element={<ProductFormPage />} />
      <Route path="*" element={<Navigate to="/products" replace />} />
    </Routes>
  )
}

function TabsLayout() {
  const t = useT()
  return (
    <div>
      <nav className="-mx-4 mb-5 overflow-x-auto border-b border-line px-4 lg:-mx-6 lg:px-6">
        <ul className="flex gap-6">
          {TABS.map((tab) => (
            <li key={tab.path}>
              <NavLink to={tab.path} end={tab.path === '/products'}
                className={({ isActive }) =>
                  `block whitespace-nowrap border-b-2 pb-3 text-[15px] font-medium transition-colors ${
                    isActive ? 'border-brand text-brand' : 'border-transparent text-ink hover:text-brand'
                  }`}>
                {t(tab.label)}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
      <Outlet />
    </div>
  )
}
