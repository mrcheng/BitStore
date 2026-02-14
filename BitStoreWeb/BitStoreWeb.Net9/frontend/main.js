import "bootstrap/dist/css/bootstrap.min.css";
import "bootstrap";
import $ from "jquery";
import DataTable from "datatables.net-bs5";
import "datatables.net-bs5/css/dataTables.bootstrap5.css";
import "datatables.net-responsive-bs5";
import "datatables.net-responsive-bs5/css/responsive.bootstrap5.css";
import Swal from "sweetalert2";
import "sweetalert2/dist/sweetalert2.min.css";
import "./site.css";

window.$ = $;
window.jQuery = $;
window.DataTable = DataTable;

function showToast(icon, title) {
  Swal.fire({
    toast: true,
    position: "top-end",
    icon,
    title,
    showConfirmButton: false,
    timer: 1400,
    timerProgressBar: true,
  });
}

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

async function copyTextToClipboard(value) {
  if (navigator.clipboard && window.isSecureContext) {
    await navigator.clipboard.writeText(value);
    return;
  }

  const tempInput = document.createElement("textarea");
  tempInput.value = value;
  tempInput.style.position = "fixed";
  tempInput.style.left = "-9999px";
  document.body.appendChild(tempInput);
  tempInput.focus();
  tempInput.select();
  document.execCommand("copy");
  document.body.removeChild(tempInput);
}

function initBucketsTable() {
  const tableElement = document.getElementById("buckets-table");
  if (!tableElement) {
    return;
  }

  const sourceUrl = tableElement.dataset.sourceUrl;
  const bucketUrlTemplate = tableElement.dataset.bucketUrlTemplate;
  if (!sourceUrl || !bucketUrlTemplate) {
    return;
  }

  // Avoid double-init when scripts/hot reload rerun.
  if (tableElement.dataset.initialized === "true") {
    return;
  }
  tableElement.dataset.initialized = "true";

  new DataTable(tableElement, {
    ajax: {
      url: sourceUrl,
      dataSrc: "data",
      error: () => {
        tableElement.dataset.initialized = "false";
      },
    },
    autoWidth: false,
    deferRender: true,
    responsive: true,
    pageLength: 10,
    lengthMenu: [10, 25, 50, 100],
    order: [[5, "desc"]],
    language: {
      emptyTable: "No buckets yet. Create your first bucket above.",
      search: "Search",
      searchPlaceholder: "Name, slug, or description",
    },
    columns: [
      {
        data: "name",
        responsivePriority: 1,
        render: (data, type) => {
          const value = data ?? "";
          if (type !== "display") {
            return value;
          }

          return `<span class="fw-semibold">${escapeHtml(value)}</span>`;
        },
      },
      {
        data: "description",
        responsivePriority: 5,
        defaultContent: "",
        render: (data, type) => {
          const value = data ?? "";
          if (type !== "display") {
            return value;
          }

          return value
            ? escapeHtml(value)
            : '<span class="text-muted">No description.</span>';
        },
      },
      {
        data: "slug",
        responsivePriority: 4,
        render: (data, type) => {
          const value = data ?? "";
          if (type !== "display") {
            return value;
          }

          return `<code>${escapeHtml(value)}</code>`;
        },
      },
      {
        data: "writeApiKey",
        responsivePriority: 6,
        render: (data, type) => {
          const value = data ?? "";
          if (type !== "display") {
            return value;
          }

          const escapedValue = escapeHtml(value);
          return `
            <span class="d-inline-flex align-items-center gap-1">
              <button
                type="button"
                class="copy-key-btn"
                data-key="${escapedValue}"
                aria-label="Copy write key"
                title="Copy write key">
                <svg class="copy-key-icon" viewBox="0 0 24 24" aria-hidden="true">
                  <path d="M9 9h11v11H9z"></path>
                  <path d="M4 4h11v11H4z"></path>
                </svg>
              </button>
              <code>${escapedValue}</code>
            </span>`;
        },
      },
      { data: "recordCount", responsivePriority: 3 },
      {
        data: "updatedUtc",
        responsivePriority: 2,
        render: (data, type) => {
          const value = data ?? "";
          if (type !== "display") {
            return value;
          }

          const parsed = new Date(value);
          return Number.isNaN(parsed.getTime())
            ? escapeHtml(value)
            : escapeHtml(parsed.toLocaleString());
        },
      },
      {
        data: "id",
        orderable: false,
        searchable: false,
        className: "text-end",
        responsivePriority: 1,
        render: (data, type) => {
          if (type !== "display") {
            return data;
          }

          const href = bucketUrlTemplate.replace("__id__", encodeURIComponent(String(data)));
          return `<a href="${href}" class="btn btn-primary btn-sm">Open</a>`;
        },
      },
    ],
  });

  tableElement.addEventListener("click", async (event) => {
    const button = event.target.closest(".copy-key-btn");
    if (!button) {
      return;
    }

    const key = button.dataset.key ?? "";
    if (!key) {
      return;
    }

    event.preventDefault();

    try {
      await copyTextToClipboard(key);
      button.classList.add("copied");
      button.title = "Copied";
      showToast("success", "Copied");
      window.setTimeout(() => {
        button.classList.remove("copied");
        button.title = "Copy write key";
      }, 1200);
    } catch {
      showToast("error", "Copy failed");
      button.title = "Copy failed";
      window.setTimeout(() => {
        button.title = "Copy write key";
      }, 1200);
    }
  });
}

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", initBucketsTable);
} else {
  initBucketsTable();
}
