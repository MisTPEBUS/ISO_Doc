# Dark Mode 與 Windows 7 配色相容性規格

> 狀態：設計與相容性規格，尚未實作  
> 適用範圍：`frontend/` 的主題色、狀態色、邊框、遮罩與陰影  
> 不影響：API contract、資料庫 schema、角色與 Domain 規則

## 1. 目標

本文件定義兩種顯示模式：

- `dark`：維持目前介面的配色與視覺層級，不在導入主題功能時順便重新設計。
- `light`：保留相同的藍色品牌與狀態語意，但只使用 Windows 7 上最後一代 Chromium 瀏覽器能解析的固定色值與效果。

本文件所稱「目前 dark 樣式」是目前產品的「深色頁首／側邊欄 + 淺色工作區」，不是全畫面黑底。導入 dark mode 時須原樣保留這個外觀；若未來要改成全暗色工作區，需另開設計規格，不在本文件中自行推導。

本文件只定義主題 token 與相容性邊界，不定義切換按鈕的位置、使用者偏好保存方式或後端欄位。

## 2. 現況與相容性邊界

### 2.1 配色來源

實際配色以 `src/styles/theme.css` 的 `@theme` 為唯一來源。`tailwind.config.js` 目前仍保留一份舊色票，其中 `state-active`、`state-expiring`、`state-danger` 等值與 `theme.css` 不同；後續實作不得從兩處混用色值。

### 2.2 Windows 7 的實際目標

Windows 7 本身不解析 CSS，是否能顯示取決於瀏覽器引擎。相容性基準訂為：

| 環境 | 本文件定位 |
| --- | --- |
| 現代 Chrome / Edge / Firefox | 完整支援目標 |
| Windows 7 + Edge 109 或 Chrome 109 | light 色彩與效果的 legacy 驗收基準 |
| Internet Explorer 11 | 不支援；現有 React、Vite、Tailwind 技術棧不可只靠換色保證執行 |

Edge 109 是最後支援 Windows 7 的 Edge 版本；它低於 Tailwind CSS v4 官方要求的 Chrome 111 等級。現有 `Tailwind CSS 4.3.3` 與 `Vite 8.2.2` 的預設瀏覽器目標都不能直接宣稱完整支援 Windows 7。

因此需區分兩種成果：

1. **配色相容**：本文件的 light token 不使用 Edge 109 無法解析的色彩語法。
2. **整站相容**：除配色外，JavaScript bundle、Tailwind 產物、HTML 元件與互動行為也須在 Edge 109 驗證。未完成第 8 節的 legacy build 決策與實機測試前，不得把「配色可解析」寫成「整站支援 Windows 7」。

## 3. 色彩語法規則

light/legacy 路徑只可使用下列格式：

- 不透明色：`#RRGGBB`。
- 半透明色：傳統逗號格式 `rgba(r, g, b, a)`。
- 漸層若非必要不使用；必要時每個 stop 都必須是明確 HEX 或傳統 `rgba()`。
- 邊框、hover、focus、disabled 與狀態底色都使用預先定義的 token，不在瀏覽器端計算。

light/legacy 路徑禁止：

- `oklch()`、`oklab()`、`lab()`、`lch()`、Display-P3。
- `color-mix()`、relative color syntax、`light-dark()`。
- 空白分隔或斜線 alpha，例如 `rgb(15 23 42 / 0.12)`。
- Tailwind 顏色透明度修飾，例如 `bg-shell-900/50`；應改用明確的 overlay token。
- 用 `filter: brightness(...)` 產生 hover 色；應改用明確的 hover token。
- `backdrop-filter`、混色模式或透明漸層作為資訊辨識的必要條件。

## 4. 主題色票

### 4.1 核心 token

既有 token 的 dark 欄完全沿用目前 `theme.css`；light 欄是 legacy-safe 的明確 sRGB 色值。表內新增的 `state-danger-hover` 是用來取代目前 `brightness-95` 的相容性 token，不代表既有樣式已經存在這個變數。

| Token | dark（目前樣式） | light（Win7 legacy） | 用途 |
| --- | --- | --- | --- |
| `--color-primary` | `#2563EB` | `#2563EB` | 主要操作、連結、focus |
| `--color-primary-hover` | `#1D4ED8` | `#1D4ED8` | 主要操作 hover |
| `--color-primary-subtle` | `#EFF6FF` | `#EFF6FF` | 選取列、資訊底色 |
| `--color-primary-on-shell` | `#60A5FA` | `#1D4ED8` | shell 上的作用中項目 |
| `--color-on-primary` | `#FFFFFF` | `#FFFFFF` | primary 按鈕文字 |
| `--color-shell-900` | `#0F172A` | `#FFFFFF` | 頁首、側邊欄主底色 |
| `--color-shell-800` | `#1E293B` | `#F8FAFC` | shell hover |
| `--color-shell-700` | `#334155` | `#EFF6FF` | shell active |
| `--color-on-shell` | `#FFFFFF` | `#0F172A` | shell 主要文字 |
| `--color-canvas` | `#F8FAFC` | `#F8FAFC` | 頁面底色 |
| `--color-surface` | `#FFFFFF` | `#FFFFFF` | 卡片、表格、Dialog |
| `--color-surface-header` | `#F1F5F9` | `#F1F5F9` | 表頭、disabled 背景 |
| `--color-surface-zebra` | `#F8FAFC` | `#F8FAFC` | 偶數列 |
| `--color-surface-hover` | `#EFF6FF` | `#EFF6FF` | hover、展開列 |
| `--color-ink` | `#0F172A` | `#0F172A` | 主要文字 |
| `--color-ink-muted` | `#64748B` | `#64748B` | 次要文字 |
| `--color-ink-faint` | `#94A3B8` | `#64748B` | placeholder、極次要文字 |
| `--color-ink-disabled` | `#CBD5E1` | `#94A3B8` | disabled 控制項 |
| `--color-line` | `#E2E8F0` | `#E2E8F0` | 一般分隔線 |
| `--color-line-strong` | `#CBD5E1` | `#CBD5E1` | 表格外框、強分隔線 |
| `--color-control-border` | `#8492A6` | `#8492A6` | Input、Select、Textarea 邊框 |

light 模式的 `ink-faint` 刻意比目前樣式深，避免 Windows 7 舊螢幕或未啟用字型平滑時 placeholder 幾乎消失。disabled 狀態仍須同時以 `disabled` 屬性、游標與互動行為表達，不可只靠淡色。

### 4.2 狀態 token

狀態必須以「文字／圖示 + 顏色」雙重表達，顏色不可成為唯一資訊來源。

| Token | dark（目前樣式） | light（Win7 legacy） | 語意 |
| --- | --- | --- | --- |
| `--color-state-active` | `#047857` | `#047857` | 正常、生效、成功 |
| `--color-state-active-subtle` | `#ECFDF5` | `#ECFDF5` | 成功底色 |
| `--color-state-review` | `#2563EB` | `#2563EB` | 資訊、處理中 |
| `--color-state-review-subtle` | `#EFF6FF` | `#EFF6FF` | 資訊底色 |
| `--color-state-expiring` | `#92400E` | `#92400E` | 即將到期、警告 |
| `--color-state-expiring-subtle` | `#FFFBEB` | `#FFFBEB` | 警告底色 |
| `--color-state-obsolete` | `#64748B` | `#475569` | 作廢、非現行 |
| `--color-state-obsolete-subtle` | `#F1F5F9` | `#F1F5F9` | 作廢底色 |
| `--color-state-danger` | `#B91C1C` | `#B91C1C` | 錯誤、破壞性操作 |
| `--color-state-danger-hover` | `#991B1B` | `#991B1B` | 破壞性操作 hover |
| `--color-state-danger-subtle` | `#FEF2F2` | `#FEF2F2` | 錯誤底色 |

light 模式將 obsolete 文字改為 `#475569`，因為目前的 `#64748B` 搭配 `#F1F5F9` 約為 `4.34:1`，不足以穩定通過一般小字的 `4.5:1` 目標。其他主要文字配對須至少維持 WCAG AA `4.5:1`；控制項邊框與 focus 指示須至少維持 `3:1`。

## 5. 效果 token 與降級方式

所有效果都有實色邊界；陰影消失時仍須看得出元件範圍。

| Token／情境 | legacy-safe 值或規則 |
| --- | --- |
| `--color-overlay` | `rgba(15, 23, 42, 0.50)` |
| `--shadow-sticky-y` | `0 1px 0 #E2E8F0, 0 2px 4px rgba(15, 23, 42, 0.04)` |
| `--shadow-sticky-x` | `1px 0 0 #E2E8F0, 2px 0 4px rgba(15, 23, 42, 0.04)` |
| `--shadow-float` | `0 4px 12px rgba(15, 23, 42, 0.12)` |
| Dialog | `border: 1px solid var(--color-line-strong)` 必須存在，陰影只作增強 |
| Modal backdrop | 使用 `--color-overlay`，不使用 `bg-shell-900/50` 或 `backdrop-filter` |
| hover | 直接切換到明確的 HEX token，不用 brightness、混色或透明度計算 |
| loading／disabled | 必須有文字、spinner、`disabled` 屬性或游標差異，不能只降低透明度 |

目前 `theme.css` 的三個 shadow 使用 `rgb(15 23 42 / alpha)`，以及 `Modal.tsx` 使用 `backdrop:bg-shell-900/50`。這些是實作時優先替換的已知位置。

## 6. CSS 契約

主題由根元素的 `data-theme` 決定，不使用 `light-dark()`。未設定屬性時必須維持現有 dark/current 外觀，避免上線後發生未預期的預設主題變更。

以下僅示範結構；完整 token 應以第 4、5 節為準：

```css
:root,
:root[data-theme="dark"] {
  --color-shell-900: #0f172a;
  --color-shell-800: #1e293b;
  --color-shell-700: #334155;
  --color-on-shell: #ffffff;
  --color-primary-on-shell: #60a5fa;
  --color-overlay: rgba(15, 23, 42, 0.50);
}

:root[data-theme="light"] {
  --color-shell-900: #ffffff;
  --color-shell-800: #f8fafc;
  --color-shell-700: #eff6ff;
  --color-on-shell: #0f172a;
  --color-primary-on-shell: #1d4ed8;
  --color-ink-faint: #64748b;
  --color-ink-disabled: #94a3b8;
  --color-state-obsolete: #475569;
  --color-overlay: rgba(15, 23, 42, 0.50);
}
```

元件只能引用語意 token 或其 Tailwind utility，例如 `bg-surface`、`text-ink`、`border-line`。元件內不得新增 hard-coded 色值，也不得依主題寫兩套 JSX class。

## 7. 元件驗收清單

兩種主題都要逐一檢查：

- `AppHeader`、`SidebarNav`：預設、hover、active、focus、收合狀態。
- `Button`：primary、secondary、danger、ghost、disabled、loading。
- `Input`、`Select`、`Textarea`：預設、placeholder、focus、error、disabled。
- `Badge`、`Alert`：neutral、info、success、warning、danger，且保留文字或圖示。
- `Table`：表頭、斑馬紋、hover、展開列、sticky 列／欄、loading、empty state。
- `Modal`：surface、邊框、陰影、overlay、keyboard focus。
- 登入頁、首頁、修改密碼與全部管理頁，不可出現透明底變黑、文字消失或狀態只剩顏色的情況。

## 8. 實作前必須決定的 build 路徑

### 路徑 A：只修正顏色降級

- 保留 Tailwind CSS v4 與目前 Vite 設定。
- 把本文件列出的 light token、overlay、shadow 與 hover 色改為明確 HEX／傳統 `rgba()`。
- 適合解決「部分顏色或遮罩不顯示」，但不得宣稱完整支援 Windows 7。

### 路徑 B：正式支援 Windows 7 + Edge/Chrome 109

- 依 Tailwind 官方建議評估改用 Tailwind CSS 3.4，或建立不依賴 v4 現代 CSS 特性的 legacy CSS 產物。
- 明確設定並驗證 Vite production build 的瀏覽器 target；只改 target 不會自動補齊所有 polyfill。
- 在實際 Windows 7 + Edge/Chrome 109 執行完整 smoke test。
- 此路徑涉及建置工具與套件版本調整；執行前須另行確認，不在本文件建立時直接修改相依套件。

## 9. 驗收標準

完成實作後必須符合：

1. 未設定 `data-theme` 時，畫面與目前樣式一致。
2. `data-theme="dark"` 與目前樣式一致，沒有順便重設計。
3. `data-theme="light"` 在 Windows 7 + Edge/Chrome 109 可辨識所有文字、控制項、狀態、hover、focus、Dialog 與遮罩。
4. legacy CSS 路徑不含第 3 節禁止的色彩語法；特別檢查 `oklch`、`color-mix`、斜線 alpha 與 Tailwind `/opacity` 產物。
5. 陰影或透明效果未生效時，表格、Dialog、Input 與按鈕仍有實色邊框可辨識。
6. 主要小字對背景對比至少 `4.5:1`，大型文字至少 `3:1`，非文字控制項邊界與 focus indicator 至少 `3:1`。
7. 狀態、錯誤、disabled、loading 不可只靠顏色表達。
8. 執行 `npm run build`、`npm run lint`，並以 production build 驗收，不以 Vite dev server 的結果代替。
9. 若採路徑 A，交付說明需明確寫「色彩降級」，不可寫「Windows 7 完整支援」。

## 10. 參考資料

- [Tailwind CSS — Compatibility](https://tailwindcss.com/docs/compatibility)：v4 的最低瀏覽器需求。
- [Tailwind CSS — Upgrade guide](https://tailwindcss.com/docs/upgrade-guide)：需要較舊瀏覽器時，官方建議維持 v3.4。
- [Vite — Building for Production](https://vite.dev/guide/build)：production target、polyfill 與 legacy build 說明。
- [Microsoft Edge lifecycle](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-edge)：Edge 109 是最後支援 Windows 7 的版本。
- [Chrome browser system requirements](https://support.google.com/chrome/a/answer/7100626)：Chrome 109 是最後支援 Windows 7 的版本。
