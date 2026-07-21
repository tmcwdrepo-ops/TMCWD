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
  statusFilter:  'in-progress'  // 'in-progress' | 'completed'
};


/* ============================================================
   UTILITY
   ============================================================ */

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

  // Build zone stats from FULL dataset so the bar is always accurate
  var zoneStats = {};
  readingSheetState.rows.forEach(function(r) {
    if (!zoneStats[r.zone]) zoneStats[r.zone] = { total: 0, done: 0 };
    zoneStats[r.zone].total += 1;
    if (r.status === 'Completed') zoneStats[r.zone].done += 1;
  });

  var html = rows.map(function(row) {
    var badgeClass  = row.status === 'Completed' ? 'badge--green' : 'badge--yellow';
    var isSelected  = readingSheetState.selectedIds.has(row.id);
    var rowClass    = isSelected ? ' class="is-selected"' : '';
    var chkChecked  = isSelected ? ' checked' : '';

    // Zone mini progress — computed from full dataset
    var zoneStat    = zoneStats[row.zone] || { total: 0, done: 0 };
    var isCompleted = readingSheetState.statusFilter === 'completed';

    // In-Progress view: done / total (bar fills as work completes)
    // Completed view:   bar always 100% full
    var zonePct  = isCompleted ? 100 : (zoneStat.total > 0 ? Math.round((zoneStat.done / zoneStat.total) * 100) : 0);
    var labelNum = isCompleted ? zoneStat.total : zoneStat.done;
    var barLabel = labelNum + '/' + zoneStat.total;

    var zoneCell =
      '<div class="zone-cell">' +
        '<span class="zone-cell__label">' + row.zone + '</span>' +
        '<div class="zone-mini-bar" title="' + barLabel + '">' +
          '<div class="zone-mini-bar__track">' +
            '<div class="zone-mini-bar__fill" style="width:' + zonePct + '%"></div>' +
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
        '<td>' + row.billingDate + '</td>' +
        '<td>' + zoneCell + '</td>' +
        '<td>' + row.forPosting + '</td>' +
        '<td><span class="badge ' + badgeClass + '">' + row.status + '</span></td>' +
        '<td class="col-actions">' +
          '<div class="action-btns">' +
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
   ROW ACTIONS — EDIT & DELETE
   ============================================================ */

/**
 * Open the delete confirmation modal and store the pending id.
 * Confirmation is handled by #deleteModalConfirm click.
 *
 * @param {number} id - Row id to delete on confirm.
 */
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
 * Delegated click handler for Edit buttons on the table body.
 * Dispatches a custom "readingSheet:edit" event with the row id.
 * Modal/form logic is wired externally in a later phase.
 *
 * @param {MouseEvent} event
 */
function handleEditClick(event) {
  var btn = event.target.closest('.action-btn--edit');
  if (!btn) return;

  var id = parseInt(btn.dataset.id, 10);
  document.dispatchEvent(new CustomEvent('readingSheet:edit', { detail: { id: id } }));
  console.log('[ReadingSheet] Edit dispatched for id:', id);
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
function handleDeleteConfirm() {
  var backdrop = document.getElementById('deleteModalBackdrop');
  if (!backdrop || !backdrop.dataset.pendingId) return;

  var id = parseInt(backdrop.dataset.pendingId, 10);
  closeDeleteModal();

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
  if (confirmBtn) confirmBtn.disabled = true;

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
function handleBulkDeleteInput(event) {
  var val        = event.target.value.trim().toLowerCase();
  var confirmBtn = document.getElementById('bulkDeleteModalConfirm');
  var isValid    = val === 'delete all';

  if (confirmBtn) confirmBtn.disabled = !isValid;
  event.target.classList.toggle('is-valid', isValid);
}

/**
 * Execute the bulk deletion after modal confirmation.
 */
function handleBulkDeleteConfirm() {
  closeBulkDeleteModal();

  readingSheetState.rows = readingSheetState.rows.filter(function(row) {
    return !readingSheetState.selectedIds.has(row.id);
  });

  readingSheetState.selectedIds.clear();
  readingSheetState.currentPage = 1;

  applyAllFilters();
  updateBulkActionsBar();
  applyAndRender();
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
function initReadingSheetPage() {
  // Reset all runtime state on every init — prevents stale data
  // from a previous page load bleeding through.
  readingSheetState.rows          = [];
  readingSheetState.filteredRows  = [];
  readingSheetState.selectedIds   = new Set();
  readingSheetState.currentPage   = 1;
  readingSheetState.sortColumn    = null;
  readingSheetState.sortDirection = 'asc';
  readingSheetState.statusFilter  = 'in-progress';

  if (typeof READING_SHEET_SAMPLE_DATA !== 'undefined') {
    readingSheetState.rows = READING_SHEET_SAMPLE_DATA.slice();
  }

  applyAllFilters();
  applyAndRender();

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

  if (selectAll)  selectAll.addEventListener('change', handleSelectAll);
  if (tbody) {
    tbody.addEventListener('change', handleRowSelect);
    tbody.addEventListener('click',  handleEditClick);
    tbody.addEventListener('click',  handleDeleteClick);
  }
  if (bulkDelete) bulkDelete.addEventListener('click', handleBulkDelete);
  if (bulkClear)  bulkClear.addEventListener('click', handleBulkClear);

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
  var bulkDeleteInput    = document.getElementById('bulkDeleteConfirmInput');

  if (bulkDeleteConfirm)  bulkDeleteConfirm.addEventListener('click', handleBulkDeleteConfirm);
  if (bulkDeleteCancel)   bulkDeleteCancel.addEventListener('click', closeBulkDeleteModal);
  if (bulkDeleteInput)    bulkDeleteInput.addEventListener('input', handleBulkDeleteInput);
  if (bulkDeleteBackdrop) {
    bulkDeleteBackdrop.addEventListener('click', function(event) {
      if (event.target === bulkDeleteBackdrop) closeBulkDeleteModal();
    });
  }

  // Escape closes whichever modal is open
  document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
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
