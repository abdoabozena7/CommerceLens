import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const apiTarget = process.env.COMMERCELENS_API_URL ?? 'http://127.0.0.1:5080'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: { '/api': apiTarget },
  },
  preview: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: { '/api': apiTarget },
  },
})
