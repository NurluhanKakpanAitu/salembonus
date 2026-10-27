import { api } from './api'

/** Каталог суреті (тауар, топ, бренд): экранда 1600px жеткілікті, файл 150–400 КБ. */
export const PHOTO = { maxSide: 1600, quality: 0.85 }
/** Құжат фотосы (чек, накладная): ұсақ мәтін оқылуы үшін үлкенірек және сапалырақ, бірақ телефонның 5–10 МБ-ынан әлдеқайда аз. */
export const DOCUMENT = { maxSide: 2400, quality: 0.9 }

const COMPRESSIBLE = ['image/jpeg', 'image/png', 'image/webp']

/**
 * Суретті браузердің өзінде кішірейтіп, WebP-ке айналдырады (WebP кодтай алмаса — JPEG). Сервер файлды көрмейді де,
 * өңдемейді де — жүктеме пайдаланушының құрылғысында. Нәтиже түпнұсқадан үлкен шықса (кішкене сурет) не браузер
 * суретті аша алмаса — түпнұсқа қайтады. PDF пен басқа түрлер өзгермейді.
 */
export async function compressImage(file: File, { maxSide, quality } = PHOTO): Promise<File> {
  if (!COMPRESSIBLE.includes(file.type)) return file
  let bitmap: ImageBitmap
  try {
    // Телефон фотосының бұрылысы EXIF-те — суретті сол бойынша тік қоямыз.
    bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
  } catch {
    return file
  }
  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * scale)
  canvas.height = Math.round(bitmap.height * scale)
  const ctx = canvas.getContext('2d')
  if (!ctx) { bitmap.close(); return file }
  ctx.imageSmoothingQuality = 'high'
  ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close()
  const toBlob = (type: string) => new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, type, quality))
  let blob = await toBlob('image/webp')
  if (!blob || blob.type !== 'image/webp') blob = await toBlob('image/jpeg')
  if (!blob || (scale === 1 && blob.size >= file.size)) return file
  const ext = blob.type === 'image/webp' ? 'webp' : 'jpg'
  return new File([blob], `${file.name.replace(/\.[^.]+$/, '') || 'image'}.${ext}`, { type: blob.type })
}

/**
 * Файлды R2-ге тікелей жүктейді: сервер тек қолтаңбалы сілтеме береді (түрі мен көлемі бекітілген).
 * Қайтарады — жария сілтеме, оны тауарға не брендке сақтаймыз.
 */
export async function uploadImage(file: File, kind: 'product' | 'brand' | 'category' | 'finance'): Promise<string> {
  // Қаржы вложениесі — чек не шот фотосы (сомасы оқылуы керек) не PDF (өзгермейді, ТЗ «Финансы» §7).
  const blob = await compressImage(file, kind === 'finance' ? DOCUMENT : PHOTO)
  const { uploadUrl, publicUrl } = await api<{ uploadUrl: string; publicUrl: string }>('/pos/v1/uploads', {
    method: 'POST',
    body: JSON.stringify({ kind, contentType: blob.type, size: blob.size }),
  })
  const res = await fetch(uploadUrl, { method: 'PUT', headers: { 'Content-Type': blob.type }, body: blob })
  if (!res.ok) throw new Error(`Upload failed: ${res.status}`)
  return publicUrl
}
