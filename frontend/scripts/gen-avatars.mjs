/**
 * Дайын аватарларды алдын ала SVG файлға айналдырады: `npm run avatars`.
 *
 * DiceBear-ді браузерге тіркесек, бандл ~380 КБ өседі, ал аватарлар тек профиль
 * беттерінде керек. Сондықтан суреттер осы жерде бір рет құрастырылып,
 * public/avatars ішінде статикалық файл ретінде жатады.
 *
 * Стиль: Notionists (Zoish), лицензиясы CC0 1.0 — сілтеме талап етілмейді.
 */
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createAvatar } from '@dicebear/core'
import * as notionists from '@dicebear/notionists'
import { AVATAR_SEEDS, avatarBackground } from '../src/lib/avatarSeeds.mjs'

const outDir = resolve(dirname(fileURLToPath(import.meta.url)), '../public/avatars')
await mkdir(outDir, { recursive: true })

for (const seed of AVATAR_SEEDS) {
  const svg = createAvatar(notionists, {
    seed,
    backgroundColor: [avatarBackground(seed)],
    radius: 50,
    scale: 105,
  }).toString()
  await writeFile(resolve(outDir, `${seed}.svg`), svg, 'utf8')
}

console.log(`${AVATAR_SEEDS.length} аватар жазылды: ${outDir}`)
