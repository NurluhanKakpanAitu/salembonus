import { useState, type FormEvent, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Archive, ArchiveRestore, ImagePlus, Pencil, Plus, X } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input, Select, Toggle } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { OrderControl } from '../ui/OrderControl'
import { RowMenu } from '../ui/RowMenu'
import { SearchInput } from '../ui/SearchInput'
import { StatusBadge } from '../ui/StatusBadge'
import { ApiError } from '../../lib/api'
import { catalogApi } from '../../lib/catalogApi'
import { catalogKeys, useCatalogAction, useCatalogPermissions } from '../../lib/catalogHooks'
import type { Brand, CatalogStatus, Characteristic, CharacteristicType, Unit } from '../../lib/catalogTypes'
import { useT, type TranslationKey } from '../../lib/i18n'
import { uploadImage } from '../../lib/upload'

const th = 'px-3 py-3 text-left text-[13px] font-semibold text-ink-2'
const td = 'px-3 py-2.5 text-[14px]'

function Table({ head, children, empty }: { head: ReactNode; children: ReactNode; empty: boolean }) {
  const t = useT()
  return (
    <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
      <table className="w-full min-w-[760px] border-collapse">
        <thead className="bg-field"><tr>{head}</tr></thead>
        <tbody>
          {empty ? <tr><td colSpan={10} className="px-3 py-10 text-center text-[14px] text-ink-3">{t('catalog.nothingFound')}</td></tr> : children}
        </tbody>
      </table>
    </div>
  )
}

function EditButton({ onClick }: { onClick: () => void }) {
  const t = useT()
  return (
    <Button variant="secondary" className="h-9! px-3! text-[13px]! text-brand!" icon={<Pencil size={15} />} onClick={onClick}>
      {t('catalog.edit')}
    </Button>
  )
}

/** Сақтау формасының ортақ бөлігі: қате өрісі мен батырмалар. */
function useSave(onSaved: (msg: string) => void) {
  const t = useT()
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(false)
  const save = async (action: () => Promise<unknown>, isNew: boolean) => {
    setLoading(true)
    setErrors({})
    try {
      await action()
      onSaved(isNew ? t('catalog.created') : t('catalog.saved'))
    } catch (err) {
      if (err instanceof ApiError && err.field) setErrors({ [err.field]: err.message })
      else setErrors({ form: err instanceof ApiError ? err.message : t('login.failed') })
    } finally {
      setLoading(false)
    }
  }
  return { errors, setErrors, loading, save }
}

function FormButtons({ loading, onClose, error }: { loading: boolean; onClose: () => void; error?: string }) {
  const t = useT()
  return (
    <>
      {error && <p className="text-[14px] text-danger">{error}</p>}
      <div className="mt-2 flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
        <Button type="submit" loading={loading}>{t('common.save')}</Button>
      </div>
    </>
  )
}

function StatusSelect({ value, onChange, gender }: { value: CatalogStatus; onChange: (v: CatalogStatus) => void; gender: 'f' | 'm' }) {
  const t = useT()
  return (
    <Field label={t('node.status')}>
      <Select value={value} onChange={(e) => onChange(e.target.value as CatalogStatus)}>
        <option value="Active">{t(`status.Active.${gender}` as TranslationKey)}</option>
        <option value="Archived">{t(`status.Archived.${gender}` as TranslationKey)}</option>
      </Select>
    </Field>
  )
}

// ---------------- Брендтер (ТЗ §9) ----------------

export function BrandsTab() {
  const t = useT()
  const perms = useCatalogPermissions()
  const run = useCatalogAction(catalogKeys.brands)
  const { data = [], isLoading } = useQuery({ queryKey: catalogKeys.brands, queryFn: catalogApi.brands })
  const [query, setQuery] = useState('')
  const [editing, setEditing] = useState<Brand | 'new' | null>(null)
  const list = data.filter((b) => b.name.toLowerCase().includes(query.trim().toLowerCase()))

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {perms.dictionaries && <Button icon={<Plus size={18} />} onClick={() => setEditing('new')}>{t('brand.add')}</Button>}
        <SearchInput className="ml-auto w-full sm:w-72" value={query} onChange={setQuery} placeholder={t('catalog.searchBrands')} />
      </div>
      <Table empty={!isLoading && list.length === 0} head={<>
        <th className={`${th} w-24`}>{t('brand.col.logo')}</th>
        <th className={th}>{t('brand.name')}</th>
        <th className={`${th} text-right`}>{t('catalog.col.products')}</th>
        <th className={th}>{t('catalog.col.status')}</th>
        <th className={th}>{t('catalog.col.order')}</th>
        <th className={`${th} text-right`}>{t('catalog.col.actions')}</th>
      </>}>
        {list.map((b) => (
          <tr key={b.id} className="border-t border-line">
            <td className={td}><BrandLogo brand={b} /></td>
            <td className={`${td} font-semibold`}>{b.name}</td>
            <td className={`${td} text-right text-brand`}>{b.productCount}</td>
            <td className={td}><StatusBadge active={b.status === 'Active'} label={t(`status.${b.status}.m` as TranslationKey)} /></td>
            <td className={td}>
              <OrderControl value={b.sortOrder} disabled={!perms.dictionaries}
                onUp={() => void run(() => catalogApi.reorderBrand(b.id, 'up'), t('catalog.saved'))}
                onDown={() => void run(() => catalogApi.reorderBrand(b.id, 'down'), t('catalog.saved'))} />
            </td>
            <td className={`${td} whitespace-nowrap text-right`}>
              {perms.dictionaries && (
                <div className="inline-flex items-center gap-1.5">
                  <EditButton onClick={() => setEditing(b)} />
                  <RowMenu items={[b.status === 'Active'
                    ? { label: t('catalog.archive'), icon: <Archive size={16} />, onClick: () => void run(() => catalogApi.updateBrand(b.id, { name: b.name, logoUrl: b.logoUrl, status: 'Archived' }), t('catalog.archived')) }
                    : { label: t('catalog.restore'), icon: <ArchiveRestore size={16} />, onClick: () => void run(() => catalogApi.updateBrand(b.id, { name: b.name, logoUrl: b.logoUrl, status: 'Active' }), t('catalog.restored')) }]} />
                </div>
              )}
            </td>
          </tr>
        ))}
      </Table>
      {editing && <BrandModal brand={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)}
        onSaved={(msg) => void run(async () => undefined, msg).then(() => setEditing(null))} />}
    </div>
  )
}

function BrandLogo({ brand }: { brand: Pick<Brand, 'name' | 'logoUrl'> }) {
  return brand.logoUrl
    ? <img src={brand.logoUrl} alt="" className="h-9 w-16 rounded-lg border border-line bg-white object-contain p-1" />
    : (
      <span className="flex h-9 w-16 items-center justify-center rounded-lg bg-field text-[13px] font-extrabold tracking-tight text-ink-2">
        {brand.name.slice(0, 3).toUpperCase()}
      </span>
    )
}

function BrandModal({ brand, onClose, onSaved }: { brand?: Brand; onClose: () => void; onSaved: (msg: string) => void }) {
  const t = useT()
  const [name, setName] = useState(brand?.name ?? '')
  const [logoUrl, setLogoUrl] = useState<string | null>(brand?.logoUrl ?? null)
  const [uploading, setUploading] = useState(false)
  const [status, setStatus] = useState<CatalogStatus>(brand?.status ?? 'Active')
  const { errors, setErrors, loading, save } = useSave(onSaved)

  const upload = async (file: File | undefined) => {
    if (!file) return
    setUploading(true)
    try {
      setLogoUrl(await uploadImage(file, 'brand'))
    } catch (err) {
      setErrors({ logoUrl: err instanceof Error ? err.message : String(err) })
    } finally {
      setUploading(false)
    }
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (!name.trim()) return setErrors({ name: t('common.required') })
    const body = { name, logoUrl, status }
    void save(() => (brand ? catalogApi.updateBrand(brand.id, body) : catalogApi.createBrand(body)), !brand)
  }

  return (
    <Modal title={brand ? t('brand.edit') : t('brand.add')} onClose={onClose}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <Field label={t('brand.name')} required error={errors.name}>
          <Input autoFocus value={name} onChange={(e) => setName(e.target.value)} error={!!errors.name} maxLength={100} />
        </Field>
        <Field label={t('brand.logo')} error={errors.logoUrl}>
          <div className="flex items-center gap-3">
            <BrandLogo brand={{ name: name || '—', logoUrl }} />
            <label className={`inline-flex h-10 cursor-pointer items-center gap-2 rounded-xl border border-line px-4 text-[14px] font-medium hover:bg-field ${uploading ? 'pointer-events-none opacity-60' : ''}`}>
              <ImagePlus size={17} /> {t('brand.uploadLogo')}
              <input type="file" accept="image/jpeg,image/png,image/webp" hidden onChange={(e) => { void upload(e.target.files?.[0]); e.target.value = '' }} />
            </label>
            {logoUrl && (
              <button type="button" onClick={() => setLogoUrl(null)} className="flex items-center gap-1 text-[13px] text-ink-3 hover:text-danger">
                <X size={14} /> {t('brand.removeLogo')}
              </button>
            )}
          </div>
        </Field>
        <StatusSelect value={status} onChange={setStatus} gender="m" />
        <FormButtons loading={loading || uploading} onClose={onClose} error={errors.form} />
      </form>
    </Modal>
  )
}

// ---------------- Өлшем бірліктері (ТЗ §10) ----------------

export function UnitsTab() {
  const t = useT()
  const perms = useCatalogPermissions()
  const run = useCatalogAction(catalogKeys.units)
  const { data = [], isLoading } = useQuery({ queryKey: catalogKeys.units, queryFn: catalogApi.units })
  const [editing, setEditing] = useState<Unit | 'new' | null>(null)

  return (
    <div>
      <div className="mb-4 flex items-center gap-2">
        {perms.dictionaries && <Button icon={<Plus size={18} />} onClick={() => setEditing('new')}>{t('unit.add')}</Button>}
      </div>
      <Table empty={!isLoading && data.length === 0} head={<>
        <th className={th}>{t('unit.name')}</th>
        <th className={th}>{t('unit.short')}</th>
        <th className={`${th} text-right`}>{t('catalog.col.products')}</th>
        <th className={th}>{t('catalog.col.status')}</th>
        <th className={`${th} text-right`}>{t('catalog.col.actions')}</th>
      </>}>
        {data.map((u) => (
          <tr key={u.id} className="border-t border-line">
            <td className={`${td} font-semibold`}>{u.name}</td>
            <td className={td}><span className="rounded-md bg-field px-2 py-1 text-[13px] font-semibold">{u.shortName}</span></td>
            <td className={`${td} text-right text-brand`}>{u.productCount}</td>
            <td className={td}><StatusBadge active={u.status === 'Active'} label={t(`status.${u.status}.f` as TranslationKey)} /></td>
            <td className={`${td} whitespace-nowrap text-right`}>
              {perms.dictionaries && (
                <div className="inline-flex items-center gap-1.5">
                  <EditButton onClick={() => setEditing(u)} />
                  <RowMenu items={[u.status === 'Active'
                    ? { label: t('catalog.archive'), icon: <Archive size={16} />, onClick: () => void run(() => catalogApi.updateUnit(u.id, { name: u.name, shortName: u.shortName, status: 'Archived' }), t('catalog.archived')) }
                    : { label: t('catalog.restore'), icon: <ArchiveRestore size={16} />, onClick: () => void run(() => catalogApi.updateUnit(u.id, { name: u.name, shortName: u.shortName, status: 'Active' }), t('catalog.restored')) }]} />
                </div>
              )}
            </td>
          </tr>
        ))}
      </Table>
      {editing && <UnitModal unit={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)}
        onSaved={(msg) => void run(async () => undefined, msg).then(() => setEditing(null))} />}
    </div>
  )
}

function UnitModal({ unit, onClose, onSaved }: { unit?: Unit; onClose: () => void; onSaved: (msg: string) => void }) {
  const t = useT()
  const [name, setName] = useState(unit?.name ?? '')
  const [shortName, setShortName] = useState(unit?.shortName ?? '')
  const [status, setStatus] = useState<CatalogStatus>(unit?.status ?? 'Active')
  const { errors, setErrors, loading, save } = useSave(onSaved)

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const errs: Record<string, string> = {}
    if (!name.trim()) errs.name = t('common.required')
    if (!shortName.trim()) errs.shortName = t('common.required')
    if (Object.keys(errs).length) return setErrors(errs)
    const body = { name, shortName, status }
    void save(() => (unit ? catalogApi.updateUnit(unit.id, body) : catalogApi.createUnit(body)), !unit)
  }

  return (
    <Modal title={unit ? t('unit.edit') : t('unit.add')} onClose={onClose}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <div className="grid grid-cols-[1fr_140px] gap-3">
          <Field label={t('unit.name')} required error={errors.name}>
            <Input autoFocus value={name} onChange={(e) => setName(e.target.value)} error={!!errors.name} maxLength={50} />
          </Field>
          <Field label={t('unit.short')} required error={errors.shortName}>
            <Input value={shortName} onChange={(e) => setShortName(e.target.value)} error={!!errors.shortName} maxLength={12} />
          </Field>
        </div>
        <StatusSelect value={status} onChange={setStatus} gender="f" />
        <FormButtons loading={loading} onClose={onClose} error={errors.form} />
      </form>
    </Modal>
  )
}

// ---------------- Сипаттамалар (ТЗ §11) ----------------

const TYPES: CharacteristicType[] = ['List', 'Text', 'Number', 'Boolean', 'Date', 'Range']

export function CharacteristicsTab() {
  const t = useT()
  const perms = useCatalogPermissions()
  const run = useCatalogAction(catalogKeys.characteristics)
  const { data = [], isLoading } = useQuery({ queryKey: catalogKeys.characteristics, queryFn: catalogApi.characteristics })
  const [editing, setEditing] = useState<Characteristic | 'new' | null>(null)

  const body = (c: Characteristic, status: CatalogStatus) => ({ name: c.name, type: c.type, isRequired: c.isRequired, options: c.options, status })

  return (
    <div>
      <div className="mb-4 flex items-center gap-2">
        {perms.dictionaries && <Button icon={<Plus size={18} />} onClick={() => setEditing('new')}>{t('char.add')}</Button>}
      </div>
      <Table empty={!isLoading && data.length === 0} head={<>
        <th className={th}>{t('char.name')}</th>
        <th className={th}>{t('char.type')}</th>
        <th className={th}>{t('char.values')}</th>
        <th className={th}>{t('char.col.required')}</th>
        <th className={th}>{t('catalog.col.status')}</th>
        <th className={th}>{t('catalog.col.order')}</th>
        <th className={`${th} text-right`}>{t('catalog.col.actions')}</th>
      </>}>
        {data.map((c) => (
          <tr key={c.id} className="border-t border-line">
            <td className={`${td} font-semibold`}>{c.name}</td>
            <td className={`${td} text-brand`}>{t(`char.type.${c.type}` as TranslationKey)}</td>
            <td className={`${td} max-w-72 truncate text-ink-2`} title={c.options.join(', ')}>{c.options.length ? c.options.join(', ') : '—'}</td>
            <td className={td}>
              <span className={`rounded-md px-2 py-1 text-[12px] font-semibold ${c.isRequired ? 'bg-success-soft text-success' : 'bg-field text-ink-3'}`}>
                {c.isRequired ? t('common.yes') : t('common.no')}
              </span>
            </td>
            <td className={td}><StatusBadge active={c.status === 'Active'} label={t(`status.${c.status}.f` as TranslationKey)} /></td>
            <td className={td}>
              <OrderControl value={c.sortOrder} disabled={!perms.dictionaries}
                onUp={() => void run(() => catalogApi.reorderCharacteristic(c.id, 'up'), t('catalog.saved'))}
                onDown={() => void run(() => catalogApi.reorderCharacteristic(c.id, 'down'), t('catalog.saved'))} />
            </td>
            <td className={`${td} whitespace-nowrap text-right`}>
              {perms.dictionaries && (
                <div className="inline-flex items-center gap-1.5">
                  <EditButton onClick={() => setEditing(c)} />
                  <RowMenu items={[c.status === 'Active'
                    ? { label: t('catalog.archive'), icon: <Archive size={16} />, onClick: () => void run(() => catalogApi.updateCharacteristic(c.id, body(c, 'Archived')), t('catalog.archived')) }
                    : { label: t('catalog.restore'), icon: <ArchiveRestore size={16} />, onClick: () => void run(() => catalogApi.updateCharacteristic(c.id, body(c, 'Active')), t('catalog.restored')) }]} />
                </div>
              )}
            </td>
          </tr>
        ))}
      </Table>
      {editing && <CharacteristicModal characteristic={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)}
        onSaved={(msg) => void run(async () => undefined, msg).then(() => setEditing(null))} />}
    </div>
  )
}

function CharacteristicModal({ characteristic: c, onClose, onSaved }: {
  characteristic?: Characteristic
  onClose: () => void
  onSaved: (msg: string) => void
}) {
  const t = useT()
  const [name, setName] = useState(c?.name ?? '')
  const [type, setType] = useState<CharacteristicType>(c?.type ?? 'List')
  const [options, setOptions] = useState<string[]>(c?.options.length ? c.options : [''])
  const [isRequired, setIsRequired] = useState(c?.isRequired ?? false)
  const [status, setStatus] = useState<CatalogStatus>(c?.status ?? 'Active')
  const { errors, setErrors, loading, save } = useSave(onSaved)

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (!name.trim()) return setErrors({ name: t('common.required') })
    const body = { name, type, isRequired, options: type === 'List' ? options.filter((o) => o.trim()) : [], status }
    void save(() => (c ? catalogApi.updateCharacteristic(c.id, body) : catalogApi.createCharacteristic(body)), !c)
  }

  return (
    <Modal title={c ? t('char.edit') : t('char.add')} onClose={onClose} width={480}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <Field label={t('char.name')} required error={errors.name}>
          <Input autoFocus value={name} onChange={(e) => setName(e.target.value)} error={!!errors.name} maxLength={100} />
        </Field>
        <Field label={t('char.type')} required error={errors.type}>
          <Select value={type} onChange={(e) => setType(e.target.value as CharacteristicType)}>
            {TYPES.map((ty) => <option key={ty} value={ty}>{t(`char.type.${ty}` as TranslationKey)}</option>)}
          </Select>
        </Field>
        {type === 'List' && (
          <Field label={t('char.values')} required error={errors.options}>
            <div className="flex flex-col gap-2">
              {options.map((o, i) => (
                <div key={i} className="flex items-center gap-2">
                  <Input value={o} maxLength={100} onChange={(e) => setOptions(options.map((x, j) => (j === i ? e.target.value : x)))} />
                  <button type="button" aria-label="Remove" onClick={() => setOptions(options.length > 1 ? options.filter((_, j) => j !== i) : [''])}
                    className="flex size-11 shrink-0 items-center justify-center rounded-xl text-ink-3 hover:bg-field hover:text-danger">
                    <X size={17} />
                  </button>
                </div>
              ))}
              <button type="button" onClick={() => setOptions([...options, ''])}
                className="inline-flex items-center gap-1.5 self-start text-[14px] font-semibold text-brand">
                <Plus size={16} /> {t('char.addValue')}
              </button>
            </div>
          </Field>
        )}
        <Toggle checked={isRequired} onChange={setIsRequired} label={t('char.required')} />
        <StatusSelect value={status} onChange={setStatus} gender="f" />
        <FormButtons loading={loading} onClose={onClose} error={errors.form} />
      </form>
    </Modal>
  )
}
