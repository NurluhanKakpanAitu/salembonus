/**
 * Қазақстан нөмірінің маскасы: +7 777 123 45 67 (ТЗ «Авторизация» §3.4).
 *
 * Өрісте «+7» әрқашан тұрады, пайдаланушы оның артына тереді. Көп адам нөмірді әдетпен
 * 8-ден (8 700 …) немесе толық 7-ден (7 700 …) бастап тереді не қояды — оларды түсінеміз:
 * - жергілікті бөлік 8-ден басталса — бұл магистраль префиксі, алып тастаймын
 *   (оператор кодтары 7-ден басталады, 8-мен басталатын жергілікті нөмір жоқ);
 * - жергілікті бөлік 11 таңбаға жетіп, 7-ден басталса — алдыңғы 7 ел коды.
 */
export function formatPhoneInput(raw: string): string {
  let local = raw.replace(/\D/g, '')
  if (raw.trimStart().startsWith('+7')) local = local.slice(1)
  if (local.startsWith('8')) local = local.slice(1)
  if (local.length === 11 && local.startsWith('7')) local = local.slice(1)
  local = local.slice(0, 10)

  const parts = [local.slice(0, 3), local.slice(3, 6), local.slice(6, 8), local.slice(8, 10)].filter(Boolean)
  return ['+7', ...parts].join(' ') + (parts.length === 0 ? ' ' : '')
}

export const phoneDigits = (value: string) => value.replace(/\D/g, '')

/**
 * Тізімдегі телефон іздеуі — сервердегі PhoneSearch-пен бірдей ереже: нөмірдің соңы «0774», ортасы «707 170», толық «+7 (707) …»
 * не «8 707 …» (8 — ел коды) — бәрі табылады. Тек сан мен «+ ( ) - .» белгілерінен тұратын, кемінде 3 саны бар сұраныс телефон
 * деп саналады: әріп аралас мәтіндегі сандар («Иван 7») телефонмен салыстырылмайды.
 */
export function matchesPhone(phone: string | null | undefined, query: string): boolean {
  const q = query.trim()
  if (!phone || !/^[\d\s+().-]+$/.test(q)) return false
  const digits = phoneDigits(q)
  if (digits.length < 3) return false
  const stored = '+' + phoneDigits(phone)
  return stored.includes(digits) || (digits.startsWith('8') && !q.startsWith('+') && stored.startsWith('+7' + digits.slice(1)))
}

/** Толық нөмір: 7 + 10 цифр, оператор коды 7-ден басталады. */
export const isCompletePhone = (value: string) => /^77\d{9}$/.test(phoneDigits(value))
