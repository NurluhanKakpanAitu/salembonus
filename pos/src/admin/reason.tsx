import { useState } from 'react'
import { create } from 'zustand'
import { Button } from '../components/ui/Button'
import { Textarea } from '../components/ui/Field'
import { Modal } from '../components/ui/Modal'

let reasonResolver: ((v: string | null) => void) | null = null
const useReasonStore = create<{ message: string | null }>(() => ({ message: null }))

/** Себеп сұрайтын стильдегі диалог (браузердің prompt-ы емес). */
export function askReason(message: string): Promise<string | null> {
  return new Promise((resolve) => {
    reasonResolver = resolve
    useReasonStore.setState({ message })
  })
}

export function ReasonHost() {
  const message = useReasonStore((s) => s.message)
  const [text, setText] = useState('')
  if (!message) return null
  const done = (v: string | null) => { reasonResolver?.(v); reasonResolver = null; setText(''); useReasonStore.setState({ message: null }) }
  return (
    <Modal title="Укажите причину" onClose={() => done(null)} width={460}>
      <form onSubmit={(e) => { e.preventDefault(); if (text.trim().length >= 3) done(text.trim()) }} className="flex flex-col gap-3">
        <p className="text-[14px] text-ink-2">{message}</p>
        <Textarea value={text} maxLength={500} autoFocus onChange={(e) => setText(e.target.value)} className="min-h-20" />
        <div className="flex justify-end gap-2"><Button type="button" variant="secondary" onClick={() => done(null)}>Отмена</Button><Button type="submit" variant="danger" disabled={text.trim().length < 3}>Подтвердить</Button></div>
      </form>
    </Modal>
  )
}
