import type { Config } from 'tailwindcss';

const config: Config = {
  darkMode: 'class',
  content: [
    './src/pages/**/*.{js,ts,jsx,tsx,mdx}',
    './src/components/**/*.{js,ts,jsx,tsx,mdx}',
    './src/app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      colors: {
        background: '#09090B',
        card: {
          DEFAULT: '#18181B',
          hover: '#27272A',
        },
        border: '#3F3F46',
        primary: {
          DEFAULT: '#60A5FA',
          hover: '#3B82F6',
          foreground: '#FFFFFF',
        },
        muted: {
          DEFAULT: '#27272A',
          foreground: '#A1A1AA',
        },
        accent: {
          DEFAULT: '#27272A',
          foreground: '#FAFAFA',
        },
        destructive: {
          DEFAULT: '#EF4444',
          foreground: '#FAFAFA',
        },
        success: '#22C55E',
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
      animation: {
        'fade-in': 'fadeIn 0.3s ease-in-out',
        'slide-up': 'slideUp 0.3s ease-out',
        'pulse-slow': 'pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite',
      },
      keyframes: {
        fadeIn: {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        slideUp: {
          '0%': { transform: 'translateY(10px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
      },
    },
  },
  plugins: [],
};

export default config;
