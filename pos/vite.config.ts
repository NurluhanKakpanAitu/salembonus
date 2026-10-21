import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

/**
 * admin.salemtech.kz (SalemTech әкімшілігі) — сол жинақтағы екінші бет (admin.html). Продакшнда nginx домен бойынша соны береді;
 * локалда http://admin.localhost:5174 — осы плагин HTML сұрауларын admin.html-ге бұрады.
 */
function adminHost(): Plugin {
  return {
    name: 'admin-host',
    configureServer(server) {
      server.middlewares.use((req, _res, next) => {
        const html = req.headers.accept?.includes('text/html')
        if (req.headers.host?.startsWith('admin.') && html && req.url && !req.url.startsWith('/api')) req.url = '/admin.html'
        next()
      })
    },
  }
}

// SalemPos: API мен фронт бір доменде (продакшнда pos.salemtech.kz/api), сондықтан
// локалда да /api прокси арқылы жүреді — refresh cookie солай ғана жұмыс істейді.
export default defineConfig({
  plugins: [react(), tailwindcss(), adminHost()],
  build: {
    rollupOptions: { input: { main: 'index.html', admin: 'admin.html' } },
  },
  server: {
    port: 5174,
    strictPort: true,
    proxy: {
      '/api': { target: 'http://localhost:5113', changeOrigin: true },
    },
  },
})
