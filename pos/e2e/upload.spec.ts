import { expect, test, type Page } from '@playwright/test'
import { posLogin, useLanguage } from './pos'

/**
 * Суреттер серверді жүктемейді: браузер оларды кішірейтіп (WebP), каталог фотосын R2-ге тікелей,
 * накладнаяны тануға сығылған күйде жібереді. Телефон фотосы (≈4000×3000, бірнеше МБ) — тест үшін браузерде салынады.
 */

const KB = 1024

/** Телефон камерасындай үлкен JPEG: шу мен мәтін — жақсы сығылмайды, нақты фотоға ұқсас көлем. */
async function phonePhoto(page: Page): Promise<Buffer> {
  const base64 = await page.evaluate(async () => {
    const c = document.createElement('canvas')
    c.width = 4000
    c.height = 3000
    const ctx = c.getContext('2d')!
    const img = ctx.createImageData(c.width, c.height)
    for (let i = 0; i < img.data.length; i += 4) {
      const v = (i / 4) % c.width
      img.data[i] = (v * 7 + Math.random() * 90) & 255
      img.data[i + 1] = (Math.random() * 255) & 255
      img.data[i + 2] = ((i / 4 / c.width) * 3) & 255
      img.data[i + 3] = 255
    }
    ctx.putImageData(img, 0, 0)
    ctx.fillStyle = '#000'
    ctx.font = '120px sans-serif'
    ctx.fillText('Накладная № 15 от 22.10.2026', 200, 400)
    const blob = await new Promise<Blob>((r) => c.toBlob((b) => r(b!), 'image/jpeg', 0.95))
    const bytes = new Uint8Array(await blob.arrayBuffer())
    let s = ''
    for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode(...bytes.subarray(i, i + 0x8000))
    return btoa(s)
  })
  return Buffer.from(base64, 'base64')
}

test.describe('image upload is compressed in the browser', () => {
  test.beforeEach(async ({ page }) => {
    await useLanguage(page, 'ru')
    await posLogin(page)
  })

  test('compressImage: catalog photo 1600px, document 2400px, small images and PDF untouched', async ({ page }) => {
    await page.goto('/products')
    const r = await page.evaluate(async () => {
      const { compressImage, DOCUMENT } = await import('/src/lib/upload.ts' as string)
      const make = async (w: number, h: number) => {
        const c = document.createElement('canvas')
        c.width = w
        c.height = h
        const ctx = c.getContext('2d')!
        for (let i = 0; i < 400; i++) { ctx.fillStyle = `hsl(${i * 37} 70% 50%)`; ctx.fillRect((i * 97) % w, (i * 53) % h, w / 9, h / 9) }
        const blob = await new Promise<Blob>((res) => c.toBlob((b) => res(b!), 'image/jpeg', 0.98))
        return new File([blob], 'IMG_0001.JPG', { type: 'image/jpeg' })
      }
      const size = async (f: File) => { const b = await createImageBitmap(f); const s = [b.width, b.height]; b.close(); return s }
      const big = await make(4000, 3000)
      const photo = await compressImage(big)
      const doc = await compressImage(big, DOCUMENT)
      const small = await make(120, 80)
      const smallOut = await compressImage(small)
      const pdf = new File(['%PDF-1.4'], 'a.pdf', { type: 'application/pdf' })
      return {
        bigSize: big.size,
        photo: { type: photo.type, name: photo.name, size: photo.size, dims: await size(photo) },
        doc: { type: doc.type, size: doc.size, dims: await size(doc) },
        small: { dims: await size(smallOut), notBigger: smallOut.size <= small.size },
        pdfSame: (await compressImage(pdf)) === pdf,
      }
    })
    expect(r.photo.type).toBe('image/webp')
    expect(r.photo.name).toBe('IMG_0001.webp')
    expect(r.photo.dims).toEqual([1600, 1200])
    expect(r.photo.size).toBeLessThan(r.bigSize)
    expect(r.doc.dims).toEqual([2400, 1800])
    expect(r.small.dims).toEqual([120, 80]) // кішкене сурет үлкейтілмейді
    expect(r.small.notBigger).toBe(true)
    expect(r.pdfSame).toBe(true)
  })

  test('product photo goes to storage compressed, the API only signs the link', async ({ page }) => {
    const original = await phonePhoto(page)
    expect(original.length).toBeGreaterThan(2_000 * KB)

    let signed: { kind: string; contentType: string; size: number } | null = null
    await page.route('**/api/pos/v1/uploads', async (route) => {
      signed = route.request().postDataJSON()
      await route.fulfill({ json: { uploadUrl: 'http://localhost:5174/__r2-test/upload', publicUrl: 'http://localhost:5174/favicon.svg', key: 'k', expiresAt: new Date().toISOString() } })
    })
    const put = new Promise<{ type: string; size: number }>((resolve) => {
      void page.route('**/__r2-test/**', async (route) => {
        resolve({ type: route.request().headers()['content-type'], size: route.request().postDataBuffer()?.length ?? 0 })
        await route.fulfill({ status: 200, body: '' })
      })
    })

    await page.goto('/products/new')
    await page.getByRole('button', { name: 'Фото и штрихкод' }).click()
    await page.locator('input[type=file][multiple]').setInputFiles({ name: 'IMG_0001.jpg', mimeType: 'image/jpeg', buffer: original })
    const uploaded = await put
    expect(uploaded.type).toBe('image/webp')
    expect(uploaded.size).toBeLessThan(original.length / 3)
    expect(signed).toMatchObject({ kind: 'product', contentType: 'image/webp', size: uploaded.size })
  })

  test('invoice photo is compressed before it is sent for recognition', async ({ page }) => {
    const original = await phonePhoto(page)
    const sent = new Promise<{ size: number; webp: boolean }>((resolve) => {
      void page.route('**/receipts/recognize', async (route) => {
        const body = route.request().postDataBuffer() ?? Buffer.alloc(0)
        resolve({ size: body.length, webp: body.includes('Content-Type: image/webp') })
        await route.fulfill({ status: 400, json: { message: 'test' } })
      })
    })

    await page.goto('/warehouse/receipts/new')
    await page.getByRole('button', { name: 'Распознать накладную' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.locator('input[type=file][accept*="application/pdf"]').setInputFiles({ name: 'invoice.jpg', mimeType: 'image/jpeg', buffer: original })
    await dialog.getByRole('button', { name: 'Распознать', exact: true }).click()
    const req = await sent
    expect(req.webp).toBe(true)
    expect(req.size).toBeLessThan(original.length / 2)
  })
})
