import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Proxy API calls to the app service
      '/api': {
        target: process.env.SERVER_HTTPS || process.env.SERVER_HTTP || process.env.services__server__https__0 || process.env.services__server__http__0,
        changeOrigin: true
      },
      '/connect': {
        target: process.env.IDENTITY_HTTPS || process.env.IDENTITY_HTTP || process.env.services__emma_identity__https__0 || process.env.services__emma_identity__http__0,
        changeOrigin: true
      },
      '/api/keys': {
        target: process.env.IDENTITY_HTTPS || process.env.IDENTITY_HTTP || process.env.services__emma_identity__https__0 || process.env.services__emma_identity__http__0,
        changeOrigin: true
      }
    }
  }
})
