import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Dev only: forward API calls to the ASP.NET Core backend (launchSettings http profile).
    proxy: {
      '/api': 'http://localhost:5059',
    },
  },
  build: {
    // The production build is served by the ASP.NET Core backend from wwwroot.
    outDir: '../Server/wwwroot',
    emptyOutDir: true,
  },
})
