
//  Phases 2 \u2013 4: data loading, rendering, pagination, cleanup.
//  Naming follows the cr-* / colrep-* pattern used by the
//  Collection Remittance and Collection Report modals above.
// ============================================================
(function initCollectionMonitoringModal() {
  'use strict';

  //  DOM refs 
  var overlay       = document.getElementById('cmOverlay');
  var openBtn       = document.getElementById('collectionMonitoringBtn');
  var closeBtn      = document.getElementById('cmCloseBtn');
  var tableBody     = document.getElementById('cmTableBody');
  var totalAmountEl = document.getElementById('totalCollectionAmount');
  var rowsSelect    = document.getElementById('rowsPerPageSelect');
  var rangeLabel    = document.getElementById('paginationRangeLabel');
  var prevBtn       = document.getElementById('prevPageBtn');
  var nextBtn       = document.getElementById('nextPageBtn');

  // Guard \u2014 nothing to wire if any element is absent.
  if (!overlay || !openBtn || !closeBtn || !tableBody || !totalAmountEl ||
      !rowsSelect || !rangeLabel || !prevBtn || !nextBtn) return;

  // Full dataset 
  // Populated once per open; every render function reads from here
  // so all views (table, total, pagination) share a single source.
  var monitoringData = [];

  //  Pagination state 
  // reset to page 1 on open so the modal reopens fresh.
  var monitoringPagination = {
    currentPage: 1,
    rowsPerPage: 5
  };

  //  Helpers 
  function fmtCurrency(amount) {
    return '\u20B1' + Number(amount).toLocaleString('en-PH', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  //  Data loading 
  // TODO: replace the mock below with a real API call, e.g.:
  //
  //   return fetch('/api/Billing/CollectionMonitoring')
  //     .then(function (r) {
  //       if (!r.ok) throw new Error(r.statusText);
  //       return r.json();
  //     });
  //
  // The returned array must contain objects shaped as:
  //   { collector, orFrom, orTo, cashOnHand }
  function loadCollectionMonitoringData() {
    return Promise.resolve(
      (typeof SAMPLE_COLLECTION_MONITORING !== 'undefined') ? SAMPLE_COLLECTION_MONITORING : []
    );
  }

  //  Total collection 
  // Sums the FULL dataset \u2014 never the current page slice.
  // Called once after load; pagination never calls this again.
  function calculateTotalCollection(data) {
    var total = data.reduce(function (sum, item) {
      return sum + (item.cashOnHand || 0);
    }, 0);
    totalAmountEl.textContent = fmtCurrency(total);
  }

  //  Table rendering 
  // Accepts whatever slice (or full array) is passed to it.
  // Empty-state message shown when the slice has zero rows.
  function renderCollectionMonitoringTable(data) {
    tableBody.innerHTML = '';

    if (!data || data.length === 0) {
      var emptyRow = document.createElement('tr');
      emptyRow.innerHTML = '<td colspan="4" class="cm-empty-row">No collections found.</td>';
      tableBody.appendChild(emptyRow);
      return;
    }

    data.forEach(function (item) {
      var row = document.createElement('tr');
      row.innerHTML =
        '<td>' + item.collector              + '</td>' +
        '<td>' + item.orFrom                 + '</td>' +
        '<td>' + item.orTo                   + '</td>' +
        '<td>' + fmtCurrency(item.cashOnHand)+ '</td>';
      tableBody.appendChild(row);
    });
  }

  //  Page slice 
  // Pure helper \u2014 no side effects. Returns the correct sub-array
  // for the given pagination state.
  function getPageSlice(data, pagination) {
    var start = (pagination.currentPage - 1) * pagination.rowsPerPage;
    var end   = start + pagination.rowsPerPage;
    return data.slice(start, end);
  }

  //  Pagination controls 
  // Updates range label and enables / disables arrow buttons.
  // Also clamps currentPage when rowsPerPage shrinks the range.
  function updatePaginationControls(data, pagination) {
    var total      = data.length;
    var totalPages = Math.max(1, Math.ceil(total / pagination.rowsPerPage));

    // Clamp \u2014 prevents currentPage from sitting beyond the last page.
    if (pagination.currentPage > totalPages) {
      pagination.currentPage = totalPages;
    }

    var start = total === 0 ? 0 : (pagination.currentPage - 1) * pagination.rowsPerPage + 1;
    var end   = Math.min(pagination.currentPage * pagination.rowsPerPage, total);

    rangeLabel.textContent = total === 0
      ? '0\u20130 of 0'
      : start + '\u2013' + end + ' of ' + total;

    // Disable arrows at the boundaries; both disabled when no data.
    prevBtn.disabled = total === 0 || pagination.currentPage <= 1;
    nextBtn.disabled = total === 0 || pagination.currentPage >= totalPages;
  }

  //  Central view refresh
  // Single call-site for anything that changes what is visible.
  // Reads monitoringData directly so callers cannot pass a stale copy.
  function refreshView() {
    var slice = getPageSlice(monitoringData, monitoringPagination);
    renderCollectionMonitoringTable(slice);
    updatePaginationControls(monitoringData, monitoringPagination);
  }

  //  Pagination resets 
  // Restores state to defaults \u2014 called on close so the modal
  // always reopens at page 1 with the default row count.
  function resetPagination() {
    monitoringPagination.currentPage = 1;
    monitoringPagination.rowsPerPage = 5;
    rowsSelect.value = '5';
  }

  // Rows-per-page select 
  rowsSelect.addEventListener('change', function () {
    monitoringPagination.rowsPerPage = parseInt(this.value, 10);
    monitoringPagination.currentPage = 1;   // always back to page 1
    refreshView();
  });

  //  Prev / Next buttons 
  prevBtn.addEventListener('click', function () {
    if (monitoringPagination.currentPage > 1) {
      monitoringPagination.currentPage--;
      refreshView();
    }
  });

  nextBtn.addEventListener('click', function () {
    var totalPages = Math.ceil(monitoringData.length / monitoringPagination.rowsPerPage);
    if (monitoringPagination.currentPage < totalPages) {
      monitoringPagination.currentPage++;
      refreshView();
    }
  });

  // Open 
  function openModal() {
    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';
    closeBtn.focus();

    loadCollectionMonitoringData()
      .then(function (data) {
        // Store the full dataset \u2014 all functions read from here.
        monitoringData = data;

        // Reset to page 1 with the default row count before rendering.
        resetPagination();

        // Total is set once from the full dataset and never changes
        // as the user pages through \u2014 satisfies Phase 2 & 4 requirements.
        calculateTotalCollection(monitoringData);

        // Render the first page slice and sync pagination controls.
        refreshView();
      })
      .catch(function (err) {
        console.error('[CollectionMonitoring] Data load error:', err);
        // Show error state; disable controls so nothing is clickable.
        tableBody.innerHTML = '<tr><td colspan="4" class="cm-empty-row">Failed to load data. Please try again.</td></tr>';
        totalAmountEl.textContent = '0.00';
        rangeLabel.textContent    = '0\u20130 of 0';
        prevBtn.disabled = true;
        nextBtn.disabled = true;
      });
  }

  //  Close
  // Resets pagination on every close so the modal always reopens
  // at page 1 \u2014 it never remembers the last page viewed.
  function closeModal() {
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
    resetPagination();
    openBtn.focus();
  }

  // Event listeners
  openBtn.addEventListener('click', function (e) {
    e.preventDefault();
    openModal();
  });

  closeBtn.addEventListener('click', closeModal);

  // Dim overlay click (outside the card) â†’ close
  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });

  // Escape key  close (only when this modal is the visible one)
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

}());
