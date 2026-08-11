//  — this file never redefines that array; it only reads it.
// ============================================================
(function initReprintInvoiceModal() {
  'use strict';

  //  DOM refs 
  var overlay      = document.getElementById('riOverlay');
  var openBtnNav   = document.getElementById('reprintInvoiceBtn');
  var closeBtn     = document.getElementById('riCloseBtn');
  var invoiceInput = document.getElementById('reprintInvoiceNumber');
  var printBtn     = document.getElementById('printInvoiceBtn');

  // Guard \u2014 nothing to wire if modal markup is absent.
  if (!overlay || !openBtnNav || !closeBtn) return;

  //  In-flight guard 
  // Mirrors the _exportInFlight pattern used by initCollectionReportModal.
  // Prevents double-triggering while a print Promise is pending.
  var _printInFlight = false;

  //  Inline feedback helpers 
  // One error <span> and one success <span> are created lazily beneath
  // the input wrapper and reused on every call \u2014 no duplicates accumulate.
  // Pattern mirrors initRemittanceSave's showFieldError / clearFieldError.

  function getOrCreateFeedbackEl(cls) {
    var existing = overlay.querySelector('.' + cls);
    if (existing) return existing;
    var span = document.createElement('span');
    span.className = cls;
    var wrap = overlay.querySelector('.ri-input-wrap');
    if (wrap && wrap.parentNode) {
      wrap.parentNode.insertBefore(span, wrap.nextSibling);
    }
    return span;
  }

  function showInputError(msg) {
    // hide success first
    var ok = overlay.querySelector('.ri-input-success');
    if (ok) ok.style.display = 'none';

    var el = getOrCreateFeedbackEl('ri-input-error');
    el.textContent   = msg;
    el.style.display = 'block';
    if (invoiceInput) invoiceInput.classList.add('ri-input--error');
  }

  function clearInputError() {
    var el = overlay.querySelector('.ri-input-error');
    if (el) el.style.display = 'none';
    if (invoiceInput) invoiceInput.classList.remove('ri-input--error');
  }

  function showInputSuccess(msg) {
    // hide error first
    clearInputError();
    var el = getOrCreateFeedbackEl('ri-input-success');
    el.textContent   = msg;
    el.style.display = 'block';
  }

  function clearInputSuccess() {
    var el = overlay.querySelector('.ri-input-success');
    if (el) el.style.display = 'none';
  }

  function clearAllFeedback() {
    clearInputError();
    clearInputSuccess();
  }

  //  Live search suggestions 
  // As the user types, show a dropdown of matching invoices below
  // the input — mirrors the main search-box pattern in Collections.
  // Click or arrow-key navigation selects a suggestion.

  var selectedSuggestionIndex = -1;

  function getOrCreateSuggestionsEl() {
    var existing = overlay.querySelector('.ri-suggestions');
    if (existing) return existing;

    var div = document.createElement('div');
    div.className = 'ri-suggestions';
    div.style.display = 'none';

    // Must be INSIDE .ri-input-wrap so position:absolute anchors correctly
    var wrap = overlay.querySelector('.ri-input-wrap');
    if (wrap) {
      wrap.appendChild(div);
    }
    return div;
  }

  function showSuggestions(searchTerm) {
    if (searchTerm.length < 2) {
      hideSuggestions();
      return;
    }

    var data = (typeof reprintInvoiceSampleData !== 'undefined')
      ? reprintInvoiceSampleData
      : [];

    var term = searchTerm.toUpperCase();
    var matches = data.filter(function (record) {
      return record.invoiceNo.toUpperCase().indexOf(term) !== -1;
    });

    var suggestionsEl = getOrCreateSuggestionsEl();

    if (matches.length === 0) {
      suggestionsEl.innerHTML = '<div class="ri-no-suggestions">No matching invoices</div>';
      suggestionsEl.style.display = 'block';
      return;
    }

    var html = matches.map(function (record, index) {
      return '<div class="ri-suggestion-item" data-index="' + index + '" data-invoice-no="' +
        record.invoiceNo + '">' +
        '<div class="ri-suggestion-invoice">' + record.invoiceNo + '</div>' +
        '<div class="ri-suggestion-account">' + record.account + '</div>' +
        '<div class="ri-suggestion-details">' + record.transactionType + ' \u2022 ' +
        record.status + ' \u2022 ' + record.dateIssued + '</div>' +
      '</div>';
    }).join('');

    suggestionsEl.innerHTML = html;
    suggestionsEl.style.display = 'block';
    selectedSuggestionIndex = -1;

    // Click handler for each suggestion
    suggestionsEl.querySelectorAll('.ri-suggestion-item').forEach(function (item) {
      item.addEventListener('click', function () {
        var invoiceNo = this.dataset.invoiceNo;
        if (invoiceInput) invoiceInput.value = invoiceNo;
        hideSuggestions();
        // Trigger print immediately on selection
        handlePrintInvoice();
      });
    });
  }

  function hideSuggestions() {
    var suggestionsEl = overlay.querySelector('.ri-suggestions');
    if (suggestionsEl) suggestionsEl.style.display = 'none';
    selectedSuggestionIndex = -1;
  }

  function navigateSuggestions(direction) {
    var suggestionsEl = overlay.querySelector('.ri-suggestions');
    if (!suggestionsEl || suggestionsEl.style.display === 'none') return;

    var items = suggestionsEl.querySelectorAll('.ri-suggestion-item');
    if (items.length === 0) return;

    // Remove previous highlight
    if (selectedSuggestionIndex >= 0 && items[selectedSuggestionIndex]) {
      items[selectedSuggestionIndex].classList.remove('ri-suggestion-highlighted');
    }

    // Update index
    if (direction === 'down') {
      selectedSuggestionIndex = (selectedSuggestionIndex + 1) % items.length;
    } else if (direction === 'up') {
      selectedSuggestionIndex = selectedSuggestionIndex <= 0
        ? items.length - 1
        : selectedSuggestionIndex - 1;
    }

    // Highlight new suggestion
    if (items[selectedSuggestionIndex]) {
      items[selectedSuggestionIndex].classList.add('ri-suggestion-highlighted');
      items[selectedSuggestionIndex].scrollIntoView({ block: 'nearest' });
    }
  }

  //  Input sanitisation + live search 
  // Allow only characters that appear in real invoice numbers:
  // letters (A-Z a-z), digits (0-9), hyphens (-), underscores (_).
  // Anything else is silently stripped on every keystroke.
  // Also clears any inline error/success the moment the user edits
  // and shows live suggestions as they type.
  if (invoiceInput) {
    invoiceInput.addEventListener('input', function () {
      var raw     = this.value;
      var cleaned = raw.replace(/[^A-Za-z0-9\-_]/g, '');
      if (cleaned !== raw) this.value = cleaned;
      clearAllFeedback();

      // Show live suggestions
      showSuggestions(cleaned);
    });
  }

  //  findInvoiceByNumber 
  // Reads reprintInvoiceSampleData from collection_sample.js \u2014
  // that global is defined there and never duplicated here.
  //
  // Returns the matching record, or null (with inline error shown).
  // Structured so handlePrintInvoice() needs zero changes when
  // this becomes an async API call: just swap the array search for
  // a fetch() and make the function async / return a Promise.
  function findInvoiceByNumber(invoiceNo) {
    // 1. Empty / whitespace-only
    if (!invoiceNo || !invoiceNo.trim()) {
      showInputError('Please enter an invoice number.');
      return null;
    }

    var term = invoiceNo.trim().toUpperCase();

    // TODO (backend): replace the array search below with a real API call:
    //   return fetch('/api/Billing/Invoice?invoiceNo=' + encodeURIComponent(term))
    //     .then(function (r) {
    //       if (r.status === 404) return null;
    //       if (!r.ok) throw new Error(r.statusText);
    //       return r.json();
    //     });
    //
    // Also mark findInvoiceByNumber as async (or have it return the Promise)
    // \u2014 handlePrintInvoice() already awaits its result, so no other change needed.
    var data = (typeof reprintInvoiceSampleData !== 'undefined')
      ? reprintInvoiceSampleData
      : [];

    var match = null;
    for (var i = 0; i < data.length; i++) {
      if (data[i].invoiceNo.toUpperCase() === term) {
        match = data[i];
        break;
      }
    }

    // 2. No match
    if (!match) {
      showInputError('Invoice not found.');
      return null;
    }

    return match;
  }

  //  printInvoiceDocument 
  // Stub that simulates the async print/PDF pipeline.
  // Logs the full record so it can be inspected in DevTools.
  //
  // TODO (backend): replace with the real print pipeline, e.g.:
  //   return fetch('/api/Billing/Invoice/Print', {
  //     method: 'POST',
  //     headers: { 'Content-Type': 'application/json' },
  //     body: JSON.stringify({ invoiceNo: invoiceRecord.invoiceNo })
  //   }).then(function (r) {
  //     if (!r.ok) throw new Error(r.statusText);
  //     return r.blob();
  //   }).then(function (blob) {
  //     var url = URL.createObjectURL(blob);
  //     var win = window.open(url);
  //     win.addEventListener('load', function () { win.print(); });
  //     URL.revokeObjectURL(url);
  //   });
  //
  // If the app already has a shared print pipeline (e.g. a printDocument()
  // helper used by other receipt views) wire to that instead.
  function printInvoiceDocument(invoiceRecord) {
    console.log('[Re-Print Invoice] Sending to printer:', invoiceRecord);
    // Simulated delay \u2014 replace with a real async print call above.
    return new Promise(function (resolve) {
      setTimeout(resolve, 800);
    });
  }

  //  Button loading / restore 
  // Mirrors the lockAllExportBtns / restoreExportBtn pattern in
  // initCollectionReportModal \u2014 same names, same shape.
  var PRINT_BTN_ORIGINAL_TEXT = 'Print';

  function lockPrintBtn() {
    if (!printBtn) return;
    printBtn.disabled    = true;
    printBtn.textContent = 'Printing\u2026';
  }

  function unlockPrintBtn() {
    if (!printBtn) return;
    printBtn.disabled    = false;
    printBtn.textContent = PRINT_BTN_ORIGINAL_TEXT;
  }

  //  Success toast 
  // Reuses the page-level toast card \u2014 same pattern as showReportToast
  // (initCollectionReportModal) and showScannerMsg (initScanner).
  function showRiToast(msg, isDanger) {
    var card     = document.getElementById('toast');
    var backdrop = document.getElementById('toastBackdrop');
    var title    = document.getElementById('toastTitle');
    var msgEl    = document.getElementById('toastMsg');
    var iconOk   = document.getElementById('toastIconSuccess');
    var iconErr  = document.getElementById('toastIconDanger');
    if (!card) return;

    title.textContent     = isDanger ? 'Error' : 'Success';
    msgEl.textContent     = msg;
    iconOk.style.display  = isDanger ? 'none'  : 'block';
    iconErr.style.display = isDanger ? 'block' : 'none';
    card.className        = 'toast-card show' + (isDanger ? ' danger' : '');
    backdrop.className    = 'toast-backdrop show';

    clearTimeout(window._riToastTimer);
    window._riToastTimer = setTimeout(function () {
      card.className     = 'toast-card';
      backdrop.className = 'toast-backdrop';
    }, 3000);
  }

  //  handlePrintInvoice 
  // Async so it is already structured for a future API-backed
  // findInvoiceByNumber() and printInvoiceDocument().
  // The modal stays open after a successful print so the user can
  // immediately reprint a different invoice number.
  async function handlePrintInvoice() {
    if (!invoiceInput || _printInFlight) return;

    var trimmed = invoiceInput.value.trim();
    var invoice = await findInvoiceByNumber(trimmed);
    if (!invoice) return;   // error already shown by findInvoiceByNumber

    clearAllFeedback();

    //  Loading state 
    _printInFlight = true;
    lockPrintBtn();

    printInvoiceDocument(invoice)
      .then(function () {
        if (!_printInFlight) return;  // modal was closed mid-print \u2014 discard
        _printInFlight = false;
        unlockPrintBtn();

        // Keep the modal open; show confirmation inline + page-level toast.
        showInputSuccess('\u2714 Invoice sent to printer.');
        showRiToast('Invoice ' + invoice.invoiceNo + ' sent to printer.');
      })
      .catch(function (err) {
        if (!_printInFlight) return;
        _printInFlight = false;
        unlockPrintBtn();

        console.error('[Re-Print Invoice] print error:', err);
        showInputError('Print failed. Please try again.');
        showRiToast('Print failed. Please try again.', true);
      });
  }

  //  Open 
  function openModal() {
    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';

    // Full reset every open — mirrors resetModal() pattern in initBatchPaymentsModal
    if (invoiceInput) invoiceInput.value = '';
    clearAllFeedback();
    hideSuggestions();
    unlockPrintBtn();
    _printInFlight = false;

    // Autofocus the input after the CSS animation settles
    setTimeout(function () {
      if (invoiceInput) invoiceInput.focus();
    }, 200);
  }

  //  Close 
  // Clears the field, all inline feedback, suggestions, and the in-flight guard
  // so the modal reopens completely fresh next time.
  // Mirrors the closeModal() shape in initMyCollectionModal /
  // initCollectionMonitoringModal — no withReset flag needed here
  // because this modal has no multi-field state to preserve.
  function closeModal() {
    // If a print Promise is in flight, mark it abandoned
    if (_printInFlight) {
      _printInFlight = false;
      unlockPrintBtn();
    }
    if (invoiceInput) invoiceInput.value = '';
    clearAllFeedback();
    hideSuggestions();

    overlay.style.display        = 'none';
    document.body.style.overflow = '';
    openBtnNav.focus();
  }

  //  Event listeners 

  // Nav-bar "Reprint Invoice" anchor \u2192 open
  openBtnNav.addEventListener('click', function (e) {
    e.preventDefault();
    openModal();
  });

  // X button \u2192 close
  closeBtn.addEventListener('click', closeModal);

  // Dim overlay click (outside the card) \u2192 close
  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });

  // Escape key \u2192 close (only when this modal is the visible one)
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

  // Print button \u2192 lookup + print
  if (printBtn) {
    printBtn.addEventListener('click', function () {
      handlePrintInvoice();
    });
  }

  // Enter key in the input → select suggestion or trigger print
  // Arrow keys → navigate suggestions dropdown
  if (invoiceInput) {
    invoiceInput.addEventListener('keydown', function (e) {
      var suggestionsEl = overlay.querySelector('.ri-suggestions');
      var suggestionsVisible = suggestionsEl && suggestionsEl.style.display !== 'none';

      if (e.key === 'ArrowDown') {
        if (suggestionsVisible) {
          e.preventDefault();
          navigateSuggestions('down');
        }
        return;
      }

      if (e.key === 'ArrowUp') {
        if (suggestionsVisible) {
          e.preventDefault();
          navigateSuggestions('up');
        }
        return;
      }

      if (e.key === 'Escape') {
        hideSuggestions();
        return;
      }

      if (e.key === 'Enter') {
        e.preventDefault();

        // If a suggestion is highlighted, select it
        if (suggestionsVisible && selectedSuggestionIndex >= 0) {
          var items = suggestionsEl.querySelectorAll('.ri-suggestion-item');
          if (items[selectedSuggestionIndex]) {
            var invoiceNo = items[selectedSuggestionIndex].dataset.invoiceNo;
            invoiceInput.value = invoiceNo;
            hideSuggestions();
          }
        }

        // Always trigger print (either with selected suggestion or typed value)
        handlePrintInvoice();
      }
    });
  }

  // Hide suggestions when clicking outside the input/dropdown
  document.addEventListener('click', function (e) {
    if (!e.target.closest('.ri-input-wrap') &&
        !e.target.closest('.ri-suggestions')) {
      hideSuggestions();
    }
  });

  //  One-time style injection 
  // Keeps ri-* feedback styles self-contained without touching
  // collections.css — mirrors injectColrepStyles / injectErrorStyles.
  (function injectRiStyles() {
    if (document.getElementById('ri-dynamic-styles')) return;
    var style = document.createElement('style');
    style.id  = 'ri-dynamic-styles';
    style.textContent =
      // Inline error
      '.ri-input-error{' +
        'display:none;' +
        'font-size:.73rem;' +
        'color:#dc2626;' +
        'margin-top:5px;' +
        'padding-left:2px;' +
        'font-weight:500;' +
      '}' +
      // Red border on the input when there is an error
      '.ri-input--error{' +
        'border-color:#dc2626 !important;' +
        'box-shadow:0 0 0 3px rgba(220,38,38,.13) !important;' +
      '}' +
      // Inline success
      '.ri-input-success{' +
        'display:none;' +
        'font-size:.73rem;' +
        'color:#16a34a;' +
        'margin-top:5px;' +
        'padding-left:2px;' +
        'font-weight:600;' +
      '}' +
      // Suggestions dropdown
      '.ri-suggestions{' +
        'position:absolute;' +
        'top:100%;' +
        'left:0;' +
        'right:0;' +
        'background:var(--color-panel);' +
        'border:1px solid var(--color-border);' +
        'border-top:none;' +
        'border-radius:0 0 8px 8px;' +
        'box-shadow:0 8px 24px rgba(0,0,0,.18);' +
        'max-height:260px;' +
        'overflow-y:auto;' +
        'z-index:1000;' +
        'margin-top:-1px;' +
      '}' +
      '.ri-suggestion-item{' +
        'padding:10px 14px;' +
        'cursor:pointer;' +
        'border-bottom:1px solid var(--color-border);' +
        'transition:background .12s;' +
      '}' +
      '.ri-suggestion-item:last-child{border-bottom:none;}' +
      '.ri-suggestion-item:hover,' +
      '.ri-suggestion-highlighted{' +
        'background:var(--color-blue-soft);' +
      '}' +
      '.ri-suggestion-invoice{' +
        'font-weight:700;' +
        'font-family:"IBM Plex Mono",monospace;' +
        'color:var(--color-blue);' +
        'font-size:.82rem;' +
        'margin-bottom:2px;' +
      '}' +
      '.ri-suggestion-account{' +
        'font-weight:500;' +
        'font-size:.85rem;' +
        'color:var(--color-text);' +
        'margin-bottom:2px;' +
      '}' +
      '.ri-suggestion-details{' +
        'font-size:.73rem;' +
        'color:var(--color-text-dim);' +
      '}' +
      '.ri-no-suggestions{' +
        'padding:12px 14px;' +
        'color:var(--color-text-dim);' +
        'font-style:italic;' +
        'text-align:center;' +
        'font-size:.85rem;' +
      '}';
    document.head.appendChild(style);
  }());

}());
