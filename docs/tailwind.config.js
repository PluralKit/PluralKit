import daisyui from "daisyui"
import typography from "@tailwindcss/typography"
import { light, dark, night, autumn, coffee, halloween, pastel } from "daisyui/src/theming/themes"

/** @type {import('tailwindcss').Config} */
export default {
  content: ["./src/**/*.{html,js,svelte,ts}"],
  plugins: [typography, daisyui],
  theme: {
    extend: {
      colors: {
        muted: "oklch(var(--muted) / <alpha-value>)",
      },
    },
  },
  daisyui: {
    themes: [
      {
        // cool light
        light: {
          ...light,
          primary: "#da9317",
          secondary: "#9e66ff",
          accent: "#22ded8",
          // DARK BUTTONS
          /* 
          "--muted": "69.38% 0.01 252.85",
          "base-200": "#ededed",
          "base-300": "#e1e3e3",
          "primary-content": "#090101",
          "neutral-content": "#f9fcff",
          "base-content": "10161e",
          "secondary-content": "fafafa"
          */
          // LIGHT BUTTONS
          "--muted": "59.37% 0.01 252.85",
          "base-200": "#ededed",
          "base-300": "#e5e7e7",
          "primary-content": "#090101",
          neutral: "#f8f8f8",
          "neutral-content": "#040507",
          "base-content": "10161e",
          "secondary-content": "#090101",
        },
        // cool dark
        dark: {
          ...dark,
          primary: "#da9317",
          secondary: "#ae81fc",
          accent: "#6df1fc",
          "--muted": "59.37% 0.0117 254.07",
          "base-100": "#22262b",
          "base-200": "#191c1f",
          "base-300": "#17191b",
          "base-content": "#ced3dc",
          neutral: "#33383e",
          "neutral-content": "#e1e2e3",
        },
        // bright dark
        acid: {
          ...night,
          primary: "#49c701",
          secondary: "#00c6cf",
          accent: "#f29838",
          "base-100": "#1a2433",
          "base-200": "#101a27",
          "base-300": "#111724",
          neutral: "#242e41",
          "--muted": "60.8% 0.05 272",
        },
        // bright light (trans rights!)
        cotton: {
          ...pastel,
          primary: "#ff69a8",
          secondary: "#63a7f9",
          accent: "#f8b939",
          neutral: "#f8f8f8",
          "base-200": "#eeecf1",
          "base-300": "#e2e1e7",
          "--muted": "59% 0.01 252.85",
          "--rounded-btn": "0.5rem",
        },
        // warm light
        autumn: {
          ...autumn,
          primary: "#e38010",
          success: "#2c7866",
          "success-content": "#eeeeee",
          error: "#97071a",
          "error-content": "#eeeeee",
          neutral: "#ebebeb",
          "neutral-content": "#141414",
          "base-100": "#fcfcfc",
          "--muted": "67.94% 0.01 39.18",
        },
        // warm dark
        coffee: {
          ...halloween,
          secondary: "#bc4b2b",
          accent: coffee.accent,
          primary: coffee.primary,
          info: "#3499c0",
          neutral: "#120f12",
          "neutral-content": "#dfe0de",
          "base-200": "#1a1a1a",
          "base-300": "#181818",
          "base-content": "#d9dbd8",
          "--muted": "57.65% 0 54",
        },
      },
    ],
  },
}
