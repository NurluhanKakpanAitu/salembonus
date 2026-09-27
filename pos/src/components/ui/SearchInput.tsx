import { Search, X } from 'lucide-react'

export function SearchInput({ value, onChange, placeholder, className = '' }: {
  value: string
  onChange: (value: string) => void
  placeholder: string
  className?: string
}) {
  return (
    <label className={`flex h-11 items-center gap-2.5 rounded-xl border border-line bg-surface px-3.5 focus-within:border-brand ${className}`}>
      <Search size={18} className="shrink-0 text-ink-3" />
      <input value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder}
        className="min-w-0 flex-1 bg-transparent text-[14px] outline-none placeholder:text-ink-3" />
      {value && (
        <button type="button" aria-label="Clear" onClick={() => onChange('')} className="text-ink-3 hover:text-ink">
          <X size={16} />
        </button>
      )}
    </label>
  )
}
