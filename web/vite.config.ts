/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Geliştirmede /api istekleri backend'e yönlendirilir (aynı origin: CORS ve HTTPS sertifikası derdi yok).
// Üretimde arayüzün origin'i API'nin Cors:AllowedOrigins listesine eklenir ve VITE_API_URL verilir.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/api': { target: process.env.API_PROXY_TARGET ?? 'http://localhost:5284', changeOrigin: true } },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
