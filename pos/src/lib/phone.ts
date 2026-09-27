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

/** Толық нөмір: 7 + 10 цифр, оператор коды 7-ден басталады. */
export const isCompletePhone = (value: string) => /^77\d{9}$/.test(phoneDigits(value))
