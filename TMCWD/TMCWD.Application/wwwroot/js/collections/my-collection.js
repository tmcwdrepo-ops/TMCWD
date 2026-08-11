//  Phase 4 : full pagination, reset-on-close, final style pass.
// ============================================================
(function initMyCollectionModal() {
  'use strict';

  //  DOM refs 
  var overlay      = document.getElementById('mclOverlay');
  var openBtn      = document.getElementById('myCollectionBtn');
  var closeBtn     = document.getElementById('mclCloseBtn');
  var dateInput    = document.getElementById('collectionListDate');
  var searchInput  = document.getElementById('collectionListSearch');
  var tableBody    = document.getElementById('mclTableBody');
  var rangeLabel   = document.getElementById('collectionListPaginationRangeLabel');
  var rowsSelect   = document.getElementById('collectionListRowsPerPageSelect');
  var prevBtn      = document.getElementById('collectionListPrevPageBtn');
  var nextBtn      = document.getElementById('collectionListNextPageBtn');

  if (!overlay || !openBtn || !closeBtn) return;

  //  Module-level state 
  // collectionListData     — full dataset for the selected date;
  //                          never mutated by filtering.
  // collectionListFiltered — result of the last applyCollectionListFilters()
  //                          call; pagination always slices this.
  // collectionListPagination — single source of truth for page state.
  var collectionListData     = [];
  var collectionListFiltered = [];
  var collectionListPagination = { currentPage: 1, rowsPerPage: 5 };

  // Debounce timer for the search input.
  var mclSearchTimer = null;

  //  Helpers 
  function todayISO() {
    return new Date().toISOString().slice(0, 10);
  }

  function fmtCurrency(amount) {
    return '\u20B1' + Number(amount).toLocaleString('en-PH', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  //  1. Data loading 
  // TODO: replace with a real API call, e.g.:
  //   return fetch('/api/Billing/MyCollection?date=' + encodeURIComponent(date))
  //     .then(function (r) {
  //       if (!r.ok) throw new Error(r.statusText);
  //       return r.json();
  //     });
  // Expected item shape: { invoiceNo, account, totalAmount, transactionType }
  function loadCollectionListData(date) {
    var sample = (typeof SAMPLE_COLLECTION_LIST !== 'undefined')
      ? SAMPLE_COLLECTION_LIST
      : [];
    // When a real API is wired the date parameter will be sent as a query filter.
    // For now every date returns the full sample set.
    return Promise.resolve(sample);
  }

  //  2. Table rendering 
  // Accepts the page-slice only — caller is responsible for slicing.
  // Empty array → "No records found." spanning all 4 columns.
  function renderCollectionListTable(data) {
    if (!tableBody) return;
    tableBody.innerHTML = '';

    if (!data || data.length === 0) {
      var emptyRow = document.createElement('tr');
      emptyRow.innerHTML =
        '<td colspan="4" class="mcl-empty-row">No records found.</td>';
      tableBody.appendChild(emptyRow);
      return;
    }

    data.forEach(function (item) {
      var row = document.createElement('tr');

      var tdInvoice = document.createElement('td');
      tdInvoice.textContent = item.invoiceNo;

      var tdAccount = document.createElement('td');
      tdAccount.textContent = item.account;

      var tdAmount = document.createElement('td');
      tdAmount.textContent = fmtCurrency(item.totalAmount);

      var tdType = document.createElement('td');
      tdType.textContent = item.transactionType;

      row.appendChild(tdInvoice);
      row.appendChild(tdAccount);
      row.appendChild(tdAmount);
      row.appendChild(tdType);
      tableBody.appendChild(row);
    });
  }

  //  3. Page slice helper 
  // Pure function — no side-effects. Returns the sub-array for
  // the current pagination state applied to collectionListFiltered.
  function getMclPageSlice() {
    var rpp   = collectionListPagination.rowsPerPage;
    var start = (collectionListPagination.currentPage - 1) * rpp;
    return collectionListFiltered.slice(start, start + rpp);
  }

  //  4. Pagination controls 
  // Updates range label and enables / disables arrow buttons.
  // Clamps currentPage when rowsPerPage shrinks the valid range.
  function updateMclPaginationControls() {
    var total      = collectionListFiltered.length;
    var rpp        = collectionListPagination.rowsPerPage;
    var totalPages = Math.max(1, Math.ceil(total / rpp));

    // Clamp — prevents currentPage sitting beyond the last page.
    if (collectionListPagination.currentPage > totalPages) {
      collectionListPagination.currentPage = totalPages;
    }

    var start = total === 0
      ? 0
      : (collectionListPagination.currentPage - 1) * rpp + 1;
    var end = Math.min(collectionListPagination.currentPage * rpp, total);

    if (rangeLabel) {
      rangeLabel.textContent = total === 0
        ? '1\u20130 of 0'
        : start + '\u2013' + end + ' of ' + total;
    }

    if (prevBtn) prevBtn.disabled =
      total === 0 || collectionListPagination.currentPage <= 1;
    if (nextBtn) nextBtn.disabled =
      total === 0 || collectionListPagination.currentPage >= totalPages;
  }

  //  5. Central view refresh 
  // Single call-site — slices collectionListFiltered, renders the
  // page, and syncs all pagination controls.
  function refreshMclView() {
    renderCollectionListTable(getMclPageSlice());
    updateMclPaginationControls();
  }

  //  6. Combined filter function 
  // Reads the current search term, filters collectionListData into
  // collectionListFiltered, resets to page 1, then refreshes the view.
  // Never re-fetches — only operates on the in-memory date dataset.
  function applyCollectionListFilters() {
    var term = searchInput ? searchInput.value.trim().toLowerCase() : '';

    collectionListFiltered = term
      ? collectionListData.filter(function (item) {
          return item.invoiceNo.toLowerCase().indexOf(term) !== -1 ||
                 item.account.toLowerCase().indexOf(term)   !== -1;
        })
      : collectionListData.slice();

    collectionListPagination.currentPage = 1;
    refreshMclView();
  }

  //  7. Load for a given date, then apply any active filter 
  // Owns the full "fetch → store → clear search → filter → render"
  // pipeline. Both openModal() and the date-change handler use this.
  function loadAndRender(date) {
    loadCollectionListData(date)
      .then(function (data) {
        collectionListData = data;
        collectionListPagination.currentPage = 1;
        if (searchInput) searchInput.value = '';
        applyCollectionListFilters();
      })
      .catch(function (err) {
        console.error('[MyCollection] Data load error:', err);
        collectionListData     = [];
        collectionListFiltered = [];
        collectionListPagination.currentPage = 1;
        renderCollectionListTable([]);
        updateMclPaginationControls();
      });
  }

  //  8. Reset — restores all controls + state to defaults 
  // Called on close so the modal always reopens fresh next time.
  function resetMclModal() {
    if (dateInput)   dateInput.value   = todayISO();
    if (searchInput) searchInput.value = '';
    clearTimeout(mclSearchTimer);

    collectionListData     = [];
    collectionListFiltered = [];
    collectionListPagination.currentPage = 1;
    collectionListPagination.rowsPerPage = 5;
    if (rowsSelect) rowsSelect.value = '5';

    // Reset pagination display to blank/disabled state
    if (rangeLabel) rangeLabel.textContent = '1\u20130 of 0';
    if (prevBtn) prevBtn.disabled = true;
    if (nextBtn) nextBtn.disabled = true;
  }

  //  Open 
  function openModal() {
    // Always seed today before loadAndRender reads dateInput.value
    if (dateInput) dateInput.value = todayISO();

    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';
    if (closeBtn) closeBtn.focus();

    loadAndRender(dateInput ? dateInput.value : todayISO());
  }

  //  Close 
  function closeModal() {
    resetMclModal();
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
    openBtn.focus();
  }

  //  Event listeners 

  openBtn.addEventListener('click', function (e) {
    e.preventDefault();
    openModal();
  });

  closeBtn.addEventListener('click', closeModal);

  // Dim overlay click → close
  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });

  // Escape key → close (only when this modal is the visible one)
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

  // Date change → re-fetch for the new date, clear search, re-render
  if (dateInput) {
    dateInput.addEventListener('change', function () {
      loadAndRender(this.value);
    });
  }

  // Search input → debounced filter (no re-fetch)
  if (searchInput) {
    searchInput.addEventListener('input', function () {
      clearTimeout(mclSearchTimer);
      mclSearchTimer = setTimeout(applyCollectionListFilters, 300);
    });
  }

  // Rows-per-page select → update rowsPerPage, reset to page 1, refresh
  if (rowsSelect) {
    rowsSelect.addEventListener('change', function () {
      collectionListPagination.rowsPerPage = parseInt(this.value, 10);
      collectionListPagination.currentPage = 1;
      refreshMclView();
    });
  }

  // Previous page button
  if (prevBtn) {
    prevBtn.addEventListener('click', function () {
      if (collectionListPagination.currentPage > 1) {
        collectionListPagination.currentPage--;
        refreshMclView();
      }
    });
  }

  // Next page button
  if (nextBtn) {
    nextBtn.addEventListener('click', function () {
      var totalPages = Math.ceil(
        collectionListFiltered.length / collectionListPagination.rowsPerPage
      );
      if (collectionListPagination.currentPage < totalPages) {
        collectionListPagination.currentPage++;
        refreshMclView();
      }
    });
  }

}());
