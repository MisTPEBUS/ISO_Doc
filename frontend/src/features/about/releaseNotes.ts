export interface ReleaseNoteItem {
  text: string
  /** 補充說明，例如選項範例。 */
  detail?: string
  /** 需要保留換行與縮排的內容，例如資料夾結構。 */
  preformatted?: string
}

export interface ReleaseNote {
  version: string
  items: ReadonlyArray<ReleaseNoteItem>
  /** 版本附註，例如主機搬遷或部署注意事項。 */
  notes?: ReadonlyArray<string>
}

export const SYSTEM_NAME = 'ISO文件管理系統'

// 新版本加在最前面；第一筆即為目前版本。
export const RELEASE_NOTES: ReadonlyArray<ReleaseNote> = [
  {
    version: '2.2',
    items: [
      { text: '資料庫修改成GCP - postgres' },
      { text: 'storage-data存放位置修改為 google cloud buckets' },
      {
        text: 'GCP FOLDER規則',
        preformatted: [
          'documents/iso/',
          '  {companyId}/',
          '    {isoCategoryName}/',
          '      {documentNo}/',
          '        main/',
          '          v{version}/',
          '            {fileId}.{extension}',
          '        att/',
          '          {attachmentNo}/',
          '            v{version}/',
          '              {fileId}.{extension}',
        ].join('\n'),
      },
    ],
  },
  {
    version: '2.1',
    items: [
      { text: '新增ISO文件管理欄位「品質系統」，選項ISO9001、ISO39001、ISO4001、ESG。' },
      { text: '修正「主文」名稱都改為「ISO管理程序」。' },
      { text: '修正「附件」名稱都改為「表單及附件」。' },
      {
        text: '部門以「部」為主，新增時必填。選項使用集團最大公因數。',
        detail: '以大都會為例：總經理室、業務部、財務部、總務部、人資部、機務部、調度站、資訊中心、勞安室',
      },
      { text: '人員申請時也以部為主。' },
      { text: '權限原則預設全開，各部皆可檢視、查詢公司的ISO文件表單(已生效的)。' },
      { text: '生效日期、更新日期(上傳附件、iso文件、修改內容)。' },
      { text: '前台檢視一樣加入「品質系統」的查詢選項或全部。' },
      { text: '頁數欄位拿掉，改為「品質系統」。' },
      {
        text: '新增程序時，必要欄位：公司別、部門別、文件編號、文件名稱、版本號(自己打，不能和舊版重覆)、生效日期。',
      },
      { text: 'ISO程序文件浮水印' },
    ],
  },
  {
    version: '2.0',
    items: [
      { text: '新環境開發環境架設' },
      { text: '後端 .net 10' },
      { text: '前端 vite+React' },
      { text: 'DB-postgres' },
    ],
  },
  {
    version: '1.9',
    items: [
      { text: '修正文管人員備份資料時只可備份自己的公司別，除非開啟全部資料管理設定' },
      { text: '調整權限設定為文管人員無法設定「是否管理全部資料」功能，需由系統管理人員開啟' },
      { text: '修正部份版面對齊' },
      { text: '修正人員設定中是、否的顯示顏色，設定為「是」標為紅色' },
      { text: '系統標題加入版號' },
    ],
  },
  {
    version: '1.8',
    items: [
      { text: '修改使用者維護可選取公司別' },
      { text: '取消BIG5檔案備份' },
      { text: '修改關於頁面的排版問題' },
    ],
  },
  {
    version: '1.7',
    items: [
      { text: '公司資料加入英文名稱' },
      { text: '主檔的浮水印以各公司英文名稱顯示' },
    ],
    notes: ['107年6月27日移至首都10.8.252.212'],
  },
  {
    version: '1.6',
    items: [
      { text: '修正文件發佈日期格式驗證錯誤的問題' },
      { text: '下載主文檔的pdf檔加入浮水印，並儲存下載記錄' },
      { text: '主文資料建立時，主文檔案為必要上傳，不可空白，且格式需為PDF檔。' },
      { text: '附件資料建立時，附件檔案為必要上傳，不可空白，格式可為pdf,png,jpg,xls,xlsx,odt,ods...。' },
    ],
  },
  {
    version: '1.5',
    items: [{ text: '修正xls檔ods檔odt檔無法透過paperclip上傳的問題' }],
    notes: ['此次修正為直接於Bitbucket編輯，故apple的source需git pull'],
  },
  {
    version: '1.4',
    items: [
      { text: '關聯資料表的依賴處理bug' },
      { text: '附件依附件編號排序(由小到大)' },
    ],
  },
  {
    version: '1.3',
    items: [
      { text: '修改附件狀態在USER端沒顯示的問題。' },
      { text: '加入USER欄位中Email和是否通知Notify欄位。' },
      { text: '加入email通知7天內將到生效的文件或附件的email。' },
      { text: '修改open office無法上傳的問題。' },
      { text: '加入版本資訊頁。' },
    ],
  },
]
