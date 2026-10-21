import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

/**
 * Бір жинақта үш бет: SalemPos (index.html), SalemTech әкімшілігі (admin.html, admin.salemtech.kz) және лендинг
 * (landing.html, salemtech.kz). Продакшнда nginx домен бойынша керек бетті береді; локалда http://admin.localhost:5174
 * және http://landing.localhost:5174 — осы плагин HTML сұрауларын сол бетке бұрады.
 */
function hostPages(): Plugin {
  return {
    name: 'host-pages',
    configureServer(server) {
      server.middlewares.use((req, _res, next) => {
        const html = req.headers.accept?.includes('text/html')
        const host = req.headers.host ?? ''
        if (html && req.url && !req.url.startsWith('/api')) {
          if (host.startsWith('admin.')) req.url = '/admin.html'
          else if (host.startsWith('landing.')) req.url = '/landing.html'
        }
        next()
      })
    },
  }
}

// SalemPos: API мен фронт бір доменде (продакшнда pos.salemtech.kz/api), сондықтан
// локалда да /api прокси арқылы жүреді — refresh cookie солай ғана жұмыс істейді.
export default defineConfig({
  plugins: [react(), tailwindcss(), hostPages()],
  build: {
    rollupOptions: { input: { main: 'index.html', admin: 'admin.html', landing: 'landing.html' } },
  },
  server: {
    port: 5174,
    strictPort: true,
    proxy: {
      '/api': { target: 'http://localhost:5113', changeOrigin: true },
    },
  },
})
