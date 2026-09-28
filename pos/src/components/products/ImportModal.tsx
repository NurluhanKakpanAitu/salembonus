import { useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, Download, FileSpreadsheet, Upload } from 'lucide-react'
import { Button } from '../ui/Button'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { ApiError } from '../../lib/api'
import { productApi, type ImportResult } from '../../lib/productApi'
import { useT } from '../../lib/i18n'

/**
 * Excel-ден импорт. Алдымен «Тексеру» — сервер файлды толық тексеріп, не жасалатынын, не
 * жаңартылатынын және қай жолдар өткізілетінін көрсетеді; ештеңе сақталмайды. Сосын ғана «Импорттау».
 */
export function ImportModal({ onClose }: { onClose: () => void }) {
  const t = useT()
  const qc = useQueryClient()
  const inputRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [createMissing, setCreateMissing] = useState(true)
  const [preview, setPreview] = useState<ImportResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState<'check' | 'import' | null>(null)

  const pick = (f: File | undefined) => {
    setFile(f ?? null)
    setPreview(null)
    setError(null)
  }

  const run = async (dryRun: boolean) => {
    if (!file) return
    setLoading(dryRun ? 'check' : 'import')
    setError(null)
    try {
      const result = await productApi.importXlsx(file, dryRun, createMissing)
      if (dryRun) {
        setPreview(result)
        return
      }
      // Импорт тауар, санат пен брендті өзгертеді — бүкіл каталогты жаңартамыз.
      await qc.invalidateQueries({ queryKey: ['catalog'] })
      toast(t('import.done', { created: result.created, updated: result.updated }))
      onClose()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err))
    } finally {
      setLoading(null)
    }
  }

  const ready = preview && preview.created + preview.updated > 0

  return (
    <Modal title={t('import.title')} onClose={onClose} width={640}>
      <div className="flex flex-col gap-4">
        <p className="text-[14px] leading-relaxed text-ink-2">{t('import.hint')}</p>
        <button type="button" onClick={() => void productApi.template().catch((e) => toast(String(e), 'error'))}
          className="flex items-center gap-2 self-start text-[14px] font-medium text-brand hover:underline">
          <Download size={16} /> {t('import.template')}
        </button>

        <button type="button" onClick={() => inputRef.current?.click()}
          className="flex items-center gap-3 rounded-xl border-2 border-dashed border-line px-4 py-5 text-left hover:border-brand hover:bg-brand-soft">
          <FileSpreadsheet size={28} className="shrink-0 text-brand" />
          <span className="min-w-0">
            <span className="block truncate text-[15px] font-semibold">{file ? file.name : t('import.choose')}</span>
            <span className="text-[13px] text-ink-3">{file ? `${(file.size / 1024).toFixed(0)} KB` : t('import.formats')}</span>
          </span>
        </button>
        <input ref={inputRef} type="file" hidden accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          onChange={(e) => { pick(e.target.files?.[0]); e.target.value = '' }} />

        <label className="flex cursor-pointer items-start gap-2.5 text-[14px]">
          <input type="checkbox" className="mt-0.5 size-4 accent-brand" checked={createMissing}
            onChange={(e) => { setCreateMissing(e.target.checked); setPreview(null) }} />
          <span>{t('import.createMissing')}</span>
        </label>

        {preview && (
          <div className="rounded-xl border border-line">
            <div className="grid grid-cols-3 divide-x divide-line border-b border-line text-center">
              <Stat label={t('import.created')} value={preview.created} />
              <Stat label={t('import.updated')} value={preview.updated} />
              <Stat label={t('import.skipped')} value={preview.skipped} danger={preview.skipped > 0} />
            </div>
            {(preview.createdCategories > 0 || preview.createdBrands > 0) && (
              <p className="border-b border-line px-4 py-2.5 text-[13px] text-ink-2">
                {t('import.willCreate', { categories: preview.createdCategories, brands: preview.createdBrands })}
              </p>
            )}
            {preview.errors.length > 0 ? (
              <ul className="max-h-56 overflow-y-auto text-[13px]">
                {preview.errors.map((e) => (
                  <li key={e.row} className="flex gap-3 border-b border-line px-4 py-2 last:border-0">
                    <span className="w-16 shrink-0 font-semibold text-ink-2">{t('import.row', { row: e.row })}</span>
                    <span className="text-danger">{e.message}</span>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="flex items-center gap-2 px-4 py-3 text-[14px] text-success"><CheckCircle2 size={17} /> {t('import.noErrors')}</p>
            )}
          </div>
        )}

        {error && <p className="text-[14px] text-danger">{error}</p>}
        <div className="flex flex-wrap justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="button" variant={preview ? 'secondary' : 'primary'} disabled={!file} loading={loading === 'check'}
            onClick={() => void run(true)}>
            {t('import.check')}
          </Button>
          {preview && (
            <Button type="button" icon={<Upload size={17} />} disabled={!ready} loading={loading === 'import'} onClick={() => void run(false)}>
              {t('import.apply', { count: preview.created + preview.updated })}
            </Button>
          )}
        </div>
      </div>
    </Modal>
  )
}

function Stat({ label, value, danger }: { label: string; value: number; danger?: boolean }) {
  return (
    <div className="px-3 py-3">
      <div className={`text-[22px] font-bold ${danger ? 'text-danger' : ''}`}>{value}</div>
      <div className="text-[12px] text-ink-2">{label}</div>
    </div>
  )
}
