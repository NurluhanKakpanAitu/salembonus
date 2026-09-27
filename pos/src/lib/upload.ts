import { api } from './api'

const MAX_SIDE = 1600

/**
 * Суретті кішірейтіп (ұзын жағы 1600px), WebP-ке айналдырады: телефон фотосы 5–10 МБ,
 * ал каталогқа 200–400 КБ жеткілікті. Браузер WebP кодтай алмаса — JPEG.
 */
async function compress(file: File): Promise<Blob> {
  const bitmap = await createImageBitmap(file)
  const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * scale)
  canvas.height = Math.round(bitmap.height * scale)
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close()
  const toBlob = (type: string) => new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, type, 0.85))
  const webp = await toBlob('image/webp')
  if (webp && webp.type === 'image/webp') return webp
  return (await toBlob('image/jpeg'))!
}

/**
 * Файлды R2-ге тікелей жүктейді: сервер тек қолтаңбалы сілтеме береді (түрі мен көлемі бекітілген).
 * Қайтарады — жария сілтеме, оны тауарға не брендке сақтаймыз.
 */
export async function uploadImage(file: File, kind: 'product' | 'brand'): Promise<string> {
  const blob = await compress(file)
  const { uploadUrl, publicUrl } = await api<{ uploadUrl: string; publicUrl: string }>('/pos/v1/uploads', {
    method: 'POST',
    body: JSON.stringify({ kind, contentType: blob.type, size: blob.size }),
  })
  const res = await fetch(uploadUrl, { method: 'PUT', headers: { 'Content-Type': blob.type }, body: blob })
  if (!res.ok) throw new Error(`Upload failed: ${res.status}`)
  return publicUrl
}
