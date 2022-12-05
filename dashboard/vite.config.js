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
    rollupOptions: {
      manualChunks(filename) {
        filename = filename.split("node_modules");
        if (filename.length < 2) return 'index';
        else filename = filename[1];

        // this is really big and makes the map size go over the sentry file cache limit
        if (filename.includes("highlight.js")) return 'vendor-0';

        return 'vendor-1';
        // return `vendor-${filename.charCodeAt(1) % 2}`;
      }
    }  
  }
})
