import { create } from 'zustand'
import { cashierApi } from './cashierApi'
import type { CashierProduct, CashierSort } from './cashierTypes'

/**
 * Кассаның жергілікті каталогы: дүкеннің барлық сатылатын тауары браузерде (IndexedDB) сақталады, іздеу, санат сүзгісі мен
 * сұрыптау серверге бармай, бірден орындалады. Сервермен синхрондау — ашылғанда, минут сайын, сатылымнан кейін және бет
 * қайта көрінгенде; тек өзгерістер келеді (since/version). Қалдықты сатылым кезінде сервер бәрібір тексереді — жергілікті
 * қалдық бір минутқа ескірсе де, қоймадағыдан көп сатылмайды.
 */
export interface SyncItem {
  id: string
  name: string
  article: string | null
  barcodes: string[]
  imageUrl: string | null
  brandName: string | null
  unitShortName: string | null
  price: number | null
  stock: number
  nodeId: string | null
  /** Санат жолы: «id/id/id» (түбірден). */
  nodePath: string | null
  isRecommended: boolean
  bonusEligible: boolean
  /** Соңғы 30 күндегі сатылым — «Популярные». */
  sold: number
  /** Сипаттамалар, жеткізуші, өндіруші, ел, сипаттама — тек іздеу үшін. */
  extra: string | null
}

interface SyncResponse { serverTime: string; version: string; full: boolean; items: SyncItem[]; removed: string[]; total: number }

interface Snapshot { storeId: string; serverTime: string; version: string; items: SyncItem[] }

interface LocalCatalogState {
  storeId: string | null
  items: Map<string, SyncItem>
  /** Каталог жүктелді (кэштен не серверден) — іздеу жергілікті жүреді. */
  ready: boolean
  syncing: boolean
  serverTime: string | null
  version: string | null
}

export const useLocalCatalog = create<LocalCatalogState>(() => ({
  storeId: null, items: new Map(), ready: false, syncing: false, serverTime: null, version: null,
}))

// ---------- IndexedDB (браузер деректерді өшірсе не жеке режимде — жай ғана серверден қайта жүктеледі) ----------

const DB = 'salempos-catalog'
const STORE = 'snapshots'

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB, 1)
    req.onupgradeneeded = () => req.result.createObjectStore(STORE, { keyPath: 'storeId' })
    req.onsuccess = () => resolve(req.result)
    req.onerror = () => reject(req.error)
  })
}

async function readSnapshot(storeId: string): Promise<Snapshot | null> {
  try {
    const db = await openDb()
    return await new Promise((resolve) => {
      const req = db.transaction(STORE).objectStore(STORE).get(storeId)
      req.onsuccess = () => resolve((req.result as Snapshot | undefined) ?? null)
      req.onerror = () => resolve(null)
    })
  } catch {
    return null
  }
}

async function writeSnapshot(s: Snapshot) {
  try {
    const db = await openDb()
    db.transaction(STORE, 'readwrite').objectStore(STORE).put(s)
  } catch {
    /* сақталмаса — келесі ашылуда серверден толық жүктеледі */
  }
}

// ---------- Синхрондау ----------

let running: Promise<void> | null = null

/** Дүкен каталогын жүктейді: алдымен браузердегі көшірме (бірден іздеуге дайын), сосын сервердегі өзгерістер. */
export async function openLocalCatalog(storeId: string) {
  if (useLocalCatalog.getState().storeId !== storeId) {
    useLocalCatalog.setState({ storeId, items: new Map(), ready: false, serverTime: null, version: null })
    const snap = await readSnapshot(storeId)
    if (snap && useLocalCatalog.getState().storeId === storeId)
      useLocalCatalog.setState({ items: new Map(snap.items.map((i) => [i.id, i])), ready: true, serverTime: snap.serverTime, version: snap.version })
  }
  await syncLocalCatalog()
}

/** Өзгерістерді алу (бір уақытта біреуі ғана). Қате болса — ескі көшірмемен жұмыс жалғасады, келесі минутта қайталанады. */
export function syncLocalCatalog(): Promise<void> {
  running ??= (async () => {
    const { storeId } = useLocalCatalog.getState()
    if (!storeId) return
    useLocalCatalog.setState({ syncing: true })
    try {
      await pull(storeId, false)
    } catch {
      /* желі жоқ не сервер қате берді — бұрынғы каталог қалады */
    } finally {
      useLocalCatalog.setState({ syncing: false })
      running = null
    }
  })()
  return running
}

async function pull(storeId: string, forceFull: boolean) {
  const s = useLocalCatalog.getState()
  const res = await cashierApi.catalogSync<SyncResponse>(forceFull ? null : s.serverTime, forceFull ? null : s.version)
  if (useLocalCatalog.getState().storeId !== storeId) return // осы уақытта дүкен ауысты
  const items = res.full ? new Map<string, SyncItem>() : new Map(useLocalCatalog.getState().items)
  for (const item of res.items) items.set(item.id, item)
  for (const id of res.removed) items.delete(id)
  // Саны сервердегімен сәйкес келмесе (өзгеріс жоғалған не тауар өшірілген) — бір рет толық жүктейміз.
  if (items.size !== res.total && !res.full && !forceFull) return pull(storeId, true)
  useLocalCatalog.setState({ items, ready: true, serverTime: res.serverTime, version: res.version })
  void writeSnapshot({ storeId, serverTime: res.serverTime, version: res.version, items: [...items.values()] })
}

// ---------- Іздеу (сервердегі ProductSearch-пен бірдей ереже) ----------

export const searchTokens = (text: string) =>
  [...new Set(text.toLocaleLowerCase('ru').split(/[\s,;]+/).filter(Boolean))].slice(0, 8)

export interface IndexedItem { item: SyncItem; hay: string; path: string }

/** Әр тауарға іздеу мәтіні: атауы, артикулы, штрихкодтары, бренді, санаттары (жоғарғыларымен), қосымша өрістер — кіші әріппен. */
export function buildIndex(items: Map<string, SyncItem>, nodeNames: Map<string, string>): IndexedItem[] {
  return [...items.values()].map((item) => {
    const path = item.nodePath ? `/${item.nodePath}/` : ''
    const categories = item.nodePath ? item.nodePath.split('/').map((id) => nodeNames.get(id) ?? '').join(' ') : ''
    const hay = [item.name, item.article, item.barcodes.join(' '), item.brandName, categories, item.extra]
      .filter(Boolean).join(' ').toLocaleLowerCase('ru')
    return { item, hay, path }
  })
}

// Бір collator: localeCompare әр салыстыруда жаңасын жасайды — 10 000 нәтижеде сұрыптау бірнеше есе баяу.
const collator = new Intl.Collator('ru')
const byName = (a: SyncItem, b: SyncItem) => collator.compare(a.name, b.name)
const SORTS: Record<CashierSort, (a: SyncItem, b: SyncItem) => number> = {
  // «Популярные»: ұсынылатын, сатылымы көп, қоймада бар — сервердегімен бірдей рет.
  popular: (a, b) => Number(b.isRecommended) - Number(a.isRecommended) || b.sold - a.sold || Number(b.stock > 0) - Number(a.stock > 0) || byName(a, b),
  priceAsc: (a, b) => Number(a.price == null) - Number(b.price == null) || (a.price ?? 0) - (b.price ?? 0) || byName(a, b),
  priceDesc: (a, b) => Number(a.price == null) - Number(b.price == null) || (b.price ?? 0) - (a.price ?? 0) || byName(a, b),
  stock: (a, b) => b.stock - a.stock || byName(a, b),
}

/** Іздеу + санат + сұрыптау: әр сөз тауардың бір өрісінде кездесуі керек, сөздердің реті маңызды емес. */
export function searchLocal(index: IndexedItem[], q: { search: string; nodeId: string | null; sort: CashierSort }): SyncItem[] {
  const tokens = searchTokens(q.search)
  const node = q.nodeId ? `/${q.nodeId}/` : null
  return index
    .filter((x) => (!node || x.path.includes(node)) && tokens.every((t) => x.hay.includes(t)))
    .map((x) => x.item)
    .sort(SORTS[q.sort] ?? SORTS.popular)
}

export const toProduct = (i: SyncItem): CashierProduct => ({
  id: i.id, name: i.name, article: i.article, barcode: i.barcodes[0] ?? null, imageUrl: i.imageUrl, brandName: i.brandName,
  unitShortName: i.unitShortName, price: i.price, stock: i.stock, nodeId: i.nodeId, isRecommended: i.isRecommended, bonusEligible: i.bonusEligible,
})

/** Сканер: штрихкод жергілікті каталогта дәл сәйкес келсе — серверге бармай. */
export function findByBarcode(code: string): CashierProduct | null {
  for (const item of useLocalCatalog.getState().items.values())
    if (item.barcodes.includes(code)) return toProduct(item)
  return null
}
