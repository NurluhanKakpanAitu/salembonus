const pad = (n: number) => String(n).padStart(2, '0')

/** Жергілікті күн «yyyy-MM-dd» (сервер оны дүкеннің уақыт белдеуімен түсінеді). */
export const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`

export const addDays = (d: Date, days: number) => {
  const x = new Date(d)
  x.setDate(x.getDate() + days)
  return x
}

/** «28.09.2026 14:25». */
export const dateTime = (iso: string) => {
  const d = new Date(iso)
  return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** «2026-10-05» → «05.10.2026». */
export const dateOnly = (iso: string) => {
  const [y, m, d] = iso.slice(0, 10).split('-')
  return `${d}.${m}.${y}`
}
