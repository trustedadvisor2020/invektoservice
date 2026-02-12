/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        canvas: {
          bg: '#1a1a2e',
          grid: '#252542',
        },
        node: {
          trigger: '#10b981',
          message: '#3b82f6',
          logic: '#f59e0b',
          ai: '#8b5cf6',
          action: '#ef4444',
          utility: '#6b7280',
        }
      }
    },
  },
  plugins: [],
}
