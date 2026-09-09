
const STATUS = {
  active: { label: "有效", className: "status-active" },
  review: { label: "待生效", className: "status-review" },
  expiring: { label: "即期換版", className: "status-expiring" },
  obsolete: { label: "已作廢", className: "status-obsolete" },
};

const SAMPLE_DOCUMENTS = [
  {
    id: "doc-001",
    status: "active",
    code: "HR-I-001",
    title: "人事管理程序書",
    pages: 18,
    revision: 3,
    issuedDate: "2026-08-12",
    effectiveDate: "2026-09-01",
    company: "首都客運",
    category: "程序書",
    department: "人資部",
    note: "現行版本",
    canDownload: true,
  },
  {
    id: "doc-002",
    status: "review",
    code: "QA-P-014",
    title: "客訴處理與改善作業辦法",
    pages: 12,
    revision: 5,
    issuedDate: "2026-09-03",
    effectiveDate: "2026-10-01",
    company: "大都會客運",
    category: "作業辦法",
    department: "品保部",
    note: "2026/10/01 生效",
    canDownload: true,
  },
  {
    id: "doc-003",
    status: "expiring",
    code: "OPS-S-022",
    title: "行車安全標準作業程序",
    pages: 34,
    revision: 9,
    issuedDate: "2025-10-15",
    effectiveDate: "2025-11-01",
    company: "臺北客運",
    category: "標準作業",
    department: "營運部",
    note: "換版準備中",
    canDownload: true,
  },
  {
    id: "doc-004",
    status: "active",
    code: "IT-I-008",
    title: "資訊系統帳號權限管理規範",
    pages: 9,
    revision: 2,
    issuedDate: "2026-07-22",
    effectiveDate: "2026-08-01",
    company: "首都客運",
    category: "管理規範",
    department: "資訊部",
    note: "附件 2 份",
    canDownload: true,
  },
  {
    id: "doc-005",
    status: "obsolete",
    code: "HR-F-006",
    title: "舊版教育訓練作業表單",
    pages: 4,
    revision: 1,
    issuedDate: "2024-03-10",
    effectiveDate: "2024-04-01",
    company: "大都會客運",
    category: "表單",
    department: "人資部",
    note: "已由 HR-F-009 取代",
    canDownload: false,
  },
  {
    id: "doc-006",
    status: "active",
    code: "FIN-I-011",
    title: "採購與付款作業程序",
    pages: 20,
    revision: 4,
    issuedDate: "2026-06-18",
    effectiveDate: "2026-07-01",
    company: "臺北客運",
    category: "程序書",
    department: "財務部",
    note: "",
    canDownload: true,
  },
  {
    id: "doc-007",
    status: "active",
    code: "QA-I-025",
    title: "內部稽核作業程序",
    pages: 16,
    revision: 6,
    issuedDate: "2026-05-20",
    effectiveDate: "2026-06-01",
    company: "首都客運",
    category: "程序書",
    department: "品保部",
    note: "年度稽核使用",
    canDownload: true,
  },
  {
    id: "doc-008",
    status: "review",
    code: "IT-P-013",
    title: "資訊設備報廢申請作業",
    pages: 7,
    revision: 2,
    issuedDate: "2026-09-05",
    effectiveDate: "2026-10-01",
    company: "大都會客運",
    category: "作業辦法",
    department: "資訊部",
    note: "待主管核定",
    canDownload: true,
  },
  {
    id: "doc-009",
    status: "active",
    code: "OPS-I-018",
    title: "駕駛員出勤管理規範",
    pages: 22,
    revision: 7,
    issuedDate: "2026-04-11",
    effectiveDate: "2026-05-01",
    company: "臺北客運",
    category: "管理規範",
    department: "營運部",
    note: "",
    canDownload: true,
  },
  {
    id: "doc-010",
    status: "active",
    code: "CS-I-004",
    title: "客服中心電話應對標準",
    pages: 11,
    revision: 3,
    issuedDate: "2026-03-15",
    effectiveDate: "2026-04-01",
    company: "首都客運",
    category: "標準作業",
    department: "客服部",
    note: "含附件",
    canDownload: true,
  },
];

class IsoTopbar extends HTMLElement {
  connectedCallback() {
    const mode = this.getAttribute("mode") ?? "public";
    this.innerHTML = `
      <header class="topbar">
        <div class="brand">
          <span class="brand-mark">ISO</span>
          <span>首都集團 ISO 文件管理系統</span>
        </div>
        <div class="identity">
          <span class="meta">首都客運 / 資訊部</span>
          <span class="divider">/</span>
          <strong>王小明</strong>
          ${mode === "admin"
            ? '<a class="topbar-link" href="./index.html">前台查閱</a>'
            : '<a class="topbar-link" href="./admin.html">管理</a>'}
          <a class="topbar-link" href="#">修改密碼</a>
        </div>
      </header>
    `;
  }
}

class AdminSidebar extends HTMLElement {
  connectedCallback() {
    const items = [
      ["基礎設定", [
        ["⌂", "部門維護", "#"],
        ["人", "使用者維護", "#"],
      ]],
      ["文件管理", [
        ["文", "ISO 文件維護", "./admin.html", true],
        ["權", "權限維護", "#"],
        ["備", "ISO 文件備份", "#"],
      ]],
      ["系統", [
        ["i", "關於", "#"],
      ]],
    ];

    this.innerHTML = `
      <aside class="sidebar" aria-label="管理導覽">
        ${items.map(([label, navs]) => `
          <div class="nav-group">
            <div class="nav-label">${label}</div>
            ${navs.map(([icon, text, href, active]) => `
              <a class="nav-link${active ? " active" : ""}" href="${href}">
                <span class="nav-icon">${icon}</span>
                <span>${text}</span>
              </a>
            `).join("")}
          </div>
        `).join("")}
      </aside>
    `;
  }
}

class DocumentFilterBar extends HTMLElement {
  connectedCallback() {
    this.innerHTML = `
      <div class="filter-bar">
        <div class="filter-row">
          <div class="field keyword">
            <label for="keyword">關鍵字</label>
            <input class="control" id="keyword" type="search" placeholder="文件編號或名稱" autocomplete="off" />
          </div>
          <div class="field">
            <label for="category">類別</label>
            <select class="control" id="category">
              <option value="">全部類別</option>
              <option>程序書</option>
              <option>作業辦法</option>
              <option>管理規範</option>
              <option>標準作業</option>
              <option>表單</option>
            </select>
          </div>
          <div class="field">
            <label for="company">公司別</label>
            <select class="control" id="company">
              <option value="">全部公司</option>
              <option>首都客運</option>
              <option>大都會客運</option>
              <option>臺北客運</option>
            </select>
          </div>
          <div class="field">
            <label for="status">生效狀態</label>
            <select class="control" id="status">
              <option value="">全部狀態</option>
              <option value="active">有效</option>
              <option value="review">待生效</option>
              <option value="expiring">即期換版</option>
              <option value="obsolete">已作廢</option>
            </select>
          </div>
          <button class="btn btn-primary" id="search-button" type="button">查詢</button>
        </div>

        <div class="filter-meta">
          <div id="filter-summary">已套用 0 項條件</div>
          <div class="filter-actions">
            <button class="btn btn-ghost" id="clear-filter" type="button">清除</button>
          </div>
        </div>
      </div>
    `;
  }
}

class DocumentTable extends HTMLElement {
  constructor() {
    super();
    this.rows = SAMPLE_DOCUMENTS;
  }

  set data(rows) {
    this.rows = rows;
    this.render();
  }

  connectedCallback() {
    this.render();
  }

  render() {
    const admin = this.hasAttribute("admin");

    if (!this.rows.length) {
      this.innerHTML = `
        <div class="empty">
          <strong>沒有符合條件的文件</strong>
          <span>請調整查詢條件或清除篩選。</span>
        </div>
      `;
      return;
    }

    this.innerHTML = `
      <div class="table-shell">
        <table class="document-table">
          <thead>
            <tr>
              <th style="min-width: 96px;">生效狀態</th>
              <th style="min-width: 132px;">文件編號</th>
              <th style="min-width: 280px;">名稱</th>
              <th style="min-width: 64px; text-align:right;">頁數</th>
              <th style="min-width: 64px; text-align:right;">版本</th>
              <th style="min-width: 108px; text-align:right;">發行日期</th>
              <th style="min-width: 108px; text-align:right;">生效日期</th>
              <th style="min-width: 104px;">公司別</th>
              <th style="min-width: 144px;">備註</th>
              <th style="min-width: 88px;">${admin ? "操作" : "附件"}</th>
            </tr>
          </thead>
          <tbody>
            ${this.rows.map(doc => {
              const state = STATUS[doc.status];
              const obsolete = doc.status === "obsolete";
              return `
                <tr class="${doc.status === "expiring" ? "expiring" : ""}">
                  <td class="status-cell">
                    <span class="status ${state.className}">
                      <span class="status-dot" aria-hidden="true"></span>
                      <span>${state.label}</span>
                    </span>
                  </td>
                  <td class="code">${doc.code}</td>
                  <td class="title ${obsolete ? "obsolete-title" : ""}">${doc.title}</td>
                  <td class="num" data-mobile-hide="true">${doc.pages}</td>
                  <td class="revision">V${String(doc.revision).padStart(2, "0")}</td>
                  <td class="date" data-mobile-hide="true">${doc.issuedDate}</td>
                  <td class="date mobile-date">${doc.effectiveDate}</td>
                  <td class="company" data-mobile-hide="true">${doc.company}</td>
                  <td class="note" data-mobile-hide="true">${doc.note || "－"}</td>
                  <td class="action-cell">
                    ${admin
                      ? `<button class="row-action" data-action="edit" data-id="${doc.id}" type="button">編輯</button>`
                      : doc.canDownload
                        ? `<button class="download-link" data-action="download" data-id="${doc.id}" type="button">下載</button>`
                        : `<span class="unavailable">－</span>`}
                  </td>
                </tr>
              `;
            }).join("")}
          </tbody>
        </table>
      </div>
    `;
  }
}

class PaginationBar extends HTMLElement {
  connectedCallback() {
    this.innerHTML = `
      <div class="pagination">
        <div class="pagination-info" id="pagination-info">1-10 / 10</div>
        <label class="page-size">
          <span>每頁</span>
          <select id="page-size">
            <option value="25">25</option>
            <option value="50" selected>50</option>
            <option value="100">100</option>
          </select>
          <span>筆</span>
        </label>
        <div class="page-buttons" aria-label="分頁">
          <button class="page-button" type="button" aria-label="上一頁">‹</button>
          <button class="page-button active" type="button">1</button>
          <button class="page-button" type="button" aria-label="下一頁">›</button>
        </div>
      </div>
    `;
  }
}

customElements.define("iso-topbar", IsoTopbar);
customElements.define("admin-sidebar", AdminSidebar);
customElements.define("document-filter-bar", DocumentFilterBar);
customElements.define("document-table", DocumentTable);
customElements.define("pagination-bar", PaginationBar);

window.SAMPLE_DOCUMENTS = SAMPLE_DOCUMENTS;
