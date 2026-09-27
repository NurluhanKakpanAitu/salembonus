import { AVATAR_SEEDS as SEEDS } from './avatarSeeds.mjs'

/**
 * Дайын аватарлар DiceBear-дің Notionists стилімен жасалған (лицензиясы CC0 1.0,
 * сілтеме талап етілмейді). Суреттер билд кезінде `npm run avatars` арқылы
 * public/avatars ішіне жазылады — сондықтан браузерге генератор жүктелмейді.
 *
 * Базада суреттің өзі емес, "dicebear:notionists:seed" деген қысқа белгі сақталады.
 */
const PREFIX = 'dicebear:notionists:'

export const AVATAR_SEEDS: readonly string[] = SEEDS

export const isPresetAvatar = (value: string) => value.startsWith(PREFIX)

export const presetAvatarValue = (seed: string) => `${PREFIX}${seed}`

/** Дайын аватардың файлы. */
export const presetAvatarUri = (seed: string) => `/avatars/${seed}.svg`

/** avatarUrl не фото (data URL), не дайын аватардың белгісі — екеуін де <img src> үшін дайындайды. */
export const avatarSrc = (avatarUrl: string) =>
  isPresetAvatar(avatarUrl) ? presetAvatarUri(avatarUrl.slice(PREFIX.length)) : avatarUrl
