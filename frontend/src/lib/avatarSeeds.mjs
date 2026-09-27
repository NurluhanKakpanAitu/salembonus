/**
 * Дайын аватарлардың тізімі. Билд скрипті (scripts/gen-avatars.mjs) мен қосымшаның
 * өзі бір тізімді қолдануы үшін бөлек файлда тұр.
 */
export const AVATAR_SEEDS = [
  'aigerim', 'aidana', 'alibek', 'alua', 'arman', 'asel',
  'aybek', 'azamat', 'bekzat', 'dana', 'daulet', 'dinara',
  'erlan', 'gulnaz', 'kamila', 'madi', 'nurlan', 'saltanat',
  'sanzhar', 'symbat', 'timur', 'zhanel', 'zhanibek', 'zarina',
]

const BACKGROUNDS = ['dbeafe', 'dcfce7', 'fef3c7', 'fae8ff', 'ffe4e6', 'e0e7ff']

/** Тұрақты фон түсі: бір тұқым әрқашан бір түсті алады. */
export function avatarBackground(seed) {
  let h = 0
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) | 0
  return BACKGROUNDS[Math.abs(h) % BACKGROUNDS.length]
}
