import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: '/flow-builder/',
  server: {
    port: 3002,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: '../wwwroot/flow-builder',
    emptyOutDir: true,
    sourcemap: false
  }
});
