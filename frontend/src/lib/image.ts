import { getLanguage } from './language'
import { translate } from './i18n'

const MAX_SIDE = 512
const QUALITY = 0.82

/**
 * Таңдалған суретті шаршыға қиып, кішірейтіп, JPEG data URL қайтарады.
 * Сервер фотоны base64 күйінде сақтайтындықтан өлшемді клиентте азайтамыз.
 */
export async function toSquareDataUrl(file: File): Promise<string> {
  const bitmap = await createImageBitmap(file)
  const side = Math.min(bitmap.width, bitmap.height)
  const size = Math.min(side, MAX_SIDE)

  const canvas = document.createElement('canvas')
  canvas.width = size
  canvas.height = size
  const ctx = canvas.getContext('2d')
  if (!ctx) throw new Error(translate(getLanguage(), 'profile.imageFailed'))

  ctx.drawImage(bitmap, (bitmap.width - side) / 2, (bitmap.height - side) / 2, side, side, 0, 0, size, size)
  bitmap.close()
  return canvas.toDataURL('image/jpeg', QUALITY)
}
