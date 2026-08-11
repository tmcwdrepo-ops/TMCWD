//  Phase 4: final cleanup, guards, consistent naming.
// ============================================================
(function initInvoiceSearchModal() {
  'use strict';

  //  DOM refs 
  var overlay       = document.getElementById('isOverlay');
  var openBtn       = document.getElementById('invoiceSearchBtn');
  var closeBtn      = document.getElementById('isCloseBtn');
  var searchInput   = document.getElementById('invoiceSearchInput');
  var searchBtn     = document.getElementById('invoiceSearchBtn2');
  var resultsArea   = document.getElementById('invoiceSearchResults');
  var floatingLabel = document.getElementById('isFloatingLabel');

  // Guard — nothing to wire if modal markup is absent.
  if (!overlay || !openBtn || !closeBtn) return;

  //  Data source 
  // Reference sample data from collection_sample.js
  // TODO: replace with fetch('/api/Billing/InvoiceSearch?query=<term>')
  //       .then(function (r) { if (!r.ok) throw new Error(r.statusText); return r.json(); })
  //       when backend endpoint is ready.
  var invoiceData = (typeof SAMPLE_INVOICE_SEARCH !== 'undefined') ? SAMPLE_INVOICE_SEARCH : [];

  //  In-flight guard 
  // Prevents double-triggering while a search is active.
  var searchInFlight = false;

  //  Helpers 
  function fmtCurrency(amount) {
    return '\u20B1' + Number(amount).toLocaleString('en-PH', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  function formatDate(iso) {
    var parts = iso.split('-');
    if (parts.length !== 3) return iso;
    var months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    return months[parseInt(parts[1], 10) - 1] + ' ' + parseInt(parts[2], 10) + ', ' + parts[0];
  }

  //  Search function 
  // Filters invoiceData by partial case-insensitive match on invoiceNo.
  // TODO: replace the filter logic below with:
  //   return fetch('/api/Billing/InvoiceSearch?query=' + encodeURIComponent(query))
  //     .then(function (r) { if (!r.ok) throw new Error(r.statusText); return r.json(); });
  function searchInvoices(query) {
    var term = query.trim().toLowerCase();
    
    // Empty query → return empty array (don't show full dataset)
    if (!term) return [];
    
    // Filter by partial case-insensitive match on invoiceNo
    return invoiceData.filter(function (invoice) {
      return invoice.invoiceNo.toLowerCase().indexOf(term) !== -1;
    });
  }

  //  Results rendering 
  function renderInvoiceSearchResults(results) {
    if (!resultsArea) return;
    
    resultsArea.innerHTML = '';
    
    // Empty state
    if (!results || results.length === 0) {
      var emptyMsg = document.createElement('div');
      emptyMsg.className = 'is-empty-state';
      emptyMsg.textContent = 'No invoices found';
      resultsArea.appendChild(emptyMsg);
      return;
    }
    
    // Build table
    var table = document.createElement('table');
    table.className = 'is-results-table';
    
    // Header
    var thead = document.createElement('thead');
    thead.innerHTML = '<tr>' +
      '<th>Invoice No.</th>' +
      '<th>Account</th>' +
      '<th style="text-align:right;">Total Amount</th>' +
      '<th>Transaction Type</th>' +
      '<th>Date Issued</th>' +
      '<th>Status</th>' +
    '</tr>';
    table.appendChild(thead);
    
    // Body
    var tbody = document.createElement('tbody');
    results.forEach(function (invoice) {
      var row = document.createElement('tr');
      
      var tdInvoice = document.createElement('td');
      tdInvoice.className = 'is-cell-invoice';
      tdInvoice.textContent = invoice.invoiceNo;
      
      var tdAccount = document.createElement('td');
      tdAccount.textContent = invoice.account;
      
      var tdAmount = document.createElement('td');
      tdAmount.className = 'is-cell-amount';
      tdAmount.textContent = fmtCurrency(invoice.totalAmount);
      
      var tdType = document.createElement('td');
      tdType.textContent = invoice.transactionType;
      
      var tdDate = document.createElement('td');
      tdDate.textContent = formatDate(invoice.dateIssued);
      
      var tdStatus = document.createElement('td');
      tdStatus.className = 'is-cell-status';
      var statusSpan = document.createElement('span');
      statusSpan.className = invoice.status === 'Paid' ? 'is-status-paid' : 'is-status-unpaid';
      statusSpan.textContent = invoice.status;
      tdStatus.appendChild(statusSpan);
      
      row.appendChild(tdInvoice);
      row.appendChild(tdAccount);
      row.appendChild(tdAmount);
      row.appendChild(tdType);
      row.appendChild(tdDate);
      row.appendChild(tdStatus);
      tbody.appendChild(row);
    });
    table.appendChild(tbody);
    
    resultsArea.appendChild(table);
  }

  //  Search trigger 
  // Structured as async so it's ready for a future API call.
  // The in-flight guard prevents double-triggering.
  function performSearch() {
    if (!searchInput || !searchBtn) return;
    if (searchInFlight) return;   // guard against double-trigger
    
    var query = searchInput.value;
    
    // Mark search as in-flight
    searchInFlight = true;
    
    // Loading state
    var originalHTML = searchBtn.innerHTML;
    searchBtn.disabled = true;
    searchBtn.innerHTML = '<svg class="is-spinner" viewBox="0 0 24 24" fill="none" ' +
      'stroke="currentColor" stroke-width="2.5" width="15" height="15" aria-hidden="true">' +
      '<path d="M12 2a10 10 0 0 1 10 10" stroke-linecap="round"/></svg> Searching...';
    
    // Wrap in a short timeout so the spinner is visible even for instant results
    setTimeout(function () {
      try {
        // TODO: replace with await fetch(...).then(r => r.json()) when backend is ready.
        var results = searchInvoices(query);
        renderInvoiceSearchResults(results);
      } catch (err) {
        console.error('[Invoice Search] error:', err);
        renderInvoiceSearchResults([]);
      } finally {
        searchBtn.disabled = false;
        searchBtn.innerHTML = originalHTML;
        searchInFlight = false;
      }
    }, 0);
  }

  //  Open 
  function openModal() {
    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';

    // Clear previous search input and results for a fresh start
    if (searchInput) searchInput.value = '';
    if (resultsArea) resultsArea.innerHTML = '';
    // Restore label in case it was hidden from a previous session
    if (floatingLabel) floatingLabel.classList.remove('is-label-hidden');

    // Focus the search input after the animation settles
    setTimeout(function () {
      if (searchInput) searchInput.focus();
    }, 200);
  }

  //  Close 
  function closeModal() {
    overlay.style.display        = 'none';
    document.body.style.overflow = '';

    // Clear search input and results so the modal reopens fresh next time
    if (searchInput) searchInput.value = '';
    if (resultsArea) resultsArea.innerHTML = '';
    // Restore label visibility
    if (floatingLabel) floatingLabel.classList.remove('is-label-hidden');

    openBtn.focus();
  }

  //  Event listeners 

  // Label hide/show — disappears on focus, reappears when blurred and empty
  if (searchInput && floatingLabel) {
    searchInput.addEventListener('focus', function () {
      floatingLabel.classList.add('is-label-hidden');
    });
    searchInput.addEventListener('blur', function () {
      if (!this.value) floatingLabel.classList.remove('is-label-hidden');
    });
  }

  // Nav bar button → open
  openBtn.addEventListener('click', function (e) {
    e.preventDefault();
    openModal();
  });

  // X button → close
  closeBtn.addEventListener('click', closeModal);

  // Dim overlay click (outside the card) → close
  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });

  // Escape key → close (only when this modal is visible)
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

  // Search button → perform search
  if (searchBtn) {
    searchBtn.addEventListener('click', performSearch);
  }

  // Enter key in search input → perform search
  // Live search: also fires on every keystroke (input event)
  if (searchInput) {
    searchInput.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') {
        e.preventDefault();
        performSearch();
      }
    });

    // Live / as-you-type search — debounced so it doesn't fire on every keystroke
    var liveSearchTimer = null;
    searchInput.addEventListener('input', function () {
      clearTimeout(liveSearchTimer);
      var query = this.value;

      // If input is cleared, clear the results area instantly
      if (!query.trim()) {
        if (resultsArea) resultsArea.innerHTML = '';
        return;
      }

      // Short debounce keeps the UI snappy without hammering future API calls
      liveSearchTimer = setTimeout(function () {
        performSearch();
      }, 250);
    });
  }

}());
