import type { ReactNode } from 'react'
import { Brand } from './Brand'

export function PageHeader({ right }: { right?: ReactNode }) {
  return (
    <header className="flex items-center justify-between py-2">
      <Brand />
      {right}
    </header>
  )
}

export function Avatar() {
  return (
    <div className="flex size-11 items-center justify-center rounded-full bg-gray-200 text-ink-2">
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <circle cx="12" cy="8" r="4" />
        <path d="M4 21c0-4 4-6 8-6s8 2 8 6" />
      </svg>
    </div>
  )
}
