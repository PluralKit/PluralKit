import resolve from 'path';
import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [svelte()],
  optimizeDeps: { exclude: ["svelte-navigator"] },
  build: {
    outDir: "dist",
    sourcemap: true,
  }
})
