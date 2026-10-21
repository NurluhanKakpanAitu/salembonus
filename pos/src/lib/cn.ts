import { twMerge } from 'tailwind-merge'

/**
 * Tailwind кластарын біріктіру: сыртан берілген w-/h-/text-/bg- т.б. базалық класты ауыстырады (екеуі қатар тұрмайды).
 * Онсыз `w-full` пен `w-24` бірге тұрып, CSS ретімен `w-full` ұтып кетеді — көршісі сығылып, мәтіні көрінбей қалады.
 */
export function cn(...parts: (string | false | null | undefined)[]) {
  return twMerge(parts.filter(Boolean).join(' '))
}
