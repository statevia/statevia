import type { Config } from "tailwindcss";

/**
 * Studio の色は `app/globals.css` の CSS 変数が正本。
 * ここは同じ変数を Tailwind のユーティリティへ写す。値の追加や色の変更は CSS 変数側で行う。
 * `md` 接頭辞は、Tailwind 既定の `neutral` などのパレットを上書きしないため。
 */
export default {
  // feature-first: ユーティリティクラスは app だけでなく features / shared にもある
  content: [
    "./app/**/*.{ts,tsx}",
    "./features/**/*.{ts,tsx}",
    "./shared/**/*.{ts,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        md: {
          primary: "var(--md-sys-color-primary)",
          "on-primary": "var(--md-sys-color-on-primary)",
          "primary-container": "var(--md-sys-color-primary-container)",
          "on-primary-container": "var(--md-sys-color-on-primary-container)",
          "secondary-container": "var(--md-sys-color-secondary-container)",
          surface: "var(--md-sys-color-surface)",
          "surface-container": "var(--md-sys-color-surface-container)",
          "surface-container-high": "var(--md-sys-color-surface-container-high)",
          "on-surface": "var(--md-sys-color-on-surface)",
          "on-surface-variant": "var(--md-sys-color-on-surface-variant)",
          outline: "var(--md-sys-color-outline)",
          "outline-variant": "var(--md-sys-color-outline-variant)",
          info: "var(--md-sys-color-info)",
          "on-info": "var(--md-sys-color-on-info)",
          "info-container": "var(--md-sys-color-info-container)",
          "on-info-container": "var(--md-sys-color-on-info-container)",
          success: "var(--md-sys-color-success)",
          "on-success": "var(--md-sys-color-on-success)",
          "success-container": "var(--md-sys-color-success-container)",
          "on-success-container": "var(--md-sys-color-on-success-container)",
          warning: "var(--md-sys-color-warning)",
          "on-warning": "var(--md-sys-color-on-warning)",
          "warning-container": "var(--md-sys-color-warning-container)",
          "on-warning-container": "var(--md-sys-color-on-warning-container)",
          error: "var(--md-sys-color-error)",
          "on-error": "var(--md-sys-color-on-error)",
          "error-container": "var(--md-sys-color-error-container)",
          "on-error-container": "var(--md-sys-color-on-error-container)",
          "edge-error-accent": "var(--md-sys-color-edge-error-accent)",
          neutral: "var(--md-sys-color-neutral)",
          "on-neutral": "var(--md-sys-color-on-neutral)",
          "neutral-container": "var(--md-sys-color-neutral-container)",
          "on-neutral-container": "var(--md-sys-color-on-neutral-container)",
        },
        brand: {
          "header-bg": "var(--brand-header-bg)",
          "header-fg": "var(--brand-header-fg)",
          "header-fg-muted": "var(--brand-header-fg-muted)",
          "cta-bg": "var(--brand-cta-bg)",
          "cta-fg": "var(--brand-cta-fg)",
          "cta-border": "var(--brand-cta-border)",
          "cta-bg-hover": "var(--brand-cta-bg-hover)",
        },
        editor: {
          caret: "var(--editor-caret)",
          "caret-border": "var(--editor-caret-border)",
        },
      },
    },
  },
  plugins: [],
} satisfies Config;
