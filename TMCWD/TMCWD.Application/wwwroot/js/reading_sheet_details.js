// ---------------------------------------------------------
// Reading Sheet Details — search, filter, export, print,
// and posting actions.
//
// Works in two contexts:
//   1. Standalone page  (ReadingSheetDetails.cshtml)  — element IDs without "rsd" prefix
//   2. Embedded drawer  (Billing/Index.cshtml)        — element IDs with "rsd" prefix
// ---------------------------------------------------------

(function () {
  "use strict";

  // ---------- Resolve element refs (try drawer-prefixed IDs first) ----------
  function el(drawerId, standaloneId) {
    return document.getElementById(drawerId) || document.getElementById(standaloneId);
  }

  var searchInput    = el('rsdAccountSearch',   'accountSearch');
  var usageFilter    = el('rsdUsageFilter',      'usageFilter');
  var tableBody      = el('rsdAccountsTableBody','accountsTableBody');
  var noResultsState = el('rsdNoResults',        'noResultsState');

  var completeBtn    = el('rsdCompleteBtn',   'completeBtn');
  var partialPostBtn = el('rsdPartialPostBtn','partialPostBtn');
  var closePanelBtn  = el('rsdCloseBtn',      'closePanelBtn');

  var exportPdfBtn   = el('rsdExportPdfBtn',  'exportPdfBtn');
  var exportExcelBtn = el('rsdExportExcelBtn','exportExcelBtn');

  // ---------- Filtering (search text + usage category) ----------
  function getRows() {
    if (!tableBody) return [];
    return Array.from(tableBody.querySelectorAll("tr"));
  }

  function applyFilters() {
    if (!searchInput || !usageFilter) return;
    var searchTerm = searchInput.value.trim().toLowerCase();
    var category   = usageFilter.value;
    var visibleCount = 0;

    getRows().forEach(function (row) {
      var accountName   = row.dataset.accountName   || "";
      var accountNumber = row.dataset.accountNumber || "";
      var rowCategory   = row.dataset.usageCategory || "normal";

      var matchesSearch =
        searchTerm === "" ||
        accountName.includes(searchTerm) ||
        accountNumber.includes(searchTerm);

      var matchesCategory = category === "all" || rowCategory === category;

      var isVisible = matchesSearch && matchesCategory;
      row.style.display = isVisible ? "" : "none";
      if (isVisible) visibleCount += 1;
    });

    if (noResultsState) noResultsState.classList.toggle("hidden", visibleCount !== 0);
  }

  if (searchInput)  searchInput.addEventListener("input",  applyFilters);
  if (usageFilter)  usageFilter.addEventListener("change", applyFilters);

  // ---------- Posting actions ----------
  if (completeBtn) {
    completeBtn.addEventListener("click", function () {
      var confirmed = window.confirm(
        "Mark this reading sheet as complete? This will finalize posting for all accounts on this sheet."
      );
      if (!confirmed) return;
      console.log("Complete action confirmed — hook this up to your Complete endpoint.");
    });
  }

  if (partialPostBtn) {
    partialPostBtn.addEventListener("click", function () {
      var confirmed = window.confirm(
        "Post only the selected/eligible accounts on this sheet and leave the rest pending?"
      );
      if (!confirmed) return;
      console.log("Partial Post action confirmed — hook this up to your PartialPost endpoint.");
    });
  }

  if (closePanelBtn) {
    closePanelBtn.addEventListener("click", function () {
      // In the drawer context, closeViewPanel() (from reading-sheet.js) handles this.
      // In standalone context, fall back to history.back().
      if (typeof closeViewPanel === 'function') {
        closeViewPanel();
      } else {
        history.back();
      }
    });
  }

  // ---------- Export actions ----------
  if (exportPdfBtn) {
    exportPdfBtn.addEventListener("click", function () {
      console.log("Export to PDF clicked — hook this up to your PDF export endpoint.");
    });
  }

  if (exportExcelBtn) {
    exportExcelBtn.addEventListener("click", function () {
      console.log("Export to Excel clicked — hook this up to your Excel export endpoint.");
    });
  }

  // ---------- Per-row print ----------
  if (tableBody) {
    tableBody.addEventListener("click", function (event) {
      var printBtn = event.target.closest(".row-print-btn");
      if (!printBtn) return;
      printAccountRow(printBtn.closest("tr"));
    });
  }

  function printAccountRow(row) {
    if (!row) return;
    var accountName   = row.querySelector(".acct-name")?.textContent.trim()   || "";
    var accountCode   = row.querySelector(".acct-code")?.textContent.trim()   || "";
    var accountNumber = row.querySelector(".acct-number")?.textContent.trim() || "";
    var cells  = row.querySelectorAll("td.col-num");
    var status = row.querySelector(".status-text")?.textContent.trim() || "";

    var printWindow = window.open("", "_blank", "width=480,height=640");
    if (!printWindow) return;

    printWindow.document.write(
      "<html><head><title>Account Details — " + accountNumber + "</title>" +
      "<style>" +
      "body{font-family:Arial,sans-serif;padding:24px;color:#1f2937;}" +
      "h2{margin-bottom:4px;} .meta{color:#6b7280;font-size:13px;margin-bottom:20px;}" +
      "table{width:100%;border-collapse:collapse;} td{padding:8px 0;border-bottom:1px solid #eee;}" +
      "td.label{color:#6b7280;} td.value{text-align:right;font-weight:600;}" +
      "</style></head><body>" +
      "<h2>" + accountName + "</h2>" +
      "<div class='meta'>" + accountCode + " &middot; " + accountNumber + "</div>" +
      "<table>" +
      "<tr><td class='label'>Previous Reading</td><td class='value'>" + (cells[0]?.textContent.trim() || "") + "</td></tr>" +
      "<tr><td class='label'>Present Reading</td><td class='value'>"  + (cells[1]?.textContent.trim() || "") + "</td></tr>" +
      "<tr><td class='label'>Usage</td><td class='value'>"            + (cells[2]?.textContent.trim() || "") + "</td></tr>" +
      "<tr><td class='label'>Balance</td><td class='value'>"          + (cells[3]?.textContent.trim() || "") + "</td></tr>" +
      "<tr><td class='label'>Amount</td><td class='value'>"           + (cells[4]?.textContent.trim() || "") + "</td></tr>" +
      "<tr><td class='label'>Total</td><td class='value'>"            + (cells[5]?.textContent.trim() || "") + "</td></tr>" +
      "<tr><td class='label'>Status</td><td class='value'>"           + status + "</td></tr>" +
      "</table></body></html>"
    );
    printWindow.document.close();
    printWindow.focus();
    printWindow.print();
  }

  // ---------- Init ----------
  applyFilters();
})();
