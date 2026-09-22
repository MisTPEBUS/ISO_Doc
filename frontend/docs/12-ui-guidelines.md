# UI Guidelines

## 共通

- 使用 Tailwind CSS。
- 基礎元件優先使用 shadcn/ui。
- Icon 使用 Lucide React。
- 不自行引入另一套大型 UI Framework。

## Table

ISO管理程序與表單及附件 Table 使用一致的 spacing 與 typography。

ISO管理程序 Row：

- hover 可辨識。
- 文件編號可點擊展開。
- 文件名稱可點擊檢視。

表單及附件區：

- 使用主 Row 下方展開內容。
- 表單及附件 Table 可使用稍淡背景區分階層。
- 不使用獨立 Accordion Component 包覆 `<tr>`。

## Status

生效狀態建議使用 Badge。

狀態名稱由 API 定義，前端只負責 mapping 顯示。

## Link

### ISO管理程序名稱

以可識別的 Link / Button 樣式呈現。

### 表單及附件名稱

點擊後下載檔案。

應保留：

- keyboard focus
- hover state
- disabled/loading state

## Loading

Table 查詢時：

- 保留 Table Layout。
- 顯示 loading state。
- 避免整頁閃爍。

## Empty State

沒有資料時顯示：

```text
查無符合條件的 ISO 文件
```
