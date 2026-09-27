/** «Активный / В архиве» белгісі (макеттегі жасыл/сұр таблетка). */
export function StatusBadge({ active, label }: { active: boolean; label: string }) {
  return (
    <span className={`inline-flex h-7 items-center rounded-lg px-2.5 text-[12px] font-semibold ${
      active ? 'bg-success-soft text-success' : 'bg-field text-ink-3'
    }`}>
      {label}
    </span>
  )
}
