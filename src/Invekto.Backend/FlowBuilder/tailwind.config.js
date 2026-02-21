/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#F0EEFF',
          100: '#DBD8FF',
          200: '#B8B3FF',
          300: '#948EFF',
          400: '#7A74FF',
          500: '#635BFF',
          600: '#5046E5',
          700: '#3D35CC',
          800: '#2E28A3',
          900: '#1F1B7A',
        },
        navy: {
          50: '#F7F9FC',
          100: '#E3E8EE',
          200: '#C1C9D2',
          300: '#8898AA',
          400: '#6B7C93',
          500: '#425466',
          600: '#30425A',
          700: '#1A2B3D',
          800: '#0D1F30',
          900: '#0A2540',
        },
        canvas: {
          bg: '#1a1a2e',
          grid: '#252542',
        },
        node: {
          trigger: '#10b981',
          message: '#635BFF',
          logic: '#f59e0b',
          ai: '#8b5cf6',
          action: '#ef4444',
          utility: '#6b7280',
        }
      },
      fontFamily: {
        sans: ['Inter', '-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'Roboto', 'sans-serif'],
      },
      fontSize: {
        '2xs': ['0.6875rem', { lineHeight: '1rem' }],
      },
      boxShadow: {
        'soft': '0 1px 2px 0 rgb(0 0 0 / 0.04)',
        'card': '0 1px 3px 0 rgb(0 0 0 / 0.04), 0 1px 2px -1px rgb(0 0 0 / 0.04)',
        'elevated': '0 4px 12px 0 rgb(0 0 0 / 0.06)',
        'focus': '0 0 0 3px rgba(99, 91, 255, 0.15)',
      },
    },
  },
  plugins: [],
}
