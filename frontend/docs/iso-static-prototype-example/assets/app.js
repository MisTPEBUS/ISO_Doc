
const page = document.body.dataset.page;
const filter = document.querySelector("document-filter-bar");
const table = document.querySelector("document-table");

function getFilterElements() {
  return {
    keyword: filter?.querySelector("#keyword"),
    category: filter?.querySelector("#category"),
    company: filter?.querySelector("#company"),
    status: filter?.querySelector("#status"),
    summary: filter?.querySelector("#filter-summary"),
    clear: filter?.querySelector("#clear-filter"),
    search: filter?.querySelector("#search-button"),
  };
}

function applyFilter() {
  if (!filter || !table) return;
  const fields = getFilterElements();
  const keyword = fields.keyword.value.trim().toLowerCase();
  const category = fields.category.value;
  const company = fields.company.value;
  const status = fields.status.value;

  const activeCount = [keyword, category, company, status].filter(Boolean).length;

  const rows = SAMPLE_DOCUMENTS.filter((doc) => {
    const matchesKeyword =
      !keyword ||
      doc.code.toLowerCase().includes(keyword) ||
      doc.title.toLowerCase().includes(keyword);

    return (
      matchesKeyword &&
      (!category || doc.category === category) &&
      (!company || doc.company === company) &&
      (!status || doc.status === status)
    );
  });

  table.data = rows;
  fields.summary.textContent = `已套用 ${activeCount} 項條件`;

  const info = document.querySelector("#pagination-info");
  if (info) {
    info.textContent = rows.length ? `1-${rows.length} / ${rows.length}` : "0 / 0";
  }

  bindRowActions();
}

function resetFilter() {
  const fields = getFilterElements();
  fields.keyword.value = "";
  fields.category.value = "";
  fields.company.value = "";
  fields.status.value = "";
  applyFilter();
}

let debounceId;

function bindFilters() {
  if (!filter) return;
  const fields = getFilterElements();

  fields.keyword.addEventListener("input", () => {
    window.clearTimeout(debounceId);
    debounceId = window.setTimeout(applyFilter, 300);
  });

  fields.category.addEventListener("change", applyFilter);
  fields.company.addEventListener("change", applyFilter);
  fields.status.addEventListener("change", applyFilter);
  fields.search.addEventListener("click", applyFilter);
  fields.clear.addEventListener("click", resetFilter);
}

function showToast(message) {
  const region = document.querySelector(".toast-region");
  if (!region) {
    window.alert(message);
    return;
  }

  const toast = document.createElement("div");
  toast.className = "toast";
  toast.textContent = message;
  region.replaceChildren(toast);

  window.setTimeout(() => {
    if (toast.isConnected) toast.remove();
  }, 4000);
}

function bindRowActions() {
  document.querySelectorAll("[data-action='download']").forEach((button) => {
    button.addEventListener("click", () => {
      const id = button.dataset.id;
      const doc = SAMPLE_DOCUMENTS.find((item) => item.id === id);
      if (!doc) return;

      button.disabled = true;
      const original = button.textContent;
      button.textContent = "下載中";

      window.setTimeout(() => {
        button.textContent = original;
        button.disabled = false;
        showToast(`已開始下載：${doc.code} ${doc.title}`);
      }, 450);
    });
  });

  document.querySelectorAll("[data-action='edit']").forEach((button) => {
    button.addEventListener("click", () => {
      const id = button.dataset.id;
      const doc = SAMPLE_DOCUMENTS.find((item) => item.id === id);
      if (!doc) return;
      showToast(`編輯元件預留：${doc.code}`);
    });
  });
}

function bindAdmin() {
  if (page !== "admin") return;
  document.querySelector("#create-document")?.addEventListener("click", () => {
    showToast("新增文件表單元件尚未接 API");
  });

  document.querySelectorAll(".nav-link[href='#']").forEach((link) => {
    link.addEventListener("click", (event) => {
      event.preventDefault();
      showToast(`${link.textContent.trim()}：靜態原型頁面尚未建立`);
    });
  });
}

window.addEventListener("DOMContentLoaded", () => {
  bindFilters();
  bindRowActions();
  bindAdmin();
  applyFilter();
});
