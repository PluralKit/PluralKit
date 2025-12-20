import { sveltekit } from '@sveltejs/kit/vite';
import { mdsvex } from 'mdsvex';
import { defineConfig } from 'vite';

import { execSync } from "node:child_process"
const hash = execSync("git rev-parse --short HEAD").toString().trim()

export default defineConfig({
  plugins: [sveltekit(), mdsvex({ extension: ".md" })],
  server: {
    fs: {
        allow: ["."]
    }
  },
  define: {
    __COMMIT_HASH__: JSON.stringify("_" + hash),
  },
})