import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Bold, Eye, Heading2, History, Italic, Link2, List, ListOrdered, Redo2, Save, Send, Underline, Undo2 } from 'lucide-react'
import { Button } from '../ui/Button'
import { Field, Input } from '../ui/Field'
import { Modal } from '../ui/Modal'
import { toast } from '../ui/Toast'
import { confirmDialog } from '../ui/Confirm'
import { bonusApi } from '../../lib/bonusApi'
import type { BonusTerms } from '../../lib/bonusTypes'
import { dateOnly, dateTime, isoDate } from '../../lib/dates'
import { useT, type TranslationKey } from '../../lib/i18n'
import { BlockError, Card, Skeleton } from '../stats/statsUi'
import { Pill, reportError, useBonusPermissions } from './bonusUi'

/**
 * «Правила программы» (ТЗ «Бонусы» §9): мәтін редакторы (тақырып, тізімдер, жуан/көлбеу/асты сызылған, сілтеме, болдырмау/қайталау),
 * черновик → алдын ала қарау → жариялау (нұсқа нөмірі мен күшіне ену күні), нұсқалар тарихы, ескі нұсқаны жаңа черновикке қалпына келтіру.
 * Сандық баптаулар (пайыз, мерзім, мәртебелер) — өз бөлімдерінде; HTML серверде тазаланады.
 */
export function RulesTab() {
  const t = useT()
  const perms = useBonusPermissions()
  const q = useQuery({ queryKey: ['bonus', 'terms'], queryFn: bonusApi.terms })
  const editor = useRef<HTMLDivElement>(null)
  const [dirty, setDirty] = useState(false)
  const [busy, setBusy] = useState<'save' | 'publish' | null>(null)
  const [preview, setPreview] = useState<string | null>(null)
  const [versions, setVersions] = useState(false)
  const [publish, setPublish] = useState(false)
  const [linkOpen, setLinkOpen] = useState(false)
  const range = useRef<Range | null>(null)
  const d = q.data
  const current = d?.draft ?? d?.published ?? null

  useEffect(() => {
    if (!d || !editor.current) return
    editor.current.innerHTML = (d.draft ?? d.published)?.content ?? d.template
    setDirty(!d.draft && !d.published)
  }, [d])

  const exec = (cmd: string, value?: string) => { editor.current?.focus(); document.execCommand(cmd, false, value); setDirty(true) }
  // Сілтеме — сайт стиліндегі терезеде (браузердің prompt-ы емес); белгіленген мәтін сақталып, қайта қойылады.
  const link = () => {
    const sel = window.getSelection()
    range.current = sel && sel.rangeCount > 0 ? sel.getRangeAt(0).cloneRange() : null
    setLinkOpen(true)
  }
  const applyLink = (url: string) => {
    setLinkOpen(false)
    editor.current?.focus()
    const sel = window.getSelection()
    if (range.current && sel) { sel.removeAllRanges(); sel.addRange(range.current) }
    exec('createLink', url)
  }
  const save = async () => {
    setBusy('save')
    try { await bonusApi.saveTerms(editor.current?.innerHTML ?? ''); toast(t('bon.rules.saved')); setDirty(false); void q.refetch() } catch (err) { reportError(err, t('bon.failed')) } finally { setBusy(null) }
  }
  const restore = async (v: BonusTerms) => {
    if (dirty && !(await confirmDialog({ message: t('bon.rules.restoreDirty'), confirmLabel: t('bon.rules.restore') }))) return
    try { await bonusApi.restoreTerms(v.id); toast(t('bon.rules.restored', { v: v.version })); setVersions(false); void q.refetch() } catch (err) { reportError(err, t('bon.failed')) }
  }
  if (q.isError) return <BlockError message={(q.error as Error).message} onRetry={() => void q.refetch()} />
  const tool = (icon: React.ReactNode, label: string, onClick: () => void) => (
    <button type="button" title={label} aria-label={label} onMouseDown={(e) => e.preventDefault()} onClick={onClick} className="flex size-8 items-center justify-center rounded-lg text-ink-2 hover:bg-field hover:text-ink">{icon}</button>
  )

  return (
    <div className="flex flex-col gap-3">
      <Card title={t('bon.rules.title')} action={
        <div className="flex flex-wrap items-center gap-2 text-[12px] text-ink-2">
          {d?.published ? <Pill tone="green">{t('bon.rules.publishedV', { v: d.published.version })}</Pill> : <Pill tone="gray">{t('bon.rules.notPublished')}</Pill>}
          {d?.draft && <Pill tone="amber">{t('bon.rules.draft')}</Pill>}
          {d?.published && <span>{t('bon.rules.publishedAt', { date: dateTime(d.published.publishedAt!), who: d.published.publishedBy ?? '' })}</span>}
        </div>
      }>
        {!d ? <Skeleton className="h-96" /> : (
          <div className="flex flex-col gap-3">
            {perms.terms && (
              <div className="flex flex-wrap items-center gap-0.5 rounded-xl border border-line bg-field/40 p-1">
                {tool(<Bold size={16} />, t('bon.rules.bold'), () => exec('bold'))}
                {tool(<Italic size={16} />, t('bon.rules.italic'), () => exec('italic'))}
                {tool(<Underline size={16} />, t('bon.rules.underline'), () => exec('underline'))}
                {tool(<Heading2 size={16} />, t('bon.rules.heading'), () => exec('formatBlock', 'h2'))}
                {tool(<ListOrdered size={16} />, t('bon.rules.ol'), () => exec('insertOrderedList'))}
                {tool(<List size={16} />, t('bon.rules.ul'), () => exec('insertUnorderedList'))}
                {tool(<Link2 size={16} />, t('bon.rules.link'), link)}
                <span className="mx-1 h-5 w-px bg-line" />
                {tool(<Undo2 size={16} />, t('bon.rules.undo'), () => exec('undo'))}
                {tool(<Redo2 size={16} />, t('bon.rules.redo'), () => exec('redo'))}
              </div>
            )}
            <div ref={editor} contentEditable={perms.terms} suppressContentEditableWarning onInput={() => setDirty(true)}
              className="prose-rules min-h-[420px] rounded-xl border border-line bg-surface p-5 text-[15px] leading-relaxed outline-none focus:border-brand [&_a]:text-brand [&_a]:underline [&_h2]:mb-3 [&_h2]:text-[20px] [&_h2]:font-bold [&_h3]:font-bold [&_li]:mb-1.5 [&_ol]:list-decimal [&_ol]:pl-6 [&_p]:mb-2 [&_ul]:list-disc [&_ul]:pl-6" />
            <p className="text-[12px] text-ink-3">{t('bon.rules.legal')}</p>
            <div className="flex flex-wrap gap-2">
              <Button variant="secondary" icon={<History size={16} />} onClick={() => setVersions(true)}>{t('bon.rules.versions')}</Button>
              <Button variant="secondary" icon={<Eye size={16} />} onClick={() => setPreview(editor.current?.innerHTML ?? '')}>{t('bon.rules.preview')}</Button>
              <span className="ml-auto" />
              {perms.terms && <Button variant="secondary" icon={<Save size={16} />} loading={busy === 'save'} disabled={!dirty} onClick={() => void save()}>{t('bon.rules.saveDraft')}</Button>}
              {perms.terms && <Button icon={<Send size={16} />} disabled={dirty || !d.draft} title={dirty ? t('bon.rules.saveFirst') : undefined} onClick={() => setPublish(true)}>{t('bon.rules.publish')}</Button>}
            </div>
            {current?.basedOnVersion && d.draft && <p className="text-[12px] text-ink-3">{t('bon.rules.basedOn', { v: current.basedOnVersion })}</p>}
          </div>
        )}
      </Card>
      {preview !== null && (
        <Modal title={t('bon.rules.preview')} onClose={() => setPreview(null)} width={720}>
          <div className="rounded-2xl bg-field/50 p-4"><div className="mx-auto max-w-md rounded-2xl bg-surface p-5 text-[14px] leading-relaxed shadow-sm [&_a]:text-brand [&_h2]:mb-3 [&_h2]:text-[18px] [&_h2]:font-bold [&_li]:mb-1 [&_ol]:list-decimal [&_ol]:pl-5 [&_p]:mb-2 [&_ul]:list-disc [&_ul]:pl-5"
            dangerouslySetInnerHTML={{ __html: sanitize(preview) }} /></div>
          <p className="mt-3 text-[12px] text-ink-3">{t('bon.rules.previewHint')}</p>
        </Modal>
      )}
      {versions && d && (
        <Modal title={t('bon.rules.versions')} onClose={() => setVersions(false)} width={640}>
          {d.versions.length === 0 ? <p className="py-6 text-center text-ink-3">{t('bon.rules.noVersions')}</p> : (
            <ul className="flex flex-col divide-y divide-line rounded-xl border border-line">
              {d.versions.map((v) => (
                <li key={v.id} className="flex items-center gap-3 px-3 py-2 text-[13px]">
                  <b>v{v.version}</b>
                  <Pill tone={v.status === 'Published' ? 'green' : 'gray'}>{t(`bon.rules.st.${v.status}` as TranslationKey)}</Pill>
                  <span className="min-w-0 flex-1 truncate text-ink-2">{v.publishedAt ? dateTime(v.publishedAt) : ''} · {v.publishedBy ?? v.author}{v.effectiveFrom ? ` · ${t('bon.rules.effective', { date: dateOnly(v.effectiveFrom) })}` : ''}{v.comment ? ` · ${v.comment}` : ''}</span>
                  <Button variant="ghost" className="h-8! px-2!" onClick={() => setPreview(v.content)}>{t('bon.open')}</Button>
                  {perms.terms && <Button variant="secondary" className="h-8! px-2!" onClick={() => void restore(v)}>{t('bon.rules.restore')}</Button>}
                </li>
              ))}
            </ul>
          )}
        </Modal>
      )}
      {linkOpen && <LinkModal onClose={() => setLinkOpen(false)} onApply={applyLink} />}
      {publish && d && <PublishModal next={(d.versions[0]?.version ?? 0) + 1} onClose={() => setPublish(false)} onDone={() => { setPublish(false); void q.refetch() }} busy={busy === 'publish'} setBusy={(b) => setBusy(b ? 'publish' : null)} />}
    </div>
  )
}

function LinkModal({ onClose, onApply }: { onClose: () => void; onApply: (url: string) => void }) {
  const t = useT()
  const [url, setUrl] = useState('https://')
  const valid = /^(https?:\/\/.+|mailto:.+|tel:.+)/i.test(url.trim())
  return (
    <Modal title={t('bon.rules.link')} onClose={onClose} width={440}>
      <form onSubmit={(e) => { e.preventDefault(); if (valid) onApply(url.trim()) }} className="flex flex-col gap-4">
        <Field label={t('bon.rules.linkUrl')} hint={t('bon.rules.linkHint')}><Input autoFocus value={url} onChange={(e) => setUrl(e.target.value)} /></Field>
        <div className="flex justify-end gap-2"><Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={!valid}>{t('bon.rules.insert')}</Button></div>
      </form>
    </Modal>
  )
}

function PublishModal({ next, onClose, onDone, busy, setBusy }: { next: number; onClose: () => void; onDone: () => void; busy: boolean; setBusy: (b: boolean) => void }) {
  const t = useT()
  const [date, setDate] = useState(isoDate(new Date()))
  const [comment, setComment] = useState('')
  const submit = async () => {
    setBusy(true)
    try { await bonusApi.publishTerms(date, comment); toast(t('bon.rules.published', { v: next })); onDone() } catch (err) { reportError(err, t('bon.failed')) } finally { setBusy(false) }
  }
  return (
    <Modal title={t('bon.rules.publishTitle', { v: next })} onClose={onClose} width={460}>
      <div className="flex flex-col gap-4">
        <p className="text-[13px] text-ink-2">{t('bon.rules.publishHint')}</p>
        <Field label={t('bon.rules.effectiveFrom')}><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        <Field label={t('bon.comment')}><Input value={comment} maxLength={500} onChange={(e) => setComment(e.target.value)} /></Field>
        <div className="flex justify-end gap-2"><Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button loading={busy} onClick={() => void submit()}>{t('bon.rules.publish')}</Button></div>
      </div>
    </Modal>
  )
}

/** Алдын ала қарау үшін клиенттік тазалау (сервер де тазалайды): тек рұқсат етілген тегтер, атрибутсыз, қауіпсіз сілтеме. */
function sanitize(html: string) {
  const doc = new DOMParser().parseFromString(html, 'text/html')
  const allowed = new Set(['P', 'BR', 'B', 'STRONG', 'I', 'EM', 'U', 'H2', 'H3', 'UL', 'OL', 'LI', 'A', 'DIV', 'SPAN'])
  const walk = (el: Element) => {
    for (const child of Array.from(el.children)) {
      if (!allowed.has(child.tagName)) { child.replaceWith(...child.childNodes); continue }
      const href = child.tagName === 'A' ? child.getAttribute('href') ?? '' : ''
      for (const a of Array.from(child.attributes)) child.removeAttribute(a.name)
      if (child.tagName === 'A' && /^(https?:|mailto:|tel:)/i.test(href)) { child.setAttribute('href', href); child.setAttribute('target', '_blank'); child.setAttribute('rel', 'noopener noreferrer') }
      walk(child)
    }
  }
  walk(doc.body)
  return doc.body.innerHTML
}
