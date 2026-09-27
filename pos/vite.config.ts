import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// SalemPos: API мен фронт бір доменде (продакшнда salempos.kz/api), сондықтан
// локалда да /api прокси арқылы жүреді — refresh cookie солай ғана жұмыс істейді.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5174,
    strictPort: true,
    proxy: {
      '/api': { target: 'http://localhost:5113', changeOrigin: true },
    },
  },
})
