/**
 * Reading Sheet — Page Script
 *
 * STRICT SCOPE RULES:
 *  - Function declarations and addEventListener calls only.
 *  - No hardcoded row data, no business record arrays.
 *  - HTML strings allowed only inside render functions.
 *  - readingSheetState holds transient UI/runtime values only.
 */

/* ============================================================
   CONFIG
   ============================================================ */

/** Number of rows shown per page. */
var PAGE_SIZE = 10;


/* ============================================================
   RUNTIME STATE
   ============================================================ */

var readingSheetState = {
  rows:          [],
  filteredRows:  [],
  selectedIds:   new Set(),
  currentPage:   1,
  sortColumn:    null,
  sortDirection: 'asc',
  statusFilter:  'in-progress',  // 'in-progress' | 'completed'
  manualProgress: false,
  zoneProgressOverrides: {}  // { 'ZN-01': { done: 5, total: 10 }, ... }
};

/* ============================================================
   UTILITY
   ============================================================ */


/* ============================================================
   Load data from .net controller
   ============================================================ */

async function loadDataAsync(url) {
    let returnResult = null;
    await fetch(this.url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(this.data)
    }).then(response => {
        if (!response.ok || response.status == 204)
            return null;
        return response.json();
    }).then(result => {
        returnResult = result;
    });
    readingSheetState.rows = returnResult;
    return returnResult;
}

/* End data load */

/**
 * Returns a debounced version of fn — delays invocation by
 * `delay` ms after the last call. Generic utility.
 *
 * @param {Function} fn
 * @param {number} delay
 * @returns {Function}
 */

function debounce(fn, delay) {
  var timer;
  return function() {
    var ctx  = this;
    var args = arguments;
    clearTimeout(timer);
    timer = setTimeout(function() {
      fn.apply(ctx, args);
    }, delay);
  };
}


/* ============================================================
   RENDER — TABLE
   ============================================================ */

/**
 * Build and inject table rows into #readingSheetBody.
 *
 * @param {Array<{id: number, meterReader: string, billingDate: string,
 *   zone: string, forPosting: number, status: string}>} rows
 * @param {string} [emptyMessage]
 */
function renderReadingSheetTable(rows, emptyMessage) {
  var tbody = document.getElementById('readingSheetBody');
  if (!tbody) return;

  if (rows.length === 0) {
    var msg = emptyMessage || 'No records found.';
    tbody.innerHTML =
      '<tr><td colspan="7" style="text-align:center;padding:var(--space-xl);' +
      'color:var(--color-text-faint);">' + msg + '</td></tr>';
    return;
  }

  var editIcon =
    '<svg viewBox="0 0 16 16" fill="none" aria-hidden="true">' +
      '<path d="M11.5 2.5a1.414 1.414 0 0 1 2 2L5 13H3v-2L11.5 2.5z"' +
      ' stroke="currentColor" stroke-width="1.3"' +
      ' stroke-linecap="round" stroke-linejoin="round"/>' +
    '</svg>';

  var deleteIcon =
    '<svg viewBox="0 0 16 16" fill="none" aria-hidden="true">' +
      '<path d="M2 4h12M5 4V2h6v2M6 7v5M10 7v5M3 4l1 9h8l1-9"' +
      ' stroke="currentColor" stroke-width="1.3"' +
      ' stroke-linecap="round" stroke-linejoin="round"/>' +
    '</svg>';

  var viewIcon =
    '<svg viewBox="0 0 16 16" fill="none" aria-hidden="true">' +
      '<ellipse cx="8" cy="8" rx="7" ry="4.5"' +
      ' stroke="currentColor" stroke-width="1.3"' +
      ' stroke-linecap="round" stroke-linejoin="round"/>' +
      '<circle cx="8" cy="8" r="2"' +
      ' stroke="currentColor" stroke-width="1.3"/>' +
    '</svg>';

  // Build zone stats from FULL dataset so the bar is always accurate
  var zoneStats = {};
  readingSheetState.rows.forEach(function(r) {
    if (!zoneStats[r.zone]) zoneStats[r.zone] = { total: r.totalAccounts, done: r.totalCompleted };
    //zoneStats[r.zone].total += 1;
    //if (r.status === 'Completed') zoneStats[r.zone].done += 1;
  });

  var html = rows.map(function(row) {
    var badgeClass  = row.status === 'Completed' ? 'badge--green' : 'badge--yellow';
    var isSelected  = readingSheetState.selectedIds.has(row.id);
    var rowClass    = isSelected ? ' class="is-selected"' : '';
    var chkChecked  = isSelected ? ' checked' : '';

    // Zone mini progress — computed from full dataset
    var zoneStat    = zoneStats[row.zone] || { total: 0, done: 0 };
    var isCompleted = readingSheetState.statusFilter === 'completed';

    // Check for manual progress override
    var override = readingSheetState.manualProgress && readingSheetState.zoneProgressOverrides[row.zone];
    var actualDone = override ? override.done : zoneStat.done;
    var actualTotal = override ? override.total : zoneStat.total;

    // In-Progress view: done / total (bar fills as work completes)
    // Completed view:   bar always 100% full
    var zonePct  = isCompleted ? 100 : (actualTotal > 0 ? Math.round((actualDone / actualTotal) * 100) : 0);
    var labelNum = isCompleted ? actualTotal : actualDone;
    var barLabel = labelNum + '/' + actualTotal;

    // Determine color class based on percentage
    // 1-30% = red, 31-79% = yellow, 80-100% = green
    var colorClass = '';
    if (zonePct >= 80) {
      colorClass = 'zone-mini-bar__fill--green';
    } else if (zonePct >= 31) {
      colorClass = 'zone-mini-bar__fill--yellow';
    } else if (zonePct >= 1) {
      colorClass = 'zone-mini-bar__fill--red';
    }

    var zoneCell =
      '<div class="zone-cell">' +
        '<span class="zone-cell__label">' + row.zone + '</span>' +
        '<div class="zone-mini-bar" title="' + barLabel + '">' +
          '<div class="zone-mini-bar__track">' +
            '<div class="zone-mini-bar__fill ' + colorClass + '" style="width:' + zonePct + '%"></div>' +
          '</div>' +
          '<span class="zone-mini-bar__text">' + barLabel + '</span>' +
        '</div>' +
      '</div>';

    return (
      '<tr data-id="' + row.id + '"' + rowClass + '>' +
        '<td class="col-check">' +
          '<input class="tbl-checkbox" type="checkbox" aria-label="Select row"' + chkChecked + ' />' +
        '</td>' +
        '<td>' + row.meterReader + '</td>' +
        '<td>' + new Date(row.billingDate).toISOString().split("T")[0] + '</td>' +
        '<td>' + zoneCell + '</td>' +
        '<td>' + row.forPosting + '</td>' +
        '<td><span class="badge ' + badgeClass + '">' + row.status + '</span></td>' +
        '<td class="col-actions">' +
          '<div class="action-btns">' +
            '<button class="action-btn action-btn--view" type="button"' +
              ' aria-label="View details" data-id="' + row.id + '">' +
              viewIcon +
            '</button>' +
            '<button class="action-btn action-btn--edit" type="button"' +
              ' aria-label="Edit row" data-id="' + row.id + '">' +
              editIcon +
            '</button>' +
            '<button class="action-btn action-btn--delete" type="button"' +
              ' aria-label="Delete row" data-id="' + row.id + '">' +
              deleteIcon +
            '</button>' +
          '</div>' +
        '</td>' +
      '</tr>'
    );
  }).join('');

  tbody.innerHTML = html;

  // Sync select-all checkbox indeterminate / checked state
  var selectAll = document.getElementById('selectAllRows');
  if (selectAll) {
    var pageIds        = rows.map(function(r) { return r.id; });
    var allChecked     = pageIds.length > 0 && pageIds.every(function(id) { return readingSheetState.selectedIds.has(id); });
    var someChecked    = pageIds.some(function(id) { return readingSheetState.selectedIds.has(id); });
    selectAll.checked       = allChecked;
    selectAll.indeterminate = !allChecked && someChecked;
  }
}


/* ============================================================
   SORT
   ============================================================ */

/**
 * Sort an array of rows by a given field and direction.
 * Returns a new sorted array — does not mutate the input.
 *
 * @param {Array}  rows
 * @param {string} field     - Row property key (e.g. 'meterReader').
 * @param {string} direction - 'asc' or 'desc'.
 * @returns {Array}
 */
function sortRows(rows, field, direction) {
  return rows.slice().sort(function(a, b) {
    var valA = field === 'forPosting' ? a[field] : String(a[field]).toLowerCase();
    var valB = field === 'forPosting' ? b[field] : String(b[field]).toLowerCase();

    if (valA < valB) return direction === 'asc' ? -1 :  1;
    if (valA > valB) return direction === 'asc' ?  1 : -1;
    return 0;
  });
}

/**
 * Update all sort icons in the thead to reflect the current
 * sort state. The active column shows an up or down arrow;
 * all others revert to the neutral double-arrow placeholder.
 *
 * @param {string|null} activeField - Currently sorted field key.
 * @param {string}      direction   - 'asc' or 'desc'.
 */
function updateSortIcons(activeField, direction) {
  var neutralIcon =
    '<svg class="sort-icon" viewBox="0 0 10 14" fill="none" aria-hidden="true">' +
      '<path d="M5 1v12M1 4l4-3 4 3M1 10l4 3 4-3"' +
      ' stroke="currentColor" stroke-width="1.3"' +
      ' stroke-linecap="round" stroke-linejoin="round"/>' +
    '</svg>';

  var ascIcon =
    '<svg class="sort-icon sort-icon--active" viewBox="0 0 10 14" fill="none" aria-hidden="true">' +
      '<path d="M5 1v12M1 4l4-3 4 3"' +
      ' stroke="currentColor" stroke-width="1.6"' +
      ' stroke-linecap="round" stroke-linejoin="round"/>' +
    '</svg>';

  var descIcon =
    '<svg class="sort-icon sort-icon--active" viewBox="0 0 10 14" fill="none" aria-hidden="true">' +
      '<path d="M5 1v12M1 10l4 3 4-3"' +
      ' stroke="currentColor" stroke-width="1.6"' +
      ' stroke-linecap="round" stroke-linejoin="round"/>' +
    '</svg>';

  document.querySelectorAll('.th-inner[data-sort]').forEach(function(inner) {
    var field      = inner.getAttribute('data-sort');
    var existingIcon = inner.querySelector('.sort-icon');
    if (!existingIcon) return;

    if (field === activeField) {
      existingIcon.outerHTML = direction === 'asc' ? ascIcon : descIcon;
      inner.setAttribute('aria-sort', direction === 'asc' ? 'ascending' : 'descending');
    } else {
      existingIcon.outerHTML = neutralIcon;
      inner.removeAttribute('aria-sort');
    }
  });
}

/**
 * Delegated click handler on the thead row.
 * Click 1: sort ascending. Click 2: sort descending. Click 3: reset.
 *
 * @param {MouseEvent} event
 */
function handleHeaderClick(event) {
  var inner = event.target.closest('.th-inner[data-sort]');
  if (!inner) return;

  var field = inner.getAttribute('data-sort');

  if (readingSheetState.sortColumn === field) {
    if (readingSheetState.sortDirection === 'asc') {
      // 2nd click — descending
      readingSheetState.sortDirection = 'desc';
    } else {
      // 3rd click — reset
      readingSheetState.sortColumn    = null;
      readingSheetState.sortDirection = 'asc';
    }
  } else {
    // New column — start ascending
    readingSheetState.sortColumn    = field;
    readingSheetState.sortDirection = 'asc';
  }

  if (readingSheetState.sortColumn === null) {
    applyAllFilters();
  } else {
    readingSheetState.filteredRows = sortRows(
      readingSheetState.filteredRows,
      readingSheetState.sortColumn,
      readingSheetState.sortDirection
    );
  }

  updateSortIcons(readingSheetState.sortColumn, readingSheetState.sortDirection);
  readingSheetState.currentPage = 1;
  applyAndRender();
}




/**
 * Return rows from readingSheetState.rows matching the search query
 * AND the current statusFilter.
 *
 * Status filter:
 *   'in-progress' → only In-Progress rows  (toggle OFF)
 *   'completed'   → only Completed rows    (toggle ON)
 *
 * Search fields: meterReader, zone, forPosting, status.
 * billingDate excluded — month-name letters cause false matches.
 *
 * @param {string} query
 * @returns {Array}
 */
function filterRows(query) {
  var term       = query.trim().toLowerCase();
  var statusTarget = readingSheetState.statusFilter === 'completed'
    ? 'Completed'
    : 'In-Progress';

  return readingSheetState.rows.filter(function(row) {
    // Status filter always applies
    if (row.status !== statusTarget) return false;

    // If no search term, status match is enough
    if (term === '') return true;

    return (
      row.meterReader.toLowerCase().indexOf(term) !== -1 ||
      row.zone.toLowerCase().indexOf(term)        !== -1 ||
      String(row.forPosting).indexOf(term)        !== -1 ||
      row.status.toLowerCase().indexOf(term)      !== -1
    );
  });
}

/**
 * Re-derive filteredRows from the current search input value,
 * statusFilter, and sort state. Centralises all filter re-application
 * so every trigger (search, toggle, bulk-delete) stays consistent.
 */
function applyAllFilters() {
  var searchInput = document.querySelector('.search-input');
  var query       = searchInput ? searchInput.value : '';

  readingSheetState.filteredRows = filterRows(query);

  if (readingSheetState.sortColumn) {
    readingSheetState.filteredRows = sortRows(
      readingSheetState.filteredRows,
      readingSheetState.sortColumn,
      readingSheetState.sortDirection
    );
  }
}

/**
 * Fires on every keystroke (debounced at 100ms).
 * Filters the table live — no submit required.
 *
 * NOTE: query is captured immediately on the event (before debounce
 * delay) to avoid stale event.target references.
 *
 * @param {string} query - Current input value, captured before debounce.
 */
function handleSearchInput(query) {
  applyAllFilters();
  readingSheetState.currentPage = 1;

  var emptyMsg = query.trim()
    ? 'No results found for "' + query.trim() + '".'
    : 'No records found.';

  applyAndRender(emptyMsg);
}

/**
 * Handle the status toggle switch.
 * OFF → statusFilter = 'in-progress' (show only In-Progress)
 * ON  → statusFilter = 'completed'   (show only Completed)
 *
 * Wired to 'change' on the .status-toggle__input checkbox.
 *
 * @param {Event} event
 */
function handleStatusToggle(event) {
  var checked = event.target.checked;
  readingSheetState.statusFilter = checked ? 'completed' : 'in-progress';

  // Update aria-checked on the input for accessibility
  event.target.setAttribute('aria-checked', checked ? 'true' : 'false');

  readingSheetState.currentPage = 1;
  applyAllFilters();
  applyAndRender();
}

/* ============================================================
   ROW ACTIONS — VIEW (DETAILS DRAWER)
   ============================================================ */

/**
 * Ensure the drawer backdrop element exists in the DOM (appended to body).
 * Creates it once on first call so it is never clipped by overflow containers.
 */
function ensureDrawerExists() {
  if (document.getElementById('rsdDrawerBackdrop')) return;

  var html =
    '<div class="rsd-drawer-backdrop" id="rsdDrawerBackdrop" aria-hidden="true" style="display:none;">' +
      '<div class="rsd-drawer" role="dialog" aria-modal="true">' +
        '<div class="rsd-topbar">' +
          '<div class="rsd-topbar-actions">' +
            '<button type="button" id="rsdCompleteBtn" class="btn btn-complete">' +
              '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>' +
              ' Complete' +
            '</button>' +
            '<button type="button" id="rsdPartialPostBtn" class="btn btn-partial">' +
              '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>' +
              ' Partial Post' +
            '</button>' +
          '</div>' +
          '<button type="button" id="rsdCloseBtn" class="icon-btn rsd-close" aria-label="Close details panel">' +
            '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>' +
          '</button>' +
        '</div>' +
        '<div class="rsd-info">' +
          '<div class="rsd-info-group">' +
            '<div class="rsd-info-item"><span class="rsd-label">Billing Date</span><span class="rsd-value" id="rsdBillingDate">—</span></div>' +
            '<div class="rsd-info-item"><span class="rsd-label">Due Date</span><span class="rsd-value" id="rsdDueDate">—</span></div>' +
            '<div class="rsd-info-item"><span class="rsd-label">Discon Date</span><span class="rsd-value" id="rsdDisconDate">—</span></div>' +
            '<div class="rsd-info-item"><span class="rsd-label">Meter Reader</span><span class="rsd-value" id="rsdMeterReader">—</span></div>' +
          '</div>' +
          '<div class="rsd-info-status"><span class="rsd-label">Posting Status</span><span class="rsd-value" id="rsdPostingStatus">— of —</span></div>' +
          '<div class="rsd-info-ref"><span class="rsd-label">Sheet Reference</span><span class="rsd-value rsd-ref-code" id="rsdSheetRef">—</span></div>' +
          '<div class="rsd-info-export">' +
            '<span class="rsd-label">Save</span>' +
            '<div class="rsd-export-icons">' +
              '<button type="button" id="rsdExportPdfBtn" class="export-icon-btn export-pdf" title="Save as PDF" aria-label="Save as PDF">' +
                '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="9" y1="13" x2="9" y2="17"/><line x1="12" y1="13" x2="12" y2="17"/><line x1="15" y1="13" x2="15" y2="17"/></svg>' +
                '<span class="export-icon-btn__label">PDF</span>' +
              '</button>' +
              '<button type="button" id="rsdExportExcelBtn" class="export-icon-btn export-excel" title="Save as Excel" aria-label="Save as Excel">' +
                '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 9h18M3 15h18M9 3v18"/><path d="M13 12l2 3M15 12l-2 3" stroke-linecap="round"/></svg>' +
                '<span class="export-icon-btn__label">Excel</span>' +
              '</button>' +
            '</div>' +
          '</div>' +
        '</div>' +
        '<div class="rsd-toolbar">' +
          '<div class="search-wrap"><input type="text" id="rsdAccountSearch" placeholder="Search accounts..." autocomplete="off"/></div>' +
          '<div class="filter-wrap">' +
            '<select id="rsdUsageFilter">' +
              '<option value="all">All</option>' +
              '<option value="normal">Normal Usage</option>' +
              '<option value="remarks">With Remarks</option>' +
              '<option value="abnormal">Abnormal Usage</option>' +
            '</select>' +
          '</div>' +
        '</div>' +
        '<div class="rsd-table-wrap">' +
          '<table id="rsdAccountsTable">' +
            '<thead><tr>' +
              '<th class="col-account">Account</th>' +
              '<th class="col-num">Prev</th>' +
              '<th class="col-num">Pres</th>' +
              '<th class="col-num">Usage</th>' +
              '<th class="col-num">Balance</th>' +
              '<th class="col-num">Amount</th>' +
              '<th class="col-num">Total</th>' +
              '<th class="col-status">Status</th>' +
              '<th class="col-action"></th>' +
            '</tr></thead>' +
            '<tbody id="rsdAccountsTableBody"></tbody>' +
          '</table>' +
          '<div id="rsdNoResults" class="no-results hidden"><p>No accounts match your search or filter.</p></div>' +
        '</div>' +
      '</div>' +
    '</div>';

  document.body.insertAdjacentHTML('beforeend', html);

  // Wire close button and backdrop click
  var closeBtn = document.getElementById('rsdCloseBtn');
  var backdrop = document.getElementById('rsdDrawerBackdrop');
  if (closeBtn) closeBtn.addEventListener('click', closeViewPanel);
  if (backdrop) {
    backdrop.addEventListener('click', function(e) {
      if (e.target === backdrop) closeViewPanel();
    });
  }

  // Wire live search and filter
  var rsdSearch = document.getElementById('rsdAccountSearch');
  var rsdFilter = document.getElementById('rsdUsageFilter');
  function applyDrawerFilters() {
    var term     = rsdSearch ? rsdSearch.value.trim().toLowerCase() : '';
    var category = rsdFilter ? rsdFilter.value : 'all';
    var rows     = document.querySelectorAll('#rsdAccountsTableBody tr');
    var visible  = 0;
    rows.forEach(function(row) {
      var name   = (row.dataset.accountName   || '').toLowerCase();
      var number = (row.dataset.accountNumber || '');
      var cat    = (row.dataset.usageCategory || 'normal');
      var show = (term === '' || name.indexOf(term) !== -1 || number.indexOf(term) !== -1) &&
                 (category === 'all' || cat === category);
      row.style.display = show ? '' : 'none';
      if (show) visible++;
    });
    var noResults = document.getElementById('rsdNoResults');
    if (noResults) noResults.classList.toggle('hidden', visible !== 0);
  }
  if (rsdSearch) rsdSearch.addEventListener('input', applyDrawerFilters);
  if (rsdFilter) rsdFilter.addEventListener('change', applyDrawerFilters);
}

/**
 * Open the details drawer and populate it with data from the row.
 *
 * @param {number} id - Row id to view.
 */
function openViewPanel(id) {
  var row = readingSheetState.rows.find(function(r) { return r.id === id; });
  if (!row) return;

  ensureDrawerExists();

  var backdrop = document.getElementById('rsdDrawerBackdrop');
  if (!backdrop) return;

  // Populate header info
  var billingDateEl = document.getElementById('rsdBillingDate');
  var meterReaderEl = document.getElementById('rsdMeterReader');
  var postingEl     = document.getElementById('rsdPostingStatus');
  var sheetRefEl    = document.getElementById('rsdSheetRef');

  if (billingDateEl) billingDateEl.textContent = row.billingDate;
  if (meterReaderEl) meterReaderEl.textContent = row.meterReader;
  if (postingEl)     postingEl.textContent      = row.forPosting + ' of ' + row.forPosting;
  if (sheetRefEl)    sheetRefEl.textContent      = 'RS-' + String(row.id).padStart(5, '0');

  // Populate sample account rows (replace with real data fetch when available)
  var tbody = document.getElementById('rsdAccountsTableBody');
  if (tbody) {
    tbody.innerHTML = renderSampleAccountRows(row);
  }

  // Reset search and filter
  var searchInput = document.getElementById('rsdAccountSearch');
  var filterSelect = document.getElementById('rsdUsageFilter');
  if (searchInput)  searchInput.value  = '';
  if (filterSelect) filterSelect.value = 'all';

  // Reset no-results state
  var noResults = document.getElementById('rsdNoResults');
  if (noResults) noResults.classList.add('hidden');

  backdrop.style.display = 'flex';
  backdrop.setAttribute('aria-hidden', 'false');
  document.body.classList.add('rsd-drawer-open');

  // Focus the close button for accessibility
  var closeBtn = document.getElementById('rsdCloseBtn');
  if (closeBtn) closeBtn.focus();
}

/**
 * Build placeholder account rows for the drawer.
 * Replace this with a real fetch to your reading sheet accounts endpoint.
 *
 * @param {Object} row - The reading sheet row.
 * @returns {string} HTML string of <tr> elements.
 */
function renderSampleAccountRows(row) {
  // Placeholder accounts — replace with data from your API/controller
  var placeholderAccounts = [
    { name: 'Maria Santos',      code: '03-050001', number: '260700001', prev: 1120, pres: 1198, usage: 78,  trend: 'up',     balance: 120.00, amount: 312.00, total: 432.00,  status: 'For Posting', category: 'normal'   },
    { name: 'Roberto Reyes',     code: '03-050002', number: '260700002', prev: 840,  pres: 895,  usage: 55,  trend: 'down',   balance: 0.00,   amount: 220.00, total: 220.00,  status: 'Posted',      category: 'normal'   },
    { name: 'Carlo Mendoza',     code: '03-050003', number: '260700003', prev: 560,  pres: 730,  usage: 170, trend: 'up',     balance: 480.00, amount: 680.00, total: 1160.00, status: 'Abnormal',    category: 'abnormal' },
    { name: 'Dante Villanueva',  code: '03-050004', number: '260700004', prev: 2200, pres: 2265, usage: 65,  trend: 'normal', balance: 0.00,   amount: 260.00, total: 260.00,  status: 'Posted',      category: 'normal'   },
    { name: 'Noel Castillo',     code: '03-050005', number: '260700005', prev: 410,  pres: 475,  usage: 65,  trend: 'up',     balance: 95.00,  amount: 260.00, total: 355.00,  status: 'For Posting', category: 'normal'   },
    { name: 'Rachel Domingo',    code: '03-050006', number: '260700006', prev: 990,  pres: 1045, usage: 55,  trend: 'down',   balance: 0.00,   amount: 220.00, total: 220.00,  status: 'Posted',      category: 'normal'   },
    { name: 'Samuel Ong',        code: '03-050007', number: '260700007', prev: 310,  pres: 500,  usage: 190, trend: 'up',     balance: 700.00, amount: 760.00, total: 1460.00, status: 'Abnormal',    category: 'abnormal' },
    { name: 'Teresa Padilla',    code: '03-050008', number: '260700008', prev: 1500, pres: 1558, usage: 58,  trend: 'normal', balance: 0.00,   amount: 232.00, total: 232.00,  status: 'Posted',      category: 'normal'   },
    { name: 'Eduardo Flores',    code: '03-050009', number: '260700009', prev: 720,  pres: 788,  usage: 68,  trend: 'up',     balance: 140.00, amount: 272.00, total: 412.00,  status: 'For Posting', category: 'remarks'  },
    { name: 'Ligaya Ramos',      code: '03-050010', number: '260700010', prev: 880,  pres: 910,  usage: 30,  trend: 'down',   balance: 0.00,   amount: 120.00, total: 120.00,  status: 'Posted',      category: 'remarks'  }
  ];

  if (placeholderAccounts.length === 0) {
    return '<tr><td colspan="9" style="text-align:center;padding:32px;color:#9aa4b2;">No accounts found for this reading sheet.</td></tr>';
  }

  return placeholderAccounts.map(function(acct) {
    var trendIcon = '';
    if (acct.trend === 'up') {
      trendIcon = '<svg class="trend-icon trend-up" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="19" x2="12" y2="5"/><polyline points="5 12 12 5 19 12"/></svg>';
    } else if (acct.trend === 'down') {
      trendIcon = '<svg class="trend-icon trend-down" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="5" x2="12" y2="19"/><polyline points="19 12 12 19 5 12"/></svg>';
    }
    var statusClass = 'status-' + acct.status.toLowerCase().replace(/ /g, '-');
    return (
      '<tr data-account-name="' + acct.name.toLowerCase() + '"' +
          ' data-account-number="' + acct.number + '"' +
          ' data-usage-category="' + acct.category + '">' +
        '<td class="col-account">' +
          '<div class="acct-name">' + acct.name + '</div>' +
          '<div class="acct-code">' + acct.code + '</div>' +
          '<div class="acct-number">' + acct.number + '</div>' +
        '</td>' +
        '<td class="col-num">' + acct.prev.toLocaleString() + '</td>' +
        '<td class="col-num">' + acct.pres.toLocaleString() + '</td>' +
        '<td class="col-num usage-cell"><span>' + acct.usage.toLocaleString() + '</span>' + trendIcon + '</td>' +
        '<td class="col-num">' + acct.balance.toFixed(2) + '</td>' +
        '<td class="col-num">' + acct.amount.toFixed(2) + '</td>' +
        '<td class="col-num">' + acct.total.toFixed(2) + '</td>' +
        '<td class="col-status"><span class="status-text ' + statusClass + '">' + acct.status + '</span></td>' +
        '<td class="col-action">' +
          '<button type="button" class="row-print-btn" title="Print account details" aria-label="Print account details">' +
            '<svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>' +
          '</button>' +
        '</td>' +
      '</tr>'
    );
  }).join('');
}

/**
 * Close the details drawer.
 */
function closeViewPanel() {
  var backdrop = document.getElementById('rsdDrawerBackdrop');
  if (!backdrop) return;
  backdrop.style.display = 'none';
  backdrop.setAttribute('aria-hidden', 'true');
  document.body.classList.remove('rsd-drawer-open');
}

/**
 * Delegated click handler for View buttons on the table body.
 *
 * @param {MouseEvent} event
 */
function handleViewClick(event) {
  var btn = event.target.closest('.action-btn--view');
  if (!btn) return;
  var id = parseInt(btn.dataset.id, 10);
  if (!id || isNaN(id)) return;
  openViewPanel(id);
}


/* ============================================================
   ROW ACTIONS — EDIT & DELETE
   ============================================================ */

/**
 * Open the edit modal and populate all fields from the row data.
 *
 * @param {number} id - Row id to edit.
 */
function openEditModal(id) {
  console.log('[ReadingSheet] openEditModal called with id:', id);
  
  var row = readingSheetState.rows.find(function(r) { return r.id === id; });
  if (!row) {
    console.error('[ReadingSheet] Row not found for id:', id);
    return;
  }

  console.log('[ReadingSheet] Found row:', row);

  var backdrop = document.getElementById('editModalBackdrop');
  if (!backdrop) {
    console.error('[ReadingSheet] editModalBackdrop element not found in DOM');
    return;
  }

  console.log('[ReadingSheet] Backdrop element found, opening modal');

  // Convert display date format "Jul 01, 2025" to input format "yyyy-MM-dd"
  var isoDate = '';
  try {
    var d = new Date(row.billingDate);
    if (!isNaN(d)) {
      var year = d.getFullYear();
      var month = String(d.getMonth() + 1).padStart(2, '0');
      var day = String(d.getDate()).padStart(2, '0');
      isoDate = year + '-' + month + '-' + day;
    }
  } catch (e) {
    console.error('Date conversion error:', e);
  }

  // Populate fields
  document.getElementById('editRowId').value        = row.id;
  document.getElementById('editMeterReader').value  = row.meterReader;
  document.getElementById('editBillingDate').value  = isoDate;
  document.getElementById('editZone').value         = row.zone;
  document.getElementById('editForPosting').value   = row.forPosting;
  document.getElementById('editStatus').value       = row.status;

  backdrop.hidden = false;
  backdrop.setAttribute('aria-hidden', 'false');

  console.log('[ReadingSheet] Modal opened successfully');

  // Focus first field
  var firstInput = document.getElementById('editMeterReader');
  if (firstInput) firstInput.focus();
}

/**
 * Close the edit modal.
 */
function closeEditModal() {
  var backdrop = document.getElementById('editModalBackdrop');
  if (!backdrop) return;
  backdrop.hidden = true;
  backdrop.setAttribute('aria-hidden', 'true');
}

/**
 * Save the edited values back into readingSheetState.rows and re-render.
 */
function handleEditSave() {
  var id = parseInt(document.getElementById('editRowId').value, 10);
  if (!id) return;

  var meterReader = document.getElementById('editMeterReader').value.trim();
  var billingDateRaw = document.getElementById('editBillingDate').value.trim();
  var zone        = document.getElementById('editZone').value.trim();
  var forPosting  = parseInt(document.getElementById('editForPosting').value, 10) || 0;
  var status      = document.getElementById('editStatus').value;

  if (!meterReader || !billingDateRaw || !zone) return;

  // Convert date from "yyyy-MM-dd" to display format "Jul 01, 2025"
  var displayDate = billingDateRaw;
  try {
    var d = new Date(billingDateRaw);
    if (!isNaN(d)) {
      displayDate = d.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' });
    }
  } catch (e) {
    console.error('Date formatting error:', e);
  }

  // Update the matching row in state
  readingSheetState.rows = readingSheetState.rows.map(function(row) {
    if (row.id !== id) return row;
    return {
      id:          row.id,
      meterReader: meterReader,
      billingDate: displayDate,
      zone:        zone,
      forPosting:  forPosting,
      status:      status
    };
  });

  closeEditModal();
  applyAllFilters();
  applyAndRender();
}

/**
 * Delegated click handler for Edit buttons on the table body.
 *
 * @param {MouseEvent} event
 */
function handleEditClick(event) {
  console.log('[ReadingSheet] handleEditClick called');
  
  // Check if the click is on an edit button or its children (SVG)
  var btn = event.target.closest('.action-btn--edit');
  if (!btn) {
    console.log('[ReadingSheet] No edit button found');
    return;
  }

  event.preventDefault();
  event.stopPropagation();

  var id = parseInt(btn.dataset.id, 10);
  console.log('[ReadingSheet] Edit button clicked, id:', id, 'dataset.id:', btn.dataset.id);
  
  if (!id || isNaN(id)) {
    console.error('[ReadingSheet] Invalid row ID for edit:', btn.dataset.id);
    return;
  }

  openEditModal(id);
}

function openDeleteModal(id) {
  var backdrop = document.getElementById('deleteModalBackdrop');
  if (!backdrop) return;

  backdrop.hidden = false;
  backdrop.setAttribute('aria-hidden', 'false');
  backdrop.dataset.pendingId = String(id);

  // Focus the cancel button for safe keyboard default
  var cancelBtn = document.getElementById('deleteModalCancel');
  if (cancelBtn) cancelBtn.focus();
}

/**
 * Close the delete confirmation modal and clear the pending id.
 */
function closeDeleteModal() {
  var backdrop = document.getElementById('deleteModalBackdrop');
  if (!backdrop) return;

  backdrop.hidden = true;
  backdrop.setAttribute('aria-hidden', 'true');
  delete backdrop.dataset.pendingId;
}

/**
 * Delegated click handler for Delete buttons on the table body.
 * Opens the confirmation modal instead of using window.confirm.
 *
 * @param {MouseEvent} event
 */
function handleDeleteClick(event) {
  var btn = event.target.closest('.action-btn--delete');
  if (!btn) return;

  var id = parseInt(btn.dataset.id, 10);
  openDeleteModal(id);
}

/**
 * Confirm deletion: remove the row, clean up, re-render.
 */
async function handleDeleteConfirm() {
  var backdrop = document.getElementById('deleteModalBackdrop');
  if (!backdrop || !backdrop.dataset.pendingId) return;

  var id = parseInt(backdrop.dataset.pendingId, 10);

  const result = await fetch('/readingsheet/DeleteReadingSheet?id=' + id, {
      method: 'DELETE',
      headers: {
          'Content-Type': 'application/json'
      }
  }).then(response => {
      if (response.ok || response.status == 204) return null;
      return response.json();
  }).then(result => {
      return result;
  });

  closeDeleteModal();

  initReadingSheetPage();

  if (result !== 'true') return false;

  readingSheetState.rows = readingSheetState.rows.filter(function(row) {
    return row.id !== id;
  });

  readingSheetState.selectedIds.delete(id);

  applyAllFilters();
  updateBulkActionsBar();

  var totalPages = Math.max(1, Math.ceil(readingSheetState.filteredRows.length / PAGE_SIZE));
  if (readingSheetState.currentPage > totalPages) {
    readingSheetState.currentPage = totalPages;
  }

  applyAndRender();
}


/* ============================================================
   SELECTION & BULK ACTIONS
   ============================================================ */

/**
 * Show/hide the bulk-actions bar and update the selected count label.
 * Called after any change to readingSheetState.selectedIds.
 */
function updateBulkActionsBar() {
  var bar   = document.getElementById('bulkActionsBar');
  var count = document.getElementById('bulkSelectedCount');
  if (!bar) return;

  var n = readingSheetState.selectedIds.size;
  if (n > 0) {
    bar.hidden        = false;
    if (count) count.textContent = n + ' row' + (n === 1 ? '' : 's') + ' selected';
  } else {
    bar.hidden        = true;
    if (count) count.textContent = '0 selected';
  }
}

/**
 * Select-all / deselect-all for the current page.
 * Reads the current page slice from filteredRows so it only
 * affects what the user can see right now.
 *
 * @param {Event} event - change event from #selectAllRows checkbox.
 */
function handleSelectAll(event) {
  var checked   = event.target.checked;
  var page      = readingSheetState.currentPage;
  var pageSlice = paginate(readingSheetState.filteredRows, page, PAGE_SIZE);

  pageSlice.forEach(function(row) {
    if (checked) {
      readingSheetState.selectedIds.add(row.id);
    } else {
      readingSheetState.selectedIds.delete(row.id);
    }
  });

  updateBulkActionsBar();

  // Re-render so row highlight + checkbox states refresh
  renderReadingSheetTable(pageSlice);
  renderPagination(readingSheetState.filteredRows.length, page, PAGE_SIZE);
}

/**
 * Toggle a single row's selection by its id.
 * Uses event delegation on #readingSheetBody.
 *
 * @param {Event} event - change event from a row checkbox.
 */
function handleRowSelect(event) {
  if (!event.target.classList.contains('tbl-checkbox')) return;

  var tr = event.target.closest('tr[data-id]');
  if (!tr) return;

  var id = parseInt(tr.dataset.id, 10);

  if (event.target.checked) {
    readingSheetState.selectedIds.add(id);
  } else {
    readingSheetState.selectedIds.delete(id);
  }

  updateBulkActionsBar();

  // Sync the select-all checkbox state for the current page
  var page      = readingSheetState.currentPage;
  var pageSlice = paginate(readingSheetState.filteredRows, page, PAGE_SIZE);
  var selectAll = document.getElementById('selectAllRows');
  if (selectAll) {
    var pageIds    = pageSlice.map(function(r) { return r.id; });
    var allChecked = pageIds.every(function(pid) { return readingSheetState.selectedIds.has(pid); });
    var someChecked = pageIds.some(function(pid) { return readingSheetState.selectedIds.has(pid); });
    selectAll.checked       = allChecked;
    selectAll.indeterminate = !allChecked && someChecked;
  }

  // Reflect is-selected class on the row without full re-render
  if (readingSheetState.selectedIds.has(id)) {
    tr.classList.add('is-selected');
  } else {
    tr.classList.remove('is-selected');
  }
}

/**
 * Open the bulk delete confirmation modal.
 * Shows the count and resets the confirm input.
 */
function openBulkDeleteModal() {
  var backdrop  = document.getElementById('bulkDeleteModalBackdrop');
  var countEl   = document.getElementById('bulkDeleteCount');
  var input     = document.getElementById('bulkDeleteConfirmInput');
  var confirmBtn = document.getElementById('bulkDeleteModalConfirm');
  if (!backdrop) return;

  if (countEl)   countEl.textContent = readingSheetState.selectedIds.size;
  if (input)     { input.value = ''; input.classList.remove('is-valid'); }
  //if (confirmBtn) confirmBtn.disabled = true;

  backdrop.hidden = false;
  backdrop.setAttribute('aria-hidden', 'false');

  if (input) input.focus();
}

/**
 * Close the bulk delete confirmation modal.
 */
function closeBulkDeleteModal() {
  var backdrop = document.getElementById('bulkDeleteModalBackdrop');
  var input    = document.getElementById('bulkDeleteConfirmInput');
  if (!backdrop) return;

  backdrop.hidden = true;
  backdrop.setAttribute('aria-hidden', 'true');
  if (input) { input.value = ''; input.classList.remove('is-valid'); }
}

/**
 * Validate the confirm input — enable the Delete All button
 * only when the user has typed exactly "delete all".
 *
 * @param {Event} event
 */
// function handleBulkDeleteInput(event) {
//   var val        = event.target.value.trim().toLowerCase();
//   var confirmBtn = document.getElementById('bulkDeleteModalConfirm');
//   var isValid    = val === 'delete all';

//   if (confirmBtn) confirmBtn.disabled = !isValid;
//   event.target.classList.toggle('is-valid', isValid);
// }

/**
 * Execute the bulk deletion after modal confirmation.
 */
async function handleBulkDeleteConfirm() {
  closeBulkDeleteModal();

  readingSheetState.rows = readingSheetState.rows.filter(function(row) {
    return !readingSheetState.selectedIds.has(row.id);
  });

    var queryParam = '';
    readingSheetState.selectedIds.forEach((value, index) => {
        if (index == 1) queryParam += 'ids=' + value;
        else queryParam += '&ids=' + value;
    });
    queryParam += '&status=3';
    var result = await fetch('/ReadingSheet/BulkDeleteReadingSheet?' + queryParam, {
        method: 'DELETE',
        headers: {
            'Content-Type': 'application/json'
        },
        body: null
    }).then(response => {
        if (!response.ok || response.status == 204) return null;
        return response.json();
    }).then(result => {
        return result;
    });

    if (result) {
        readingSheetState.selectedIds.clear();
        readingSheetState.currentPage = 1;

        applyAllFilters();
        updateBulkActionsBar();
        applyAndRender();
    }
}

/**
 * Open the bulk delete modal instead of acting directly.
 */
function handleBulkDelete() {
  openBulkDeleteModal();
}

/**
 * Clear all selections without deleting anything.
 */
function handleBulkClear() {
  readingSheetState.selectedIds.clear();
  updateBulkActionsBar();
  applyAndRender();
}


/* ============================================================
   PAGINATION
   ============================================================ */

/**
 * Slice a rows array to the given page and page size.
 * Pure function — no side effects.
 *
 * @param {Array}  rows
 * @param {number} page     - 1-based page number.
 * @param {number} pageSize
 * @returns {Array}
 */
function paginate(rows, page, pageSize) {
  var start = (page - 1) * pageSize;
  return rows.slice(start, start + pageSize);
}

/**
 * Rebuild the pagination controls to reflect the current page
 * and total row count. Updates:
 *   - #paginationSummary text
 *   - Page number buttons (inserted between Prev and Next)
 *   - Prev / Next disabled states
 *
 * @param {number} totalRows
 * @param {number} currentPage
 * @param {number} pageSize
 */
function renderPagination(totalRows, currentPage, pageSize) {
  var summary  = document.getElementById('paginationSummary');
  var controls = document.getElementById('paginationControls');
  var prevBtn  = document.getElementById('prevPageBtn');
  var nextBtn  = document.getElementById('nextPageBtn');
  if (!summary || !controls || !prevBtn || !nextBtn) return;

  var totalPages = Math.max(1, Math.ceil(totalRows / pageSize));
  var start      = totalRows === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  var end        = Math.min(currentPage * pageSize, totalRows);

  summary.textContent = 'Showing ' + start + '–' + end + ' of ' + totalRows + ' entries';

  // Remove old page number buttons (keep Prev and Next)
  controls.querySelectorAll('.pagination__page').forEach(function(btn) {
    btn.remove();
  });

  // Insert page number buttons between Prev and Next
  for (var p = 1; p <= totalPages; p++) {
    var btn = document.createElement('button');
    btn.type        = 'button';
    btn.className   = 'pagination__page' + (p === currentPage ? ' is-current' : '');
    btn.textContent = String(p);
    btn.setAttribute('aria-label', 'Page ' + p);
    if (p === currentPage) btn.setAttribute('aria-current', 'page');
    btn.dataset.page = String(p);
    controls.insertBefore(btn, nextBtn);
  }

  prevBtn.disabled = currentPage <= 1;
  nextBtn.disabled = currentPage >= totalPages;
}

/**
 * Central render coordinator — always call this instead of
 * renderReadingSheetTable() directly. Slices filteredRows to
 * the current page and refreshes pagination controls.
 *
 * @param {string} [emptyMessage]
 */
function applyAndRender(emptyMessage) {
  var page      = readingSheetState.currentPage;
  var pageSlice = paginate(readingSheetState.filteredRows, page, PAGE_SIZE);
  renderReadingSheetTable(pageSlice, emptyMessage);
  renderPagination(readingSheetState.filteredRows.length, page, PAGE_SIZE);
}




/**
 * Delegated click on #paginationControls page number buttons.
 *
 * @param {MouseEvent} event
 */
function handlePageClick(event) {
  var btn = event.target.closest('.pagination__page');
  if (!btn || btn.classList.contains('is-current')) return;

  readingSheetState.currentPage = parseInt(btn.dataset.page, 10);
  applyAndRender();
}

/**
 * Go to the previous page.
 */
function handlePrevClick() {
  if (readingSheetState.currentPage <= 1) return;
  readingSheetState.currentPage -= 1;
  applyAndRender();
}

/**
 * Go to the next page.
 */
function handleNextClick() {
  var totalPages = Math.ceil(readingSheetState.filteredRows.length / PAGE_SIZE);
  if (readingSheetState.currentPage >= totalPages) return;
  readingSheetState.currentPage += 1;
  applyAndRender();
}


/* ============================================================
   INIT
   ============================================================ */

/**
 * Initialize the Reading Sheet page.
 * Called by appshell.js loadPage() after all assets are loaded.
 */
async function initReadingSheetPage() {
  // Reset all runtime state on every init — prevents stale data
  // from a previous page load bleeding through.
  readingSheetState.rows          = [];
  readingSheetState.filteredRows  = [];
  readingSheetState.selectedIds   = new Set();
  readingSheetState.currentPage   = 1;
  readingSheetState.sortColumn    = null;
  readingSheetState.sortDirection = 'asc';
  readingSheetState.statusFilter  = 'in-progress';

  // Inject edit modal if it doesn't exist (fallback for SPA navigation)
  if (!document.getElementById('editModalBackdrop')) {
    var modalHTML = 
      '<div class="modal-backdrop" id="editModalBackdrop" hidden aria-hidden="true">' +
        '<div class="modal" role="dialog" aria-modal="true" aria-labelledby="editModalTitle" style="max-width: 500px;">' +
          '<h2 class="modal__title" id="editModalTitle">Edit Reading Sheet</h2>' +
          '<div class="modal__body" style="width: 100%; max-width: 100%; text-align: left;">' +
            '<input type="hidden" id="editRowId" />' +
            '<div style="display: flex; flex-direction: column; gap: 14px;">' +
              '<div style="display: flex; flex-direction: column; gap: 6px;">' +
                '<label for="editMeterReader" style="font-size: 13px; font-weight: 600; color: var(--color-text-dim);">Meter Reader</label>' +
                '<input type="text" id="editMeterReader" style="width: 100%; padding: 8px 12px; border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-panel-alt); color: var(--color-text); font-size: 14px; font-family: var(--font-sans);" />' +
              '</div>' +
              '<div style="display: flex; flex-direction: column; gap: 6px;">' +
                '<label for="editBillingDate" style="font-size: 13px; font-weight: 600; color: var(--color-text-dim);">Due Date</label>' +
                '<input type="date" id="editBillingDate" style="width: 100%; padding: 8px 12px; border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-panel-alt); color: var(--color-text); font-size: 14px; font-family: var(--font-sans);" />' +
              '</div>' +
              '<div style="display: flex; flex-direction: column; gap: 6px;">' +
                '<label for="editZone" style="font-size: 13px; font-weight: 600; color: var(--color-text-dim);">Zone</label>' +
                '<input type="text" id="editZone" style="width: 100%; padding: 8px 12px; border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-panel-alt); color: var(--color-text); font-size: 14px; font-family: var(--font-sans);" />' +
              '</div>' +
              '<div style="display: flex; flex-direction: column; gap: 6px;">' +
                '<label for="editForPosting" style="font-size: 13px; font-weight: 600; color: var(--color-text-dim);">For Posting</label>' +
                '<input type="number" id="editForPosting" style="width: 100%; padding: 8px 12px; border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-panel-alt); color: var(--color-text); font-size: 14px; font-family: var(--font-sans);" />' +
              '</div>' +
              '<div style="display: flex; flex-direction: column; gap: 6px;">' +
                '<label for="editStatus" style="font-size: 13px; font-weight: 600; color: var(--color-text-dim);">Status</label>' +
                '<select id="editStatus" style="width: 100%; padding: 8px 12px; border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-panel-alt); color: var(--color-text); font-size: 14px; font-family: var(--font-sans);">' +
                  '<option value="In-Progress">In Progress</option>' +
                  '<option value="Completed">Completed</option>' +
                '</select>' +
              '</div>' +
            '</div>' +
          '</div>' +
          '<div class="modal__actions">' +
            '<button class="btn btn--ghost" type="button" id="editModalCancel">Cancel</button>' +
            '<button class="btn btn--green" type="button" id="editModalSave">Save Changes</button>' +
          '</div>' +
        '</div>' +
      '</div>';
    document.body.insertAdjacentHTML('beforeend', modalHTML);
    console.log('[ReadingSheet] Edit modal injected via JavaScript');
  } 

  // if (typeof READING_SHEET_SAMPLE_DATA !== 'undefined') {
  //   readingSheetState.rows = READING_SHEET_SAMPLE_DATA.slice();
  // }

  const READING_SHEETS = await loadReadingSheets();

  if (typeof READING_SHEETS !== 'undefined') {
        readingSheetState.rows = READING_SHEETS;
  }

  applyAllFilters();
  applyAndRender();

  // Initialize zone progress input controls
  generateZoneProgressInputs();

  // Search — capture value immediately, pass string into debounced handler
  var searchInput = document.querySelector('.search-input');
  if (searchInput) {
    var debouncedSearch = debounce(handleSearchInput, 100);
    searchInput.addEventListener('input', function(event) {
      debouncedSearch(event.target.value);
    });
  }

  // Sort
  var thead = document.querySelector('#readingSheetTable thead');
  if (thead) {
    thead.addEventListener('click', handleHeaderClick);
  }

  // Pagination
  var prevBtn  = document.getElementById('prevPageBtn');
  var nextBtn  = document.getElementById('nextPageBtn');
  var controls = document.getElementById('paginationControls');

  if (prevBtn)  prevBtn.addEventListener('click', handlePrevClick);
  if (nextBtn)  nextBtn.addEventListener('click', handleNextClick);
  if (controls) controls.addEventListener('click', handlePageClick);

  // Selection & bulk actions
  var selectAll  = document.getElementById('selectAllRows');
  var tbody      = document.getElementById('readingSheetBody');
  var bulkDelete = document.getElementById('bulkDeleteBtn');
  var bulkClear  = document.getElementById('bulkClearBtn');

  console.log('[ReadingSheet] Init: tbody found?', !!tbody, '| editModalBackdrop found?', !!document.getElementById('editModalBackdrop'));

  if (selectAll)  selectAll.addEventListener('change', handleSelectAll);
  if (tbody) {
    tbody.addEventListener('change', handleRowSelect);
    // Single delegated click handler for view, edit, and delete buttons
    tbody.addEventListener('click', function(event) {
      console.log('[ReadingSheet] tbody click detected', event.target);

      // Check for view button first
      var viewBtn = event.target.closest('.action-btn--view');
      if (viewBtn) {
        console.log('[ReadingSheet] View button found in delegation');
        handleViewClick(event);
        return;
      }

      // Check for edit button
      var editBtn = event.target.closest('.action-btn--edit');
      if (editBtn) {
        console.log('[ReadingSheet] Edit button found in delegation');
        handleEditClick(event);
        return;
      }
      
      // Check for delete button
      var deleteBtn = event.target.closest('.action-btn--delete');
      if (deleteBtn) {
        console.log('[ReadingSheet] Delete button found in delegation');
        handleDeleteClick(event);
        return;
      }
    });
  }
  if (bulkDelete) bulkDelete.addEventListener('click', handleBulkDelete);
  if (bulkClear)  bulkClear.addEventListener('click', handleBulkClear);

  // Edit modal
  var editSave    = document.getElementById('editModalSave');
  var editCancel  = document.getElementById('editModalCancel');
  var editBackdrop = document.getElementById('editModalBackdrop');

  if (editSave)    editSave.addEventListener('click', handleEditSave);
  if (editCancel)  editCancel.addEventListener('click', closeEditModal);
  if (editBackdrop) {
    editBackdrop.addEventListener('click', function(event) {
      if (event.target === editBackdrop) closeEditModal();
    });
  }

  // Delete modal (single row)
  var deleteConfirm  = document.getElementById('deleteModalConfirm');
  var deleteCancel   = document.getElementById('deleteModalCancel');
  var deleteBackdrop = document.getElementById('deleteModalBackdrop');

  if (deleteConfirm)  deleteConfirm.addEventListener('click', handleDeleteConfirm);
  if (deleteCancel)   deleteCancel.addEventListener('click', closeDeleteModal);
  if (deleteBackdrop) {
    deleteBackdrop.addEventListener('click', function(event) {
      if (event.target === deleteBackdrop) closeDeleteModal();
    });
  }

  // Bulk delete modal
  var bulkDeleteConfirm  = document.getElementById('bulkDeleteModalConfirm');
  var bulkDeleteCancel   = document.getElementById('bulkDeleteModalCancel');
  var bulkDeleteBackdrop = document.getElementById('bulkDeleteModalBackdrop');
  //var bulkDeleteInput    = document.getElementById('bulkDeleteConfirmInput');

  if (bulkDeleteConfirm)  bulkDeleteConfirm.addEventListener('click', handleBulkDeleteConfirm);
  if (bulkDeleteCancel)   bulkDeleteCancel.addEventListener('click', closeBulkDeleteModal);
  //if (bulkDeleteInput)    bulkDeleteInput.addEventListener('input', handleBulkDeleteInput);
  if (bulkDeleteBackdrop) {
    bulkDeleteBackdrop.addEventListener('click', function(event) {
      if (event.target === bulkDeleteBackdrop) closeBulkDeleteModal();
    });
  }

  // Escape closes whichever modal is open
  document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
      closeViewPanel();
      closeEditModal();
      closeDeleteModal();
      closeBulkDeleteModal();
    }
  });

  // Status toggle
  var statusToggleInput = document.querySelector('.status-toggle__input');
  if (statusToggleInput) {
    statusToggleInput.checked = false;
    statusToggleInput.setAttribute('aria-checked', 'false');
    statusToggleInput.addEventListener('change', handleStatusToggle);
  }

  console.log('[ReadingSheet] Page initialized. Rows loaded:', readingSheetState.rows.length);
}

/**
 * ==========================================================
 * Load Reading Sheet Data From Database
 * ==========================================================
 */

async function loadReadingSheets() {
    return await fetch('/readingsheet/GetAllReadingSheets', {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json'
        }
    }).then(response => {
        if (!response.ok || response.status == 204) return null;
        return response.json();
    }).then(result => {
        return result;
    });
}


/* ============================================================
   Phase 9 — Create Reading Sheet callback
   Called by the inline script in Index.cshtml after Save.
   ============================================================ */

/**
 * Receive a newly created reading sheet from the modal,
 * add it to the table, and re-render.
 *
 * @param {{billingDate:string, meterReader:string, zone:string}} data
 */

async function onReadingSheetCreate(data) {
    var isValid = false;
    const form = document.getElementById('frmReadingSheet');
    const readingSheetData = {
        id: 0,
        name: '',
        billingDate: data.billingDate,
        dueDate: data.dueDate,
        disconnectionDate: data.disconnectionDate,
        billingPeriodStart: data.billingPeriodStart,
        zoneBookId: 0,
        sequenceFrom: data.seqFrom,
        sequenceTo: data.seqTo,
        assignedTo: data.meterReader
    };

    if (!form.reportValidity()) return false;

    var readingSheet = await fetch('/readingsheet/createreadingsheet?zone=' + data.zone + '&book=' + data.book, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(readingSheetData)
    }).then(response => {
        if (!response.ok || response.status == 204) return null;
        return response.json();
    }).then(result => {
        return result;
    });
}

function onReadingSheetCreated(data) {
  if (!data || !data.meterReader) return;

  /* Generate a new id one higher than the current max */
  var maxId = readingSheetState.rows.reduce(function (m, r) {
    return Math.max(m, r.id);
  }, 0);

  /* Map zone value (e.g. "zone1") to display label (e.g. "ZN-01") */
  var zoneMap = { zone1: 'ZN-01', zone2: 'ZN-02', zone3: 'ZN-03', zone4: 'ZN-04' };
  var zoneLabel = zoneMap[data.zone] || data.zone || 'ZN-01';

  /* Format billing date from yyyy-MM-dd to "Jul 01, 2025" */
  var displayDate = data.billingDate;
  try {
    var d = new Date(data.billingDate);
    if (!isNaN(d)) {
      displayDate = d.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' });
    }
  } catch (e) { /* keep raw value */ }

  var newRow = {
    id:          maxId + 1,
    meterReader: data.meterReader,
    billingDate: displayDate,
    zone:        zoneLabel,
    forPosting:  0,
    status:      'In-Progress'
  };

  readingSheetState.rows.unshift(newRow);

  /* Switch to In-Progress view so the new row is visible */
  readingSheetState.statusFilter = 'in-progress';
  readingSheetState.currentPage  = 1;

  var statusToggleInput = document.querySelector('.status-toggle__input');
  if (statusToggleInput) {
    statusToggleInput.checked = false;
    statusToggleInput.setAttribute('aria-checked', 'false');
  }

  applyAllFilters();
  applyAndRender();

  console.log('[ReadingSheet] New row added from modal:', newRow);
}

/* ============================================================
   ZONE PROGRESS INPUT CONTROLS
   ============================================================ */

/**
 * Generate zone progress input controls based on available zones
 */
function generateZoneProgressInputs() {
  var zoneInputsContainer = document.getElementById('zoneProgressInputs');
  if (!zoneInputsContainer) return;

  // Get unique zones from data
  var zones = {};
  readingSheetState.rows.forEach(function(row) {
    if (!zones[row.zone]) {
      zones[row.zone] = { total: 0, done: 0 };
    }
    zones[row.zone].total += 1;
    if (row.status === 'Completed') {
      zones[row.zone].done += 1;
    }
  });

  var html = Object.keys(zones).sort().map(function(zone) {
    var stats = zones[zone];
    var override = readingSheetState.zoneProgressOverrides[zone];
    var currentDone = override ? override.done : stats.done;
    var currentTotal = override ? override.total : stats.total;
    var percentage = currentTotal > 0 ? Math.round((currentDone / currentTotal) * 100) : 0;

    return (
      '<div class="zone-input-card" data-zone="' + zone + '">' +
        '<div class="zone-input-header">' +
          '<span class="zone-input-label">' + zone + '</span>' +
          '<span class="zone-input-current">Current: ' + currentDone + '/' + currentTotal + ' (' + percentage + '%)</span>' +
        '</div>' +
        '<div class="zone-input-controls">' +
          '<div class="zone-input-field">' +
            '<input type="number" class="zone-input-number zone-done-input" ' +
              'min="0" max="' + currentTotal + '" value="' + currentDone + '" ' +
              'data-zone="' + zone + '" data-type="done" />' +
            '<span>/</span>' +
            '<input type="number" class="zone-input-number zone-total-input" ' +
              'min="1" value="' + currentTotal + '" ' +
              'data-zone="' + zone + '" data-type="total" />' +
          '</div>' +
          '<input type="range" class="zone-input-slider" ' +
            'min="0" max="' + currentTotal + '" value="' + currentDone + '" ' +
            'data-zone="' + zone + '" />' +
          '<button type="button" class="zone-input-apply" data-zone="' + zone + '">Apply</button>' +
        '</div>' +
      '</div>'
    );
  }).join('');

  zoneInputsContainer.innerHTML = html;

  // Add event listeners
  bindZoneInputEvents();
}

/**
 * Bind events to zone input controls
 */
function bindZoneInputEvents() {
  var manualToggle = document.getElementById('manualProgressToggle');
  var zoneInputsContainer = document.getElementById('zoneProgressInputs');

  // Toggle manual progress mode
  if (manualToggle) {
    manualToggle.addEventListener('change', function() {
      readingSheetState.manualProgress = this.checked;
      zoneInputsContainer.classList.toggle('is-active', this.checked);
      if (!this.checked) {
        // Reset overrides when disabling manual mode
        readingSheetState.zoneProgressOverrides = {};
        generateZoneProgressInputs(); // Regenerate with original values
      }
      applyAndRender(); // Re-render table to update progress bars
    });
  }

  // Zone input changes
  document.addEventListener('input', function(e) {
    if (!e.target.classList.contains('zone-input-number') && 
        !e.target.classList.contains('zone-input-slider')) return;

    var zone = e.target.dataset.zone;
    var card = document.querySelector('[data-zone="' + zone + '"]');
    if (!card) return;

    var doneInput = card.querySelector('.zone-done-input');
    var totalInput = card.querySelector('.zone-total-input');
    var slider = card.querySelector('.zone-input-slider');

    if (e.target.classList.contains('zone-input-slider')) {
      // Slider changed, update done input
      doneInput.value = e.target.value;
    } else if (e.target.dataset.type === 'done') {
      // Done input changed, update slider
      slider.value = e.target.value;
      slider.max = totalInput.value;
    } else if (e.target.dataset.type === 'total') {
      // Total input changed, update slider max and adjust done if needed
      var newTotal = parseInt(e.target.value) || 1;
      var currentDone = parseInt(doneInput.value) || 0;
      
      if (currentDone > newTotal) {
        doneInput.value = newTotal;
        slider.value = newTotal;
      }
      slider.max = newTotal;
    }

    // Update current display
    updateZoneCurrentDisplay(zone, card);
  });

  // Apply button clicks
  document.addEventListener('click', function(e) {
    if (!e.target.classList.contains('zone-input-apply')) return;

    var zone = e.target.dataset.zone;
    var card = document.querySelector('[data-zone="' + zone + '"]');
    if (!card) return;

    var doneInput = card.querySelector('.zone-done-input');
    var totalInput = card.querySelector('.zone-total-input');
    var done = parseInt(doneInput.value) || 0;
    var total = parseInt(totalInput.value) || 1;

    // Validate inputs
    if (done > total) {
      done = total;
      doneInput.value = done;
    }

    // Store override
    readingSheetState.zoneProgressOverrides[zone] = { done: done, total: total };

    // Update display and re-render table
    updateZoneCurrentDisplay(zone, card);
    applyAndRender();

    // Visual feedback
    e.target.textContent = 'Applied!';
    e.target.disabled = true;
    setTimeout(function() {
      e.target.textContent = 'Apply';
      e.target.disabled = false;
    }, 1000);
  });
}

/**
 * Update the current display for a zone input card
 */
function updateZoneCurrentDisplay(zone, card) {
  var doneInput = card.querySelector('.zone-done-input');
  var totalInput = card.querySelector('.zone-total-input');
  var currentDisplay = card.querySelector('.zone-input-current');
  
  var done = parseInt(doneInput.value) || 0;
  var total = parseInt(totalInput.value) || 1;
  var percentage = Math.round((done / total) * 100);
  
  currentDisplay.textContent = 'Preview: ' + done + '/' + total + ' (' + percentage + '%)';
}

/**
 * Enhanced render function that uses manual overrides when available
 */
function renderFilteredTable() {
  applyAndRender();
}