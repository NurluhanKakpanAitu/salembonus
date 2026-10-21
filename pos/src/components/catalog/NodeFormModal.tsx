import { useMemo, useState, type FormEvent } from 'react'
import { useMediaQuery } from '../../lib/useMediaQuery'
import { Clock, Folder, ImagePlus, Layers, Package, Trash2, X } from 'lucide-react'
import { Modal } from '../ui/Modal'
import { Button } from '../ui/Button'
import { Field, Input, Select, Textarea } from '../ui/Field'
import { ApiError } from '../../lib/api'
import { catalogApi } from '../../lib/catalogApi'
import { subtreeIds, treeOrder, useCatalogPermissions } from '../../lib/catalogHooks'
import type { CatalogNode, CatalogStatus } from '../../lib/catalogTypes'
import { CATALOG_ICONS } from '../../lib/catalogIcons'
import { uploadImage } from '../../lib/upload'
import { useT } from '../../lib/i18n'

/**
 * Санат / топ / топша формасы (ТЗ «Товар» §7.1–7.2, §8.1). Ата-ананы өзгерту — тасымалдау:
 * өзін не өз ұрпағын ата-ана етуге болмайды (тізімде олар көрсетілмейді, сервер де тексереді).
 * Белгіше тек санатқа (ата-анасы жоқ түйінге) міндетті; сурет — санатқа қалауынша (макет бойынша).
 */
/** Санат жолы таңдауда: тар экранда (модаль ені аз) тек соңғы екі буын — «… → Двигатель → Свечи», толық жол — кең экранда. */
function pathLabel(n: { depth: number; pathNames: string[] }, keep: number) {
  const path = keep > 0 && n.pathNames.length > keep ? `… → ${n.pathNames.slice(-keep).join(' → ')}` : n.pathNames.join(' → ')
  return `${'  '.repeat(n.depth)}${path}`
}
/** Қанша буын көрсетеміз: телефон — 1, планшет — 2, кең экран — бәрі (0). */
function useCompactPaths() {
  const phone = useMediaQuery('(max-width: 639px)')
  const tablet = useMediaQuery('(max-width: 1023px)')
  return phone ? 1 : tablet ? 2 : 0
}

export function NodeFormModal({ node, parentId, nodes, onClose, onSaved, onDelete }: {
  node?: CatalogNode
  /** Жаңа түйіннің ата-анасы (қай жерден «+» басылды). */
  parentId?: string | null
  nodes: CatalogNode[]
  onClose: () => void
  onSaved: (message: string) => void
  /** Өңдеуде: «Удалить категорию» батырмасы (ТЗ §7.3) — шешімді ата-компонент қабылдайды. */
  onDelete?: (node: CatalogNode) => void
}) {
  const t = useT()
  const compact = useCompactPaths()
  const perms = useCatalogPermissions()
  const [name, setName] = useState(node?.name ?? '')
  const [parent, setParent] = useState<string>(node ? (node.parentId ?? '') : (parentId ?? ''))
  const [icon, setIcon] = useState<string | null>(node?.icon ?? (parentId ? null : 'package'))
  const [imageUrl, setImageUrl] = useState<string | null>(node?.imageUrl ?? null)
  const [uploading, setUploading] = useState(false)
  const [status, setStatus] = useState<CatalogStatus>(node?.status ?? 'Active')
  const [sortOrder, setSortOrder] = useState<string>(node ? String(node.sortOrder) : '')
  const [description, setDescription] = useState(node?.description ?? '')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(false)

  const isRoot = parent === ''
  const excluded = useMemo(() => (node ? subtreeIds(nodes, node.id) : new Set<string>()), [node, nodes])
  const parentOptions = useMemo(
    () => treeOrder(nodes).filter((n) => !excluded.has(n.id) && (n.status === 'Active' || n.id === node?.parentId)),
    [nodes, excluded, node?.parentId],
  )

  const title = node
    ? node.parentId ? t('node.editGroup') : t('node.editCategory')
    : parentId ? t('node.newGroup') : t('node.newCategory')

  const upload = async (file: File | undefined) => {
    if (!file) return
    setUploading(true)
    try {
      setImageUrl(await uploadImage(file, 'category'))
    } catch (err) {
      setErrors((e) => ({ ...e, imageUrl: err instanceof Error ? err.message : String(err) }))
    } finally {
      setUploading(false)
    }
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const errs: Record<string, string> = {}
    if (!name.trim()) errs.name = t('common.required')
    setErrors(errs)
    if (errs.name) return

    const body = {
      name,
      parentId: parent || null,
      icon: icon,
      imageUrl: isRoot ? imageUrl : null,
      description: description || null,
      status,
      sortOrder: sortOrder.trim() ? Number(sortOrder) : null,
    }
    setLoading(true)
    try {
      if (node) await catalogApi.updateNode(node.id, body)
      else await catalogApi.createNode(body)
      onSaved(node ? t('catalog.saved') : t('catalog.created'))
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      else setErrors({ form: err instanceof ApiError ? err.message : t('login.failed') })
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title={title} onClose={onClose} width={node ? 820 : 680}>
      <form onSubmit={submit} noValidate>
        <div className={node ? 'grid gap-6 md:grid-cols-[1fr_260px]' : ''}>
          <div className="flex flex-col gap-4">
            <Field label={t('node.name')} required error={errors.name}>
              <Input autoFocus value={name} onChange={(e) => setName(e.target.value)} error={!!errors.name} maxLength={150} />
            </Field>

            <Field label={t('node.parent')} error={errors.parentId}>
              <Select value={parent} onChange={(e) => setParent(e.target.value)} error={!!errors.parentId}>
                <option value="">{t('node.root')}</option>
                {parentOptions.map((n) => (
                  <option key={n.id} value={n.id}>{pathLabel(n, compact)}</option>
                ))}
              </Select>
            </Field>

            {isRoot && (
              <Field label={t('node.icon')} required error={errors.icon}>
                <div className="grid max-h-44 grid-cols-8 gap-1.5 overflow-y-auto rounded-xl border border-line p-2 sm:grid-cols-10">
                  {Object.entries(CATALOG_ICONS).map(([key, Icon]) => (
                    <button key={key} type="button" aria-label={key} aria-pressed={icon === key} onClick={() => setIcon(key)}
                      className={`flex aspect-square items-center justify-center rounded-lg border transition-colors ${
                        icon === key ? 'border-brand bg-brand-soft text-brand' : 'border-transparent text-ink-2 hover:bg-field'
                      }`}>
                      <Icon size={20} />
                    </button>
                  ))}
                </div>
              </Field>
            )}

            {/* Сурет — тек санатқа, қалауынша (макет: «Изображение (необязательно)»). */}
            {isRoot && (
              <Field label={t('node.image')} error={errors.imageUrl}>
                <div className="flex flex-wrap items-center gap-3">
                  {imageUrl
                    ? <img src={imageUrl} alt="" className="h-24 w-40 rounded-xl border border-line bg-field object-cover" />
                    : <span className="flex h-24 w-40 items-center justify-center rounded-xl border border-dashed border-line text-ink-3"><ImagePlus size={22} /></span>}
                  <label className={`inline-flex h-10 cursor-pointer items-center gap-2 rounded-xl border border-line px-4 text-[14px] font-medium hover:bg-field ${uploading ? 'pointer-events-none opacity-60' : ''}`}>
                    <ImagePlus size={17} /> {t('node.chooseImage')}
                    <input type="file" accept="image/jpeg,image/png,image/webp" hidden onChange={(e) => { void upload(e.target.files?.[0]); e.target.value = '' }} />
                  </label>
                  {imageUrl && (
                    <button type="button" onClick={() => setImageUrl(null)} className="flex items-center gap-1 text-[13px] text-ink-3 hover:text-danger">
                      <X size={14} /> {t('node.removeImage')}
                    </button>
                  )}
                </div>
              </Field>
            )}

            <div className="grid grid-cols-2 gap-3">
              <Field label={t('node.status')} error={errors.status}>
                <Select value={status} onChange={(e) => setStatus(e.target.value as CatalogStatus)}>
                  <option value="Active">{t('status.Active.f')}</option>
                  <option value="Archived">{t('status.Archived.f')}</option>
                </Select>
              </Field>
              <Field label={t('node.order')}>
                <Input type="number" min={0} value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} placeholder="auto" />
              </Field>
            </div>

            <Field label={t('node.description')}>
              <Textarea value={description} onChange={(e) => setDescription(e.target.value)} maxLength={500} />
            </Field>
          </div>

          {node && (
            <aside className="flex flex-col gap-3">
              <div className="rounded-xl bg-field p-4">
                <div className="mb-3 text-[14px] font-bold">{t('node.stats')}</div>
                <Stat icon={<Package size={17} />} label={t('node.statProducts')} value={node.productCount} />
                <Stat icon={<Folder size={17} />} label={t('node.statGroups')} value={node.childCount} />
                <Stat icon={<Layers size={17} />} label={t('node.statSub')} value={node.descendantCount} />
                <Stat icon={<Clock size={17} />} label={t('node.statUpdated')} value={new Date(node.updatedAt).toLocaleString()} />
              </div>
              <p className="rounded-xl bg-brand-soft/60 p-3 text-[12px] leading-relaxed text-ink-2">{t('node.deleteHint')}</p>
            </aside>
          )}
        </div>

        {errors.form && <p className="mt-4 text-[14px] text-danger">{errors.form}</p>}
        <div className="mt-6 flex flex-wrap items-center justify-end gap-2">
          {node && onDelete && perms.lifecycle && (
            <Button type="button" variant="secondary" className="mr-auto text-danger!" icon={<Trash2 size={17} />} onClick={() => onDelete(node)}>
              {node.parentId ? t('node.deleteGroup') : t('node.deleteCategory')}
            </Button>
          )}
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={loading || uploading}>{t('common.save')}</Button>
        </div>
      </form>
    </Modal>
  )
}

function Stat({ icon, label, value }: { icon: React.ReactNode; label: string; value: string | number }) {
  return (
    <div className="flex items-center justify-between gap-3 py-1.5 text-[13px]">
      <span className="flex items-center gap-2 text-ink-2">{icon} {label}</span>
      <span className="font-semibold text-ink">{value}</span>
    </div>
  )
}

/**
 * Түйінді тасымалдау (ТЗ §8.2, §15, §22.6): жаңа ата-ана таңдалады, сақтағанға дейін ескі және жаңа жол
 * көрінеді. Ішкі топтар мен тауарлар түйінмен бірге жаңа тармаққа көшеді. Циклді сервер де тексереді.
 */
export function MoveNodeModal({ node, nodes, onClose, onMoved }: {
  node: CatalogNode
  nodes: CatalogNode[]
  onClose: () => void
  onMoved: (message: string) => void
}) {
  const t = useT()
  const compact = useCompactPaths()
  const excluded = useMemo(() => subtreeIds(nodes, node.id), [nodes, node.id])
  const options = useMemo(
    () => treeOrder(nodes).filter((n) => !excluded.has(n.id) && n.status === 'Active' && n.id !== node.parentId),
    [nodes, excluded, node.parentId],
  )
  // Түбірге тек белгішесі бар түйін шыға алады: санатқа белгіше міндетті.
  const rootAllowed = node.parentId !== null && !!node.icon
  const [target, setTarget] = useState<string>(options[0]?.id ?? (rootAllowed ? '' : ''))
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const targetNode = target ? nodes.find((n) => n.id === target) : undefined
  const newPath = [...(targetNode?.pathNames ?? []), node.name]

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!target && !rootAllowed) return
    setLoading(true)
    setError(null)
    try {
      await catalogApi.updateNode(node.id, {
        parentId: target || null, name: node.name, icon: node.icon, imageUrl: node.imageUrl,
        description: node.description, status: node.status, sortOrder: null,
      })
      onMoved(t('catalog.moved'))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('login.failed'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title={t('node.moveNodeTitle', { name: node.name })} onClose={onClose} width={560}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <p className="text-[14px] leading-relaxed text-ink-2">{t('node.moveNodeHint')}</p>
        <Field label={t('node.newParent')} error={error}>
          <Select value={target} onChange={(e) => setTarget(e.target.value)}>
            {rootAllowed && <option value="">{t('node.root')}</option>}
            {options.map((n) => <option key={n.id} value={n.id}>{pathLabel(n, compact)}</option>)}
          </Select>
        </Field>
        <div className="grid gap-2 rounded-xl bg-field px-4 py-3 text-[14px]">
          <div><span className="text-ink-2">{t('node.currentPath')}: </span><span className="font-semibold">{node.pathNames.join(' → ')}</span></div>
          <div><span className="text-ink-2">{t('node.newPath')}: </span><span className="font-semibold text-brand">{newPath.join(' → ')}</span></div>
        </div>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={loading} disabled={!target && !rootAllowed}>{t('catalog.move')}</Button>
        </div>
      </form>
    </Modal>
  )
}

/** Мазмұнды ауыстыру: түйіннің балалары мен тауарлары басқа түйінге (ТЗ §7.3, §22.7). */
export function MoveContentModal({ node, nodes, onClose, onMoved }: {
  node: CatalogNode
  nodes: CatalogNode[]
  onClose: () => void
  onMoved: (message: string) => void
}) {
  const t = useT()
  const compact = useCompactPaths()
  const excluded = useMemo(() => subtreeIds(nodes, node.id), [nodes, node.id])
  const options = useMemo(() => treeOrder(nodes).filter((n) => !excluded.has(n.id) && n.status === 'Active'), [nodes, excluded])
  const [target, setTarget] = useState(options[0]?.id ?? '')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!target) return
    setLoading(true)
    try {
      await catalogApi.moveContent(node.id, target)
      onMoved(t('catalog.moved'))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('login.failed'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title={t('catalog.moveContent')} onClose={onClose} width={520}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <p className="text-[14px] leading-relaxed text-ink-2">{t('node.moveHint', { name: node.name })}</p>
        <Field label={t('node.moveTo')} error={error}>
          <Select value={target} onChange={(e) => setTarget(e.target.value)}>
            {options.map((n) => <option key={n.id} value={n.id}>{pathLabel(n, compact)}</option>)}
          </Select>
        </Field>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" loading={loading} disabled={!target}>{t('catalog.moveContent')}</Button>
        </div>
      </form>
    </Modal>
  )
}

/**
 * Түйінді архивтеу (ТЗ §22.7) немесе өшіру әрекеті (ТЗ §7.3, §16): ішінде не бар екенін көрсетіп, екі жол
 * ұсынады — мазмұнды басқа жерге ауыстыру не бәрін бірге архивтеу. Ішінде топ не тауар болса, физикалық
 * өшіру жоқ: тарих сақталуы керек. Тауарлар архивтен санатпен бірге қайтады.
 */
export function ArchiveNodeModal({ node, mode = 'archive', onClose, onArchived, onMoveContent }: {
  node: CatalogNode
  mode?: 'archive' | 'delete'
  onClose: () => void
  onArchived: (message: string) => void
  onMoveContent: () => void
}) {
  const t = useT()
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const hasContent = node.descendantCount > 0 || node.productCount > 0

  const archive = async () => {
    setLoading(true)
    try {
      await catalogApi.archiveNode(node.id)
      onArchived(t('catalog.archived'))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('login.failed'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title={mode === 'delete' ? t('catalog.deleteTitle', { name: node.name }) : t('catalog.archiveTitle', { name: node.name })} onClose={onClose} width={520}>
      <div className="flex flex-col gap-4">
        {hasContent ? (
          <>
            <div className="grid grid-cols-2 gap-3">
              <div className="rounded-xl bg-field px-4 py-3">
                <div className="text-[13px] text-ink-2">{t('catalog.archiveNested')}</div>
                <div className="text-[22px] font-bold">{node.descendantCount}</div>
              </div>
              <div className="rounded-xl bg-field px-4 py-3">
                <div className="text-[13px] text-ink-2">{t('catalog.col.products')}</div>
                <div className="text-[22px] font-bold">{node.productCount}</div>
              </div>
            </div>
            <p className="text-[14px] leading-relaxed text-ink-2">{mode === 'delete' ? t('catalog.deleteBlocked') : t('catalog.archiveChoice')}</p>
          </>
        ) : (
          <p className="text-[14px] leading-relaxed text-ink-2">{t('catalog.archiveEmpty')}</p>
        )}
        {error && <p className="text-[14px] text-danger">{error}</p>}
        <div className="flex flex-wrap justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          {hasContent && <Button type="button" variant="secondary" onClick={onMoveContent}>{t('catalog.moveContent')}</Button>}
          <Button type="button" loading={loading} onClick={() => void archive()}>
            {hasContent ? t('catalog.archiveAll') : t('catalog.archive')}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
