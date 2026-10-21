/**
 * App Store мен Google Play-ге арналған суреттер: `npm run native:assets`.
 *
 * Бір дереккөз — design/exports/logo.png (қызыл шеңбердегі ақ «S», шеңбердің сырты мөлдір).
 * Жасалатыны:
 *   android/…/mipmap-*            — иконкалар (ескі, дөңгелек, adaptive foreground) + adaptive фон түсі
 *   android/…/drawable-…/splash   — Android 11 және одан ескі нұсқалардың splash суреті (күндізгі/түнгі)
 *   ios/…/AppIcon.appiconset      — 1024×1024, мөлдірсіз (Apple талабы)
 *   ios/…/Splash.imageset         — 2732×2732, ашық және қараңғы тақырып
 *   store/                        — дүкен беті: Play иконкасы 512, feature graphic 1024×500,
 *                                   скриншоттар (App Store 6.9" 1320×2868, Google Play 1080×1920) орысша және қазақша
 *
 * Скрипт кез келген уақытта қайта іске қосылады: логотип не макеттер өзгерсе, файлдар жаңадан жазылады.
 */
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const design = resolve(root, '../design/exports')
const logoPath = resolve(design, 'logo.png')

/** Логотиптің қызылы — иконка фоны осы түспен, шеңбердің шеті көрінбейді. */
const RED = '#E31E24'
/** Сайттың фоны (index.css: --color-bg), күндізгі және түнгі. */
const BG_LIGHT = '#F4F5F8'
const BG_DARK = '#0F1116'

const out = async (path, image) => {
  const file = resolve(root, path)
  await mkdir(dirname(file), { recursive: true })
  await image.toFile(file)
}

const logo = (size) => sharp(logoPath).resize(size, size).png().toBuffer()

/** Логотип түсті фонның ортасында: `logoSize` — логотип суретінің жағы (шеңбер оның ~88%-ы). */
async function onBackground(width, height, background, logoSize, { alpha = false } = {}) {
  const img = sharp({ create: { width, height, channels: alpha ? 4 : 3, background } }).composite([
    { input: await logo(Math.round(logoSize)), gravity: 'center' },
  ])
  return alpha ? img.png() : img.flatten({ background }).png()
}

// ---------- Android ----------

const DENSITIES = { mdpi: 1, hdpi: 1.5, xhdpi: 2, xxhdpi: 3, xxxhdpi: 4 }
const res = 'android/app/src/main/res'

for (const [name, k] of Object.entries(DENSITIES)) {
  // Android 7 (API 24–25): иконка дайын пішінмен — логотиптің өз шеңбері.
  await out(`${res}/mipmap-${name}/ic_launcher.png`, sharp(await logo(48 * k)))
  await out(`${res}/mipmap-${name}/ic_launcher_round.png`, sharp(await logo(48 * k)))
  // Adaptive (API 26+): 108dp кенеп, көрінетіні ортадағы 72dp, «S» қауіпсіз 66dp шеңберге сыяды.
  await out(`${res}/mipmap-${name}/ic_launcher_foreground.png`, await onBackground(108 * k, 108 * k, { r: 0, g: 0, b: 0, alpha: 0 }, 80 * k, { alpha: true }))
}
await writeFile(
  resolve(root, `${res}/values/ic_launcher_background.xml`),
  `<?xml version="1.0" encoding="utf-8"?>\n<resources>\n    <color name="ic_launcher_background">${RED}</color>\n</resources>\n`,
)

// Ескі splash (Android 12+ жүйелік splash-ты өзі салады: иконка + values/colors.xml фоны).
const SPLASH = { mdpi: [320, 480], hdpi: [480, 800], xhdpi: [720, 1280], xxhdpi: [960, 1600], xxxhdpi: [1280, 1920] }
for (const [night, bg] of [['', BG_LIGHT], ['-night', BG_DARK]]) {
  await out(`${res}/drawable${night}/splash.png`, await onBackground(480, 320, bg, 120))
  for (const [name, [w, h]] of Object.entries(SPLASH)) {
    const logoSize = Math.min(w, h) * 0.32
    await out(`${res}/drawable-port${night}-${name}/splash.png`, await onBackground(w, h, bg, logoSize))
    await out(`${res}/drawable-land${night}-${name}/splash.png`, await onBackground(h, w, bg, logoSize))
  }
}

// ---------- iOS ----------

const assets = 'ios/App/App/Assets.xcassets'
// «S» иконканың ~49%-ы; шеңбер шеті фонмен бірдей қызыл, iOS бұрыштарды өзі дөңгелейді.
const appIcon = await onBackground(1024, 1024, RED, 960)
await out(`${assets}/AppIcon.appiconset/AppIcon-512@2x.png`, appIcon.clone())

await out(`${assets}/Splash.imageset/splash-2732x2732.png`, await onBackground(2732, 2732, BG_LIGHT, 520))
await out(`${assets}/Splash.imageset/splash-2732x2732-dark.png`, await onBackground(2732, 2732, BG_DARK, 520))
await writeFile(
  resolve(root, `${assets}/Splash.imageset/Contents.json`),
  JSON.stringify(
    {
      images: [
        { idiom: 'universal', filename: 'splash-2732x2732.png' },
        {
          idiom: 'universal',
          filename: 'splash-2732x2732-dark.png',
          appearances: [{ appearance: 'luminosity', value: 'dark' }],
        },
      ],
      info: { version: 1, author: 'xcode' },
    },
    null,
    2,
  ) + '\n',
)

// ---------- Дүкен беті ----------

await out('store/icon-1024.png', appIcon.clone())
await out('store/icon-512.png', sharp(await appIcon.clone().toBuffer()).resize(512, 512))

const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;')
const FONT = "'Segoe UI', Arial, sans-serif"

/** SVG мәтін қабаты: жолдар ортаға тураланған. */
const textLayer = (width, height, lines, { size, y, weight = 700, color = '#FFFFFF', lineHeight = 1.18 }) =>
  Buffer.from(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">` +
      lines
        .map(
          (line, i) =>
            `<text x="50%" y="${y + i * size * lineHeight}" text-anchor="middle" font-family="${FONT}" font-size="${size}" font-weight="${weight}" fill="${color}">${esc(line)}</text>`,
        )
        .join('') +
      '</svg>',
  )

// Google Play feature graphic: қызыл фон, логотип және атауы.
await out(
  'store/feature-graphic-1024x500.png',
  sharp({ create: { width: 1024, height: 500, channels: 3, background: RED } })
    .composite([
      { input: await logo(260), left: 90, top: 120 },
      { input: textLayer(1024, 500, ['SalemBonus'], { size: 92, y: 235 }), left: 200, top: 0 },
      { input: textLayer(1024, 500, ['Бонусы всех магазинов', 'в одном приложении'], { size: 40, y: 310, weight: 500 }), left: 200, top: 0 },
    ])
    .png(),
)

const SHOTS = [
  { file: '1-home.png', ru: ['Все бонусы', 'в одном месте'], kk: ['Барлық бонусыңыз', 'бір жерде'] },
  { file: '2-cards.png', ru: ['Карты всех магазинов', 'в вашем телефоне'], kk: ['Әр дүкеннің картасы', 'телефоныңызда'] },
  { file: '3-profile.png', ru: ['Общий баланс', 'с первого взгляда'], kk: ['Жалпы балансыңыз', 'бір қарағанда'] },
  { file: '4-notifications.png', ru: ['Узнавайте о бонусах', 'сразу'], kk: ['Бонус есептелгенін', 'бірден біліңіз'] },
]

/**
 * Скриншот: қызыл фон, жоғарыда екі жол мәтін, астында макет — бұрыштары дөңгеленген,
 * төменгі шеті кадрдан шығып тұрады (макет экраннан ұзын).
 */
async function storeShot(src, caption, width, height) {
  const shotWidth = Math.round(width * 0.8)
  const top = Math.round(height * 0.2)
  const visible = height - top
  const radius = Math.round(shotWidth * 0.06)
  const scaled = await sharp(src).resize(shotWidth).toBuffer()
  const meta = await sharp(scaled).metadata()
  const cropped = await sharp(scaled)
    .extract({ left: 0, top: 0, width: shotWidth, height: Math.min(visible, meta.height) })
    .toBuffer()
  const mask = Buffer.from(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${shotWidth}" height="${Math.min(visible, meta.height)}">` +
      `<rect width="100%" height="${Math.min(visible, meta.height) + radius}" rx="${radius}" ry="${radius}"/></svg>`,
  )
  const rounded = await sharp(cropped).composite([{ input: mask, blend: 'dest-in' }]).png().toBuffer()
  const size = Math.round(width * 0.068)
  return sharp({ create: { width, height, channels: 3, background: RED } })
    .composite([
      { input: textLayer(width, top, caption, { size, y: Math.round(top * 0.42) }), left: 0, top: 0 },
      { input: rounded, left: Math.round((width - shotWidth) / 2), top },
    ])
    .flatten({ background: RED })
    .png()
}

for (const [i, shot] of SHOTS.entries()) {
  const src = resolve(design, shot.file)
  for (const lang of ['ru', 'kk']) {
    const name = `${i + 1}-${shot.file.replace(/^\d-/, '')}`
    await out(`store/screenshots/app-store-6.9/${lang}/${name}`, await storeShot(src, shot[lang], 1320, 2868))
    await out(`store/screenshots/google-play/${lang}/${name}`, await storeShot(src, shot[lang], 1080, 1920))
  }
}

console.log('Иконкалар, splash және дүкен суреттері жазылды.')
