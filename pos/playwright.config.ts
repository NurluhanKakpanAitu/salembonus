import { defineConfig } from '@playwright/test'

/**
 * Көріну тесттері (e2e/): әр бет пен форманы шын браузерде ашып, мәтіннің кесілмегенін өлшейді.
 * Жергілікті: орнатылған Edge (Windows) не Chrome; браузер жүктелмейді. API мен Vite қосулы болса — солар қолданылады.
 * CI: E2E_CHANNEL=chrome, API Development режимінде демо деректермен (StaffSeeder, AdminSeeder) іске қосылады.
 */
const CI = !!process.env.CI

export default defineConfig({
  testDir: './e2e',
  // CI машинасы баяу, Vite беттерді алғаш ашқанда жинайды — уақыт шегі кеңірек.
  timeout: CI ? 600_000 : 120_000,
  expect: { timeout: CI ? 20_000 : 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: CI ? 1 : 0,
  // github — әр қате run бетінде annotation болып шығады (журналды ашпай-ақ көрінеді).
  reporter: CI ? [['list'], ['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: 'http://localhost:5174',
    channel: process.env.E2E_CHANNEL ?? (process.platform === 'win32' ? 'msedge' : 'chrome'),
    locale: 'ru-RU',
    timezoneId: 'Asia/Almaty',
    // Headless браузер айналдыру жолағын жасырады — пайдаланушыдағыдай көрсетеміз (ені өлшенеді, орын алады).
    launchOptions: { ignoreDefaultArgs: ['--hide-scrollbars'] },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    // Дүкендегі ең жиі ноутбук экраны — тар экранда кесілу бірінші осында көрінеді.
    { name: 'laptop', use: { viewport: { width: 1366, height: 768 } }, testIgnore: /landing|bonus-app/ },
    // Телефон, планшет (портрет) және POS-терминал (ландшафт, аласа экран): кабинет пен касса сонда да толық жұмыс істейді.
    { name: 'phone', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }, testMatch: /pos.spec|admin.spec/ },
    { name: 'tablet', use: { viewport: { width: 768, height: 1024 }, hasTouch: true }, testMatch: /pos.spec|admin.spec/ },
    { name: 'pos-terminal', use: { viewport: { width: 1024, height: 600 }, hasTouch: true }, testMatch: /pos.spec/ },
    { name: 'landing-desktop', use: { viewport: { width: 1280, height: 800 } }, testMatch: /landing/ },
    // SalemBonus клиент қосымшасы — телефон экраны.
    { name: 'bonus-app', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }, testMatch: /bonus-app/ },
    { name: 'landing-mobile', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }, testMatch: /landing/ },
  ],
  webServer: [
    {
      command: 'dotnet run --project ../backend/SalemBonus.Api --launch-profile http',
      url: 'http://localhost:5113/health',
      reuseExistingServer: !CI,
      timeout: 240_000,
      stdout: 'ignore',
    },
    { command: 'npm run dev', url: 'http://localhost:5174', reuseExistingServer: !CI, timeout: 60_000 },
    { command: 'npm run dev --prefix ../frontend', url: 'http://localhost:5173', reuseExistingServer: !CI, timeout: 60_000 },
  ],
})
