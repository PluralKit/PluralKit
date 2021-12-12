import resolve from 'path';
import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [svelte()],
  optimizeDeps: { exclude: ["svelte-navigator"] },
  server: {
    https: true
  },
  build: {
    rollupOptions: {
      input: {
        main: 'index.html',
        404: '404.html'
      },
    },
    outDir: "docs"
  }
})
