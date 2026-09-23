export function PageTitle({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div>
      <h1 className="text-[24px] font-extrabold leading-tight tracking-[-0.02em]">{title}</h1>
      {subtitle && <p className="mt-0.5 text-[13px] text-ink-2">{subtitle}</p>}
    </div>
  )
}
