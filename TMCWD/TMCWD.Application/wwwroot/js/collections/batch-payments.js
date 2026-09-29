//  Phase 4: CSV upload stub + full pagination.
// ============================================================
(function initBatchPaymentsModal() {
  'use strict';

  //  DOM refs 
  var overlay       = document.getElementById('bpOverlay');
  var openBtn       = document.getElementById('batchPaymentBtn');
  var closeBtn      = document.getElementById('bpCloseBtn');
  var dateInput     = document.getElementById('batchDate');
  var channelSelect = document.getElementById('batchPaymentChannel');
  var searchEl      = document.getElementById('batchSearch');
  var tableBody     = document.getElementById('bpTableBody');
  var uploadBtn     = document.getElementById('uploadCsvBtn');
  var csvFileInput  = document.getElementById('batchCsvFileInput');
  var rowsSelect    = document.getElementById('batchRowsPerPageSelect');
  var rangeLabel    = document.getElementById('batchPaginationRangeLabel');
  var prevBtn       = document.getElementById('batchPrevPageBtn');
  var nextBtn       = document.getElementById('batchNextPageBtn');

  // Guard — nothing to wire if modal markup is absent.
  if (!overlay || !openBtn || !closeBtn) return;

  //  Module-level state 

  // Full dataset — populated once per open; rows are prepended on upload.
  var batchPaymentsData = [];

  // The last filtered result — recalculated by applyBatchFilters() and
  // held here so pagination can re-slice it without re-filtering.
  var batchFilteredData = [];

  // Pagination state — mirrors the cm-* / colrep-* pattern in this file.
  var batchPagination = {
    currentPage: 1,
    rowsPerPage: 10    // matches batchRowsPerPageSelect default option
  };

  // Debounce timer for the search input.
  var batchSearchTimer = null;

  //  Helpers 
  var MONTH_NAMES = ['Jan','Feb','Mar','Apr','May','Jun',
                     'Jul','Aug','Sep','Oct','Nov','Dec'];

  function formatDate(iso) {
    var p = iso.split('-');
    if (p.length !== 3) return iso;
    return MONTH_NAMES[parseInt(p[1], 10) - 1] + ' ' +
           parseInt(p[2], 10) + ', ' + p[0];
  }

  function daysAgo(n) {
    var d = new Date();
    d.setDate(d.getDate() - n);
    return d.toISOString().slice(0, 10);
  }

  function todayISO() {
    return new Date().toISOString().slice(0, 10);
  }

  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  //  1. Data loading 
  // Fetches batch-upload log from the server for a configurable date range.
  // Falls back to an empty array on any error so the modal still opens cleanly.
  function loadBatchPaymentsData() {
    var today   = todayISO();
    var thirtyAgo = daysAgo(30);
    var url = '/Collections/BatchPayments?from=' +
              encodeURIComponent(thirtyAgo) + '&to=' + encodeURIComponent(today);
    return fetch(url)
      .then(function (res) {
        if (!res.ok) throw new Error('Server returned ' + res.status);
        return res.json();
      })
      .catch(function (err) {
        console.warn('[BatchPayments] loadBatchPaymentsData error:', err);
        return [];
      });
  }

  //  2. CSV upload 
  // POSTs the file as multipart/form-data to the MVC action and returns
  // the saved record so the caller can prepend a real row to the table.
  function uploadBatchCsv(file) {
    var csrf    = (document.querySelector('input[name="__RequestVerificationToken"]') || {}).value || '';
    var channel = channelSelect && channelSelect.value ? channelSelect.value : 'Manual';
    var form    = new FormData();
    form.append('file',    file);
    form.append('channel', channel);
    return fetch('/Collections/BatchPaymentUpload', {
      method:  'POST',
      headers: { 'RequestVerificationToken': csrf },
      body:    form
    }).then(function (res) {
      if (!res.ok) return res.json().then(function (b) { throw new Error(b.message || 'Upload failed.'); });
      return res.json();
    });
  }

  // Wire the invisible file <input> to the visible upload button.
  // Shows a brief loading state while "uploading", then prepends a new
  // mock row so the freshly uploaded file appears at the top of the list.
  if (uploadBtn && csvFileInput) {

    // Button click → trigger the native file picker
    uploadBtn.addEventListener('click', function () {
      csvFileInput.value = '';   // reset so re-selecting the same file fires 'change'
      csvFileInput.click();
    });

    // File selected → run the upload stub
    csvFileInput.addEventListener('change', function () {
      var file = this.files && this.files[0];
      if (!file) return;

      // Loading state on the button
      var originalHTML = uploadBtn.innerHTML;
      uploadBtn.disabled = true;
      uploadBtn.innerHTML =
        '<svg class="bp-upload-spinner" viewBox="0 0 24 24" fill="none" ' +
        'stroke="currentColor" stroke-width="2.5" width="16" height="16" aria-hidden="true">' +
        '<path d="M12 2a10 10 0 0 1 10 10" stroke-linecap="round"/></svg>';

      uploadBatchCsv(file)
        .then(function (newRow) {
          // Server returns the saved record — use it directly so the ID is real.
          batchPaymentsData.unshift(newRow);

          // Refresh channel dropdown in case the new row adds a novel channel
          populateChannelDropdown(batchPaymentsData);

          // Re-apply all filters so the new row is visible if it matches
          applyBatchFilters();
        })
        .catch(function (err) {
          console.error('[uploadBatchCsv] error:', err);
          var card = document.getElementById('toast');
          var bd   = document.getElementById('toastBackdrop');
          if (card) {
            document.getElementById('toastTitle').textContent = 'Upload Failed';
            document.getElementById('toastMsg').textContent   = err.message || 'Could not upload the file. Please try again.';
            document.getElementById('toastIconSuccess').style.display = 'none';
            document.getElementById('toastIconDanger').style.display  = 'block';
            card.className = 'toast-card show'; bd.className = 'toast-backdrop show';
            clearTimeout(window._bpUploadErrTimer);
            window._bpUploadErrTimer = setTimeout(function () {
              card.className = 'toast-card'; bd.className = 'toast-backdrop';
            }, 4000);
          }
        })
        .finally(function () {
          uploadBtn.disabled = false;
          uploadBtn.innerHTML = originalHTML;
        });
    });
  }

  //  3. Channel dropdown population 
  function populateChannelDropdown(data) {
    if (!channelSelect) return;
    var seen = {}, channels = [];
    data.forEach(function (item) {
      if (!seen[item.channel]) { seen[item.channel] = true; channels.push(item.channel); }
    });

    var current = channelSelect.value;
    channelSelect.innerHTML = '';
    var allOpt = document.createElement('option');
    allOpt.value = ''; allOpt.textContent = 'All Channels';
    channelSelect.appendChild(allOpt);
    channels.forEach(function (ch) {
      var opt = document.createElement('option');
      opt.value = ch; opt.textContent = ch;
      channelSelect.appendChild(opt);
    });
    channelSelect.value = (current && seen[current]) ? current : '';
  }

  //  4. Detail view 
  // Opens the detail for a batch upload in a lightweight fetch → toast pattern.
  function viewBatchDetails(id, filename) {
    fetch('/Collections/BatchPaymentDetail?id=' + encodeURIComponent(id))
      .then(function (res) { return res.ok ? res.json() : null; })
      .then(function (detail) {
        if (!detail) return;
        var card = document.getElementById('toast');
        var bd   = document.getElementById('toastBackdrop');
        if (!card) return;
        document.getElementById('toastTitle').textContent = 'Batch: ' + detail.filename;
        document.getElementById('toastMsg').textContent   =
          'Channel: ' + detail.channel + '  |  Rows: ' + detail.rowCount +
          '  |  Status: ' + detail.status + '  |  Date: ' + detail.dateUploaded;
        document.getElementById('toastIconSuccess').style.display = 'block';
        document.getElementById('toastIconDanger').style.display  = 'none';
        card.className = 'toast-card show';
        bd.className   = 'toast-backdrop show';
        clearTimeout(window._bpDetailTimer);
        window._bpDetailTimer = setTimeout(function () {
          card.className = 'toast-card'; bd.className = 'toast-backdrop';
        }, 5000);
      })
      .catch(function (err) {
        console.warn('[BatchPayments] viewBatchDetails error:', err);
      });
  }

  //  5. Table rendering (accepts a page-slice) 
  function renderBatchPaymentsTable(data) {
    if (!tableBody) return;
    tableBody.innerHTML = '';

    if (!data || data.length === 0) {
      var emptyRow = document.createElement('tr');
      emptyRow.innerHTML =
        '<td colspan="4" class="bp-empty-row">No batch payments found.</td>';
      tableBody.appendChild(emptyRow);
      return;
    }

    data.forEach(function (item) {
      var row = document.createElement('tr');

      var idCell = document.createElement('td');
      var link   = document.createElement('a');
      link.href      = '#';
      link.className = 'bp-id-link';
      link.setAttribute('aria-label',
        'View details for ' + item.id + ' / ' + item.filename);
      link.innerHTML =
        '<span class="bp-id-code">' + escapeHtml(item.id)       + '</span>' +
        '<span class="bp-id-file">' + escapeHtml(item.filename) + '</span>';
      (function (cid, cfile) {
        link.addEventListener('click', function (e) {
          e.preventDefault();
          viewBatchDetails(cid, cfile);
        });
      }(item.id, item.filename));
      idCell.appendChild(link);

      var channelCell = document.createElement('td');
      channelCell.textContent = item.channel;

      var statusCell = document.createElement('td');
      statusCell.textContent = item.status;

      var dateCell = document.createElement('td');
      dateCell.textContent = formatDate(item.dateUploaded);

      row.appendChild(idCell);
      row.appendChild(channelCell);
      row.appendChild(statusCell);
      row.appendChild(dateCell);
      tableBody.appendChild(row);
    });
  }

  //  6. Pagination controls 
  // Updates range label and enables / disables arrow buttons.
  // Clamps currentPage when rowsPerPage shrinks the range.
  function updatePaginationControls(total) {
    if (!rangeLabel) return;

    var rpp        = batchPagination.rowsPerPage;
    var totalPages = Math.max(1, Math.ceil(total / rpp));

    // Clamp — prevents currentPage sitting beyond the last page
    if (batchPagination.currentPage > totalPages) {
      batchPagination.currentPage = totalPages;
    }

    var start = total === 0 ? 0 : (batchPagination.currentPage - 1) * rpp + 1;
    var end   = Math.min(batchPagination.currentPage * rpp, total);

    rangeLabel.textContent = total === 0
      ? '0\u20130 of 0'
      : start + '\u2013' + end + ' of ' + total;

    if (prevBtn) prevBtn.disabled = total === 0 || batchPagination.currentPage <= 1;
    if (nextBtn) nextBtn.disabled = total === 0 || batchPagination.currentPage >= totalPages;
  }

  // Returns the correct page-slice from batchFilteredData.
  function getPageSlice() {
    var rpp   = batchPagination.rowsPerPage;
    var start = (batchPagination.currentPage - 1) * rpp;
    return batchFilteredData.slice(start, start + rpp);
  }

  // Central view refresh — single call-site for anything that changes
  // what is visible. Reads batchFilteredData directly.
  function refreshView() {
    renderBatchPaymentsTable(getPageSlice());
    updatePaginationControls(batchFilteredData.length);
  }

  //  7. Combined filter function 
  // Reads current values of all three controls, filters batchPaymentsData,
  // stores result in batchFilteredData, resets to page 1, refreshes view.
  function applyBatchFilters() {
    var dateVal    = dateInput     ? dateInput.value.trim()              : '';
    var channelVal = channelSelect ? channelSelect.value.trim()          : '';
    var searchVal  = searchEl      ? searchEl.value.trim().toLowerCase() : '';

    batchFilteredData = batchPaymentsData.filter(function (item) {
      if (dateVal    && item.dateUploaded !== dateVal)                       return false;
      if (channelVal && item.channel      !== channelVal)                    return false;
      if (searchVal) {
        var hay = (item.id + ' ' + item.filename).toLowerCase();
        if (hay.indexOf(searchVal) === -1)                                   return false;
      }
      return true;
    });

    batchPagination.currentPage = 1;   // reset to page 1 on every filter change
    refreshView();
  }

  //  8. Filter event listeners 

  if (dateInput)     dateInput.addEventListener('change', applyBatchFilters);
  if (channelSelect) channelSelect.addEventListener('change', applyBatchFilters);

  if (searchEl) {
    searchEl.addEventListener('input', function () {
      clearTimeout(batchSearchTimer);
      batchSearchTimer = setTimeout(applyBatchFilters, 300);
    });
  }

  //  9. Pagination event listeners 

  if (rowsSelect) {
    rowsSelect.addEventListener('change', function () {
      batchPagination.rowsPerPage = parseInt(this.value, 10);
      batchPagination.currentPage = 1;
      refreshView();
    });
  }

  if (prevBtn) {
    prevBtn.addEventListener('click', function () {
      if (batchPagination.currentPage > 1) {
        batchPagination.currentPage--;
        refreshView();
      }
    });
  }

  if (nextBtn) {
    nextBtn.addEventListener('click', function () {
      var totalPages = Math.ceil(batchFilteredData.length / batchPagination.rowsPerPage);
      if (batchPagination.currentPage < totalPages) {
        batchPagination.currentPage++;
        refreshView();
      }
    });
  }

  //  10. Reset — restores all controls + pagination to defaults 
  // Called on both open and close so the modal always reopens fresh.
  function resetModal() {
    var today = todayISO();
    if (dateInput)     dateInput.value     = '';   // no default filter — show all rows
    if (channelSelect) channelSelect.value = '';
    if (searchEl)      searchEl.value      = '';
    clearTimeout(batchSearchTimer);

    batchPagination.currentPage = 1;
    batchPagination.rowsPerPage = 10;
    if (rowsSelect) rowsSelect.value = '10';

    // Clear filtered data and reset pagination display
    batchFilteredData = [];
    updatePaginationControls(0);
  }

  //  Open 
  function openModal() {
    resetModal();
    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';

    loadBatchPaymentsData()
      .then(function (data) {
        batchPaymentsData = data;
        populateChannelDropdown(batchPaymentsData);
        // applyBatchFilters reads the just-reset controls so the table
        // opens pre-filtered by today's date at page 1.
        applyBatchFilters();
      })
      .catch(function (err) {
        console.error('[BatchPayments] Data load error:', err);
        if (tableBody) {
          tableBody.innerHTML =
            '<tr><td colspan="4" class="bp-empty-row">' +
            'Failed to load data. Please try again.</td></tr>';
        }
      });

    setTimeout(function () { if (searchEl) searchEl.focus(); }, 220);
  }

  //  Close 
  // Resets everything so the modal reopens fresh next time.
  function closeModal() {
    resetModal();
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
    openBtn.focus();
  }

  //  Open / close event listeners 

  openBtn.addEventListener('click', function (e) { e.preventDefault(); openModal(); });
  closeBtn.addEventListener('click', closeModal);
  overlay.addEventListener('click', function (e) { if (e.target === overlay) closeModal(); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

  //  One-time style injection 
  // Injects the upload-button spinner animation without a separate CSS file.
  // Mirrors the colrep-* pattern used by initCollectionReportModal above.
  (function injectBpStyles() {
    if (document.getElementById('bp-dynamic-styles')) return;
    var style = document.createElement('style');
    style.id = 'bp-dynamic-styles';
    style.textContent =
      '@keyframes bpSpin{from{transform:rotate(0deg)}to{transform:rotate(360deg)}}' +
      '.bp-upload-spinner{animation:bpSpin .7s linear infinite;display:block;}';
    document.head.appendChild(style);
  }());

}());
