/** Тек сандар, 7-мен басталатын 11 таңбаға дейін. "8..." -> "7...". */
export function normalizeDigits(raw: string): string {
  let d = raw.replace(/\D/g, '')
  if (d.startsWith('8')) d = '7' + d.slice(1)
  if (!d.startsWith('7')) d = '7' + d
  return d.slice(0, 11)
}

/** "77011234567" -> "+7 701 123 45 67" (терілген шамасына қарай) */
export function formatPhoneInput(raw: string): string {
  const d = normalizeDigits(raw)
  const parts = [d.slice(1, 4), d.slice(4, 7), d.slice(7, 9), d.slice(9, 11)].filter(Boolean)
  return `+7${parts.length ? ' ' + parts.join(' ') : ''}`
}

export const isCompletePhone = (raw: string) => normalizeDigits(raw).length === 11

export const toE164 = (raw: string) => '+' + normalizeDigits(raw)
