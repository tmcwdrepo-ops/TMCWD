// ============================================================
//  Collection Report Modal \u2014 final wiring & cleanup
//
//  Naming conventions match the Collection Remittance modal
//  (initRemittanceModal / initRemittanceFields / etc.) that
//  precedes this block in the file.
// ============================================================
(function initCollectionReportModal() {
  'use strict';

  //  DOM refs 
  const overlay   = document.getElementById('colrepOverlay');
  const openBtn   = document.getElementById('collectionReportBtn');
  const closeBtn  = document.getElementById('colrepCloseBtn');
  const fromInput = document.getElementById('crFromDate');
  const toInput   = document.getElementById('crToDate');
  const collector = document.getElementById('crReportCollector');
  const radioOR   = document.getElementById('sortByOR');
  const radioAcct = document.getElementById('sortByAccountNo');
  const pdfBtn    = document.getElementById('generatePdfBtn');
  const excelBtn  = document.getElementById('generateExcelBtn');

  // Guard: nothing to wire if the modal markup is absent.
  if (!overlay || !openBtn || !closeBtn) return;

  // TODO: replace with fetch('/api/Billing/Collectors').then(r => r.json())
  const REPORT_COLLECTORS = (typeof SAMPLE_REPORT_COLLECTORS !== 'undefined')
    ? SAMPLE_REPORT_COLLECTORS
    : [];

  //  Central state object 
  // Single source of truth \u2014 all field listeners write here;
  // both export functions read from here at click time.
  window.reportState = {
    fromDate:  '',   // ISO 'YYYY-MM-DD'
    toDate:    '',   // ISO 'YYYY-MM-DD'
    collector: '',   // '' = All Collectors
    sortBy:    'OR'  // 'OR' | 'accountNo'
  };

  //  In-flight guard 
  // closeModal() checks this to abort cleanly mid-export.
  var _exportInFlight = false;

  // Helpers: today 
  function todayISO() {
    return new Date().toISOString().slice(0, 10);
  }

  //  To-date inline error 
  function ensureToDateError() {
    var el = document.getElementById('crToDateError');
    if (el) return el;
    el = document.createElement('span');
    el.id        = 'crToDateError';
    el.className = 'colrep-field-error';
    el.setAttribute('role', 'alert');
    toInput.parentElement.appendChild(el);
    return el;
  }
  function showToDateError(msg) {
    var el = ensureToDateError();
    el.textContent = msg;
    el.style.display = 'block';
    toInput.classList.add('colrep-input--error');
  }
  function clearToDateError() {
    var el = ensureToDateError();
    el.style.display = 'none';
    toInput.classList.remove('colrep-input--error');
  }

  //  Export-area inline error 
  function showExportError(msg) {
    var el = document.getElementById('colrepExportError');
    if (!el) {
      el = document.createElement('p');
      el.id        = 'colrepExportError';
      el.className = 'colrep-export-error';
      el.setAttribute('role', 'alert');
      var footer = overlay.querySelector('.colrep-footer');
      if (footer) footer.insertAdjacentElement('afterend', el);
    }
    el.textContent   = msg;
    el.style.display = 'block';
  }
  function clearExportError() {
    var el = document.getElementById('colrepExportError');
    if (el) el.style.display = 'none';
  }

  //  Collector dropdown 
  function populateCollectors() {
    if (!collector) return;
    collector.innerHTML = '';
    var allOpt = document.createElement('option');
    allOpt.value       = '';
    allOpt.textContent = 'All Collectors';
    collector.appendChild(allOpt);
    REPORT_COLLECTORS.forEach(function (name) {
      var opt = document.createElement('option');
      opt.value       = name;
      opt.textContent = name;
      collector.appendChild(opt);
    });
    collector.value              = '';
    window.reportState.collector = '';
  }

  //  Date seeding 
  function seedDates() {
    var today = todayISO();
    fromInput.value             = today;
    toInput.value               = today;
    window.reportState.fromDate = today;
    window.reportState.toDate   = today;
    clearToDateError();
  }

  //  Radio label sync 
  function syncRadioLabels() {
    [radioOR, radioAcct].forEach(function (r) {
      if (!r) return;
      r.closest('.colrep-radio-label').classList.toggle(
        'colrep-radio-label--selected', r.checked
      );
    });
  }

  //  Full state reset 
  // Restores every field and reportState to defaults.
  // Called on open and on close (unless withReset === false).
  function resetState() {
    seedDates();
    populateCollectors();
    if (radioOR)   radioOR.checked   = true;
    if (radioAcct) radioAcct.checked = false;
    window.reportState.sortBy = 'OR';
    syncRadioLabels();
    clearToDateError();
    clearExportError();
  }

  //  Button loading / restore 
  var PDF_ICON_HTML =
    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"' +
    ' width="15" height="15" aria-hidden="true">' +
      '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>' +
      '<polyline points="14 2 14 8 20 8"/>' +
      '<line x1="9" y1="13" x2="15" y2="13"/>' +
      '<line x1="9" y1="17" x2="12" y2="17"/>' +
    '</svg>';

  var EXCEL_ICON_HTML =
    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"' +
    ' width="15" height="15" aria-hidden="true">' +
      '<rect x="3" y="3" width="18" height="18" rx="2"/>' +
      '<path d="M3 9h18M3 15h18M9 3v18"/>' +
    '</svg>';

  var SPINNER_HTML =
    '<svg class="colrep-spinner" viewBox="0 0 24 24" fill="none"' +
    ' stroke="currentColor" stroke-width="2.5" width="15" height="15" aria-hidden="true">' +
      '<path d="M12 2a10 10 0 0 1 10 10" stroke-linecap="round"/>' +
    '</svg>';

  // setExportBtnLoading only swaps the label; disabled is set by lockAllExportBtns
  // so both buttons are always locked/unlocked as a pair.
  function setExportBtnLoading(btn, label) {
    btn.dataset.originalLabel = btn.textContent.trim();
    btn.innerHTML = SPINNER_HTML + label;
  }
  function restoreExportBtn(btn, iconHTML) {
    btn.innerHTML = iconHTML + (btn.dataset.originalLabel || '');
  }

  // Lock / unlock both buttons together \u2014 prevents double-triggering
  // while either export is in flight.
  function lockAllExportBtns() {
    if (pdfBtn)   pdfBtn.disabled   = true;
    if (excelBtn) excelBtn.disabled = true;
  }
  function unlockAllExportBtns() {
    if (pdfBtn)   pdfBtn.disabled   = false;
    if (excelBtn) excelBtn.disabled = false;
  }

  //  Open 
  function openModal() {
    resetState();
    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';
    closeBtn.focus();
  }

  //  Close 
  // withReset defaults to true.
  // Pass false after a successful export so field values persist
  // for a possible immediate re-export in a different format.
  function closeModal(withReset) {
    // If a Promise is in flight, mark it abandoned so its .then()
    // becomes a no-op, then immediately restore button states.
    if (_exportInFlight) {
      _exportInFlight = false;
      unlockAllExportBtns();
      if (pdfBtn)   restoreExportBtn(pdfBtn,   PDF_ICON_HTML);
      if (excelBtn) restoreExportBtn(excelBtn, EXCEL_ICON_HTML);
    }
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
    if (withReset !== false) resetState();
    openBtn.focus();
  }

  //  Validation 
  function validateDateRange() {
    // Both dates must be present \u2014 no silent pass-through on empty values.
    if (!fromInput.value || !toInput.value) {
      showToDateError('Both From and To dates are required.');
      return false;
    }
    if (toInput.value < fromInput.value) {
      showToDateError('"To" date cannot be earlier than "From" date.');
      return false;
    }
    clearToDateError();
    return true;
  }

  // Shared pre-export validator \u2014 extend here for future validation rules.
  function validateReportState() {
    return validateDateRange();
  }

  //  Field event wiring 
  // Every listener writes to window.reportState so export functions
  // always read the latest user input \u2014 nothing is ever stale.

  fromInput.addEventListener('change', function () {
    window.reportState.fromDate = this.value;
    if (toInput.value) validateDateRange();   // live range check
  });
  toInput.addEventListener('change', function () {
    window.reportState.toDate = this.value;
    validateDateRange();
  });
  toInput.addEventListener('input', clearToDateError);   // clear error on edit

  if (collector) {
    collector.addEventListener('change', function () {
      window.reportState.collector = this.value;
    });
  }

  [radioOR, radioAcct].forEach(function (radio) {
    if (!radio) return;
    radio.addEventListener('change', function () {
      window.reportState.sortBy = (this.value === 'or') ? 'OR' : 'accountNo';
      syncRadioLabels();
    });
  });

  //  Open / close event listeners 
  openBtn.addEventListener('click', function (e) {
    e.preventDefault();
    openModal();
  });
  closeBtn.addEventListener('click', function () { closeModal(); });
  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

  //  PDF export 
  // TODO (backend): replace stub with a real fetch/POST + blob download:
  //
  //   function generateCollectionReportPDF(state) {
  //     return fetch('/api/Billing/CollectionReport/PDF', {
  //       method: 'POST', headers: { 'Content-Type': 'application/json' },
  //       body: JSON.stringify(state)
  //     }).then(function (r) {
  //       if (!r.ok) throw new Error(r.statusText);
  //       return r.blob();
  //     }).then(function (blob) {
  //       var url = URL.createObjectURL(blob);
  //       var a = document.createElement('a');
  //       a.href = url; a.download = 'collection-report.pdf'; a.click();
  //       URL.revokeObjectURL(url);
  //     });
  //   }
  function generateCollectionReportPDF(state) {
    console.log('[CollectionReport] PDF requested \u2014 state:', JSON.stringify(state));
    return new Promise(function (resolve) { setTimeout(resolve, 1200); });
  }

  if (pdfBtn) {
    pdfBtn.addEventListener('click', function () {
      clearExportError();
      if (!validateReportState()) return;

      _exportInFlight = true;
      lockAllExportBtns();
      setExportBtnLoading(pdfBtn, ' Generating\u2026');

      generateCollectionReportPDF(window.reportState)
        .then(function () {
          if (!_exportInFlight) return;   // modal closed mid-export \u2014 discard
          _exportInFlight = false;
          closeModal(false);              // keep fields for possible re-export
          showReportToast('Collection Report PDF generated successfully.');
        })
        .catch(function (err) {
          if (!_exportInFlight) return;
          _exportInFlight = false;
          console.error('[CollectionReport] PDF error:', err);
          showExportError('PDF generation failed. Please try again.');
        })
        .finally(function () {
          // Idempotent \u2014 safe even if the modal is already closed.
          unlockAllExportBtns();
          restoreExportBtn(pdfBtn, PDF_ICON_HTML);
        });
    });
  }

  // Excel export 
  // TODO (backend): replace stub with a real fetch/POST + blob download:
  //
  //   function generateCollectionReportExcel(state) {
  //     return fetch('/api/Billing/CollectionReport/Excel', {
  //       method: 'POST', headers: { 'Content-Type': 'application/json' },
  //       body: JSON.stringify(state)
  //     }).then(function (r) {
  //       if (!r.ok) throw new Error(r.statusText);
  //       return r.blob();
  //     }).then(function (blob) {
  //       var url = URL.createObjectURL(blob);
  //       var a = document.createElement('a');
  //       a.href = url; a.download = 'collection-report.xlsx'; a.click();
  //       URL.revokeObjectURL(url);
  //     });
  //   }
  function generateCollectionReportExcel(state) {
    console.log('[CollectionReport] Excel requested \u2014 state:', JSON.stringify(state));
    return new Promise(function (resolve) { setTimeout(resolve, 1200); });
  }

  if (excelBtn) {
    excelBtn.addEventListener('click', function () {
      clearExportError();
      if (!validateReportState()) return;

      _exportInFlight = true;
      lockAllExportBtns();
      setExportBtnLoading(excelBtn, ' Generating\u2026');

      generateCollectionReportExcel(window.reportState)
        .then(function () {
          if (!_exportInFlight) return;
          _exportInFlight = false;
          closeModal(false);
          showReportToast('Collection Report Excel generated successfully.');
        })
        .catch(function (err) {
          if (!_exportInFlight) return;
          _exportInFlight = false;
          console.error('[CollectionReport] Excel error:', err);
          showExportError('Excel generation failed. Please try again.');
        })
        .finally(function () {
          unlockAllExportBtns();
          restoreExportBtn(excelBtn, EXCEL_ICON_HTML);
        });
    });
  }

  //  Success toast 
  // Reuses the page-level toast card \u2014 same pattern as
  // initRemittanceSave and showScannerMsg elsewhere in this file.
  function showReportToast(msg) {
    var card     = document.getElementById('toast');
    var backdrop = document.getElementById('toastBackdrop');
    var title    = document.getElementById('toastTitle');
    var msgEl    = document.getElementById('toastMsg');
    var iconOk   = document.getElementById('toastIconSuccess');
    var iconErr  = document.getElementById('toastIconDanger');
    if (!card) return;
    title.textContent     = 'Success';
    msgEl.textContent     = msg;
    iconOk.style.display  = 'block';
    iconErr.style.display = 'none';
    card.className        = 'toast-card show';
    backdrop.className    = 'toast-backdrop show';
    clearTimeout(window._colrepToastTimer);
    window._colrepToastTimer = setTimeout(function () {
      card.className     = 'toast-card';
      backdrop.className = 'toast-backdrop';
    }, 3000);
  }

  //  One-time style injection 
  // Mirrors initRemittanceSave's injectErrorStyles() \u2014 keeps the
  // modal's error styles self-contained without a separate CSS file.
  (function injectColrepStyles() {
    if (document.getElementById('colrep-dynamic-styles')) return;
    var style = document.createElement('style');
    style.id = 'colrep-dynamic-styles';
    style.textContent =
      '.colrep-field-error{display:none;font-size:.73rem;color:#dc2626;margin-top:3px;}' +
      '.colrep-input--error{border-color:#dc2626 !important;' +
        'box-shadow:0 0 0 3px rgba(220,38,38,.12) !important;}';
    document.head.appendChild(style);
  }());

  // Initial setup 
  populateCollectors();
  seedDates();
  syncRadioLabels();

}());