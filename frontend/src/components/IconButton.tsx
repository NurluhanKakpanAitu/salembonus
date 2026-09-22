import type { LucideIcon } from 'lucide-react'

export function IconButton({ icon: Icon, dot, label, onClick }: {
  icon: LucideIcon
  dot?: boolean
  label: string
  onClick?: () => void
}) {
  return (
    <button
      type="button"
      aria-label={label}
      onClick={onClick}
      className="relative flex size-11 items-center justify-center rounded-full bg-surface text-ink"
    >
      <Icon size={22} strokeWidth={1.8} />
      {dot && <span className="absolute right-2 top-2 size-2 rounded-full bg-brand ring-2 ring-surface" />}
    </button>
  )
}
