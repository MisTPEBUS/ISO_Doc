/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        // ── 互動色（全系統唯一互動色相）──────────────
        primary: "#2563eb",
        "primary-hover": "#1d4ed8",
        "primary-subtle": "#eff6ff",
        "primary-on-shell": "#60a5fa", // 深色軌內的連結與作用中圖示
        "on-primary": "#ffffff",

        // ── 深色框架（頁首、側邊軌）───────────────────
        "shell-900": "#0f172a",
        "shell-800": "#1e293b",
        "shell-700": "#334155",
        "on-shell": "#ffffff",

        // ── 表面（層級由色階表達，不靠陰影）──────────
        canvas: "#f8fafc",
        surface: "#ffffff",
        "surface-header": "#f1f5f9", // 表頭、骨架列
        "surface-zebra": "#f8fafc", // 偶數列
        "surface-hover": "#eff6ff", // hover 與選取／展開列

        // ── 文字 ─────────────────────────────────────
        ink: "#0f172a",
        "ink-muted": "#64748b",
        "ink-faint": "#94a3b8", // 僅可用於 shell-900 底或 placeholder
        "ink-disabled": "#cbd5e1", // 僅「處理中」，不用於權限不足

        // ── 文件生命週期（顏色＋文字雙重編碼，不可只留色）
        "state-active": "#059669",
        "state-active-subtle": "#ecfdf5",
        "state-review": "#2563eb",
        "state-review-subtle": "#eff6ff",
        "state-expiring": "#d97706",
        "state-expiring-subtle": "#fffbeb",
        "state-obsolete": "#64748b",
        "state-obsolete-subtle": "#f1f5f9",
        "state-danger": "#dc2626", // 破壞性動作，非文件狀態
        "state-danger-subtle": "#fef2f2",

        // ── 邊框 ─────────────────────────────────────
        line: "#e2e8f0",
        "line-strong": "#cbd5e1",
      },

      fontFamily: {
        sans: ["Inter", "Noto Sans TC", "system-ui", "sans-serif"],
        mono: ["IBM Plex Mono", "ui-monospace", "monospace"],
      },

      /**
       * 語意字級 token。基準 text-base = 16px = 1rem，root font-size 不覆寫。
       * Tailwind 內建的 text-sm / text-xs / text-base 不動，語意 token 另立名稱並存。
       * 格式：[size, { lineHeight, letterSpacing?, fontWeight? }]（DESIGN.md §2.1 / §2.2）
       */
      fontSize: {
        "page-title": [
          "1.25rem",
          { lineHeight: "1.4", fontWeight: "600", letterSpacing: "-0.2px" },
        ], // 20px 頁面標題
        "section-label": ["1rem", { lineHeight: "1.5", fontWeight: "600" }], // 16px 表單分組、對話框標題
        "table-header": ["0.875rem", { lineHeight: "1.43", fontWeight: "600" }], // 14px 表頭
        cell: ["1rem", { lineHeight: "1.5", letterSpacing: "-0.1px" }], // 16px 表格內文＝基準閱讀字級
        code: ["0.9375rem", { lineHeight: "1.47", fontWeight: "500" }], // 15px 文件編號（等寬）
        revision: ["0.9375rem", { lineHeight: "1.47", fontWeight: "600" }], // 15px 版次（等寬）
        label: ["0.875rem", { lineHeight: "1.43", fontWeight: "500" }], // 14px 欄位標籤、狀態文字
        control: ["1rem", { lineHeight: "1.5", letterSpacing: "-0.1px" }], // 16px 輸入框值、按鈕、導覽項
        meta: ["0.875rem", { lineHeight: "1.43" }], // 14px 分頁資訊、輔助說明
        fine: ["0.75rem", { lineHeight: "1.33" }], // 12px 軌內分組標籤、法規註記
      },

      /**
       * 版面骨架尺寸。掛在 spacing 命名空間 → 可用於 h-* / w-* / p-* / gap-*。
       * 用 px 而非 rem：這些不隨使用者字級縮放，否則 sticky 表頭偏移量會失準（DESIGN.md §2.1）。
       */
      spacing: {
        header: "56px", // h-header：頁首，固定不隨捲動
        sidebar: "240px", // w-sidebar：側邊軌展開寬
        "sidebar-collapsed": "56px", // w-sidebar-collapsed：收合僅留圖示
        "filter-bar": "104px", // h-filter-bar：篩選列（兩排）
        "table-header": "40px", // h-table-header：表頭列高
        row: "44px", // h-row：資料列（16px 內文基準的直接成本）
        "row-sub": "36px", // h-row-sub：展開面板內的附件列
        control: "36px", // h-control：按鈕／輸入框／導覽項
        "control-sm": "32px", // h-control-sm：表格列內、工具列 ghost 按鈕
        pagination: "48px", // h-pagination

        // 主表與附件展開面板共用的欄寬（DESIGN.md §4.4 欄位表 / §4.5 面板 grid）。
        // 供 w-col-* 使用；若面板 grid-template-columns 需要原生 CSS 變數 var(--col-*)，
        // 請改在 theme.css 以 @theme 定義（v4 會產生對應 --spacing-col-* 變數，名稱不同）。
        "col-status": "100px",
        "col-code": "152px",
        "col-pages": "56px",
        "col-revision": "64px",
        "col-issued": "116px",
        "col-effective": "116px",
        "col-company": "116px",
        "col-remark": "100px",
      },

      /**
       * 圓角。刻意覆寫 Tailwind 內建的 sm / md（DESIGN.md §2.1）。
       * rounded-xs 骨架列、checkbox；rounded-sm 控制項；rounded-md 浮層。
       */
      borderRadius: {
        xs: "2px",
        sm: "4px",
        md: "6px",
      },

      /**
       * 陰影只回答一個問題：有東西疊在別的東西上面嗎？（DESIGN.md §5）
       * 按鈕、表格列、篩選列、頁首、側邊軌一律無陰影。
       */
      boxShadow: {
        "sticky-y": "0 1px 0 #e2e8f0, 0 2px 4px rgb(15 23 42 / 0.04)", // sticky 表頭捲動時
        "sticky-x": "1px 0 0 #e2e8f0, 2px 0 4px rgb(15 23 42 / 0.04)", // sticky 首欄橫向捲動時
        float: "0 4px 12px rgb(15 23 42 / 0.12)", // 下拉、日期選擇器、Dialog、Toast
      },

      // 展開／收合過渡：grid-template-rows 0fr → 1fr，150ms ease-out（DESIGN.md §4.5 / §7）。
      transitionTimingFunction: {
        "ease-out": "cubic-bezier(0, 0, 0.2, 1)",
      },
      transitionDuration: {
        150: "150ms",
      },
    },
  },

  plugins: [],
};
