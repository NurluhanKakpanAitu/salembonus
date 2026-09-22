export function PageTitle({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div>
      <h1 className="text-[26px] font-extrabold leading-tight tracking-tight">{title}</h1>
      {subtitle && <p className="mt-0.5 text-[13px] text-ink-2">{subtitle}</p>}
    </div>
  )
}
