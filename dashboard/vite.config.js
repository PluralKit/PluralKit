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

        if (filename.startsWith("/highlight.js/es/languages/")) {
            const lang = filename.split("/").pop().split(".").shift();

            return `vendor_hljs-${lang}`;
        }

        return 'vendor-1';
        // return `vendor-${filename.charCodeAt(1) % 2}`;
      }
    }
  }
})
