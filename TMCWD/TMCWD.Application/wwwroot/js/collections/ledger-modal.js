// ============================================================
//  Ledger Modal  (ldg-*)
//  Reads LEDGER_DATA from collection_sample.js (plain global array).
//  Opens when the user clicks the "Ledger" link-chip on the receipt card,
//  using the account number that is currently loaded in the Collections page.
// ============================================================
(function initLedgerModal() {
  'use strict';

  // ── Data lookup ──────────────────────────────────────────────────────────────
  // TODO: replace with fetch('/api/Billing/Ledger?accountNo=' + encodeURIComponent(accountNo))
  function getLedgerByAccountNo(accountNo) {
    if (!accountNo) return null;
    var data = (typeof LEDGER_DATA !== 'undefined') ? LEDGER_DATA : [];
    var term = String(accountNo).trim();
    for (var i = 0; i < data.length; i++) {
      if (data[i].accountNo === term) return data[i];
    }
    return null;
  }

  // ── DOM refs ─────────────────────────────────────────────────────────────────
  var overlay     = document.getElementById('ledgerOverlay');
  var closeBtn    = document.getElementById('ldgCloseBtn');
  var starBtn     = document.getElementById('ldgStarBtn');
  var menuBtn     = document.getElementById('ldgMenuBtn');
  var historyLink = document.getElementById('ldgHistoryLink');

  var elAccName   = document.getElementById('ldgAccName');
  var elAccNo     = document.getElementById('ldgAccNo');
  var elAddress   = document.getElementById('ldgAddress');
  var elRateCode  = document.getElementById('ldgRateCode');
  var elMeterNo   = document.getElementById('ldgMeterNo');
  var elStatus    = document.getElementById('ldgStatus');
  var elBalance   = document.getElementById('ldgTotalBalance');

  var tabBills    = document.getElementById('ldgTabBills');
  var tabOthers   = document.getElementById('ldgTabOthers');
  var panelBills  = document.getElementById('ldgPanelBills');
  var panelOthers = document.getElementById('ldgPanelOthers');

  var billsBody   = document.getElementById('ldgBillsBody');
  var othersBody  = document.getElementById('ldgOthersBody');
  var billsEmpty  = document.getElementById('ldgBillsEmpty');
  var othersEmpty = document.getElementById('ldgOthersEmpty');

  var rowsSelect  = document.getElementById('ldgRowsPerPage');
  var rangeLabel  = document.getElementById('ldgRangeLabel');
  var firstBtn    = document.getElementById('ldgFirstBtn');
  var prevBtn     = document.getElementById('ldgPrevBtn');
  var nextBtn     = document.getElementById('ldgNextBtn');
  var lastBtn     = document.getElementById('ldgLastBtn');

  if (!overlay) return;

  // ── Module state ─────────────────────────────────────────────────────────────
  var _record      = null;
  var _activeTab   = 'bills';
  var _currentPage = 1;
  var _rowsPerPage = 10;
  var _lastFocus   = null;   // element to restore focus to on close

  // ── Formatters ───────────────────────────────────────────────────────────────
  function fmtPHP(n) {
    if (n === null || n === undefined) return '\u2014';
    return '\u20B1' + Number(n).toLocaleString('en-PH', {
      minimumFractionDigits: 2, maximumFractionDigits: 2
    });
  }

  function fmtNum(n) {
    if (n === null || n === undefined) return '<span class="ldg-cell-null">\u2014</span>';
    return Number(n).toLocaleString('en-PH');
  }

  function escHtml(s) {
    return String(s || '')
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  // ── Header ───────────────────────────────────────────────────────────────────
  function populateHeader(rec) {
    if (elAccName)  elAccName.textContent  = rec.accountName || '\u2014';
    if (elAccNo)    elAccNo.textContent    = rec.accountNo   || '\u2014';
    if (elAddress)  elAddress.textContent  = rec.address     || '\u2014';
    if (elRateCode) elRateCode.textContent = rec.rateCode    || '\u2014';
    if (elMeterNo)  elMeterNo.textContent  = rec.meterNo     || '\u2014';
    if (elBalance)  elBalance.textContent  = fmtPHP(rec.totalBalance);

    if (elStatus) {
      elStatus.textContent = rec.status || '\u2014';
      elStatus.className   = 'ldg-status-badge';
      var s = (rec.status || '').toUpperCase();
      if      (s === 'ACTIVE')       elStatus.classList.add('ldg-status--active');
      else if (s === 'INACTIVE')     elStatus.classList.add('ldg-status--inactive');
      else if (s === 'DISCONNECTED') elStatus.classList.add('ldg-status--disconnected');
    }
  }

  // ── Row builder ──────────────────────────────────────────────────────────────
  function buildRow(entry) {
    var debitTd  = (entry.debit  && entry.debit  > 0)
      ? '<td class="ldg-col-num ldg-cell-debit">'  + fmtPHP(entry.debit)  + '</td>'
      : '<td class="ldg-col-num ldg-cell-null">\u2014</td>';

    var creditTd = (entry.credit && entry.credit > 0)
      ? '<td class="ldg-col-num ldg-cell-credit">' + fmtPHP(entry.credit) + '</td>'
      : '<td class="ldg-col-num ldg-cell-null">\u2014</td>';

    return '<tr>' +
      '<td class="ldg-col-ref">' +
        '<span class="ldg-ref-no">'   + escHtml(entry.refNo)      + '</span>' +
        '<span class="ldg-ref-time">' + escHtml(entry.timestamp)  + '</span>' +
      '</td>' +
      '<td class="ldg-col-particulars">' + escHtml(entry.particulars) + '</td>' +
      '<td class="ldg-col-num">' + fmtNum(entry.prev)  + '</td>' +
      '<td class="ldg-col-num">' + fmtNum(entry.pres)  + '</td>' +
      '<td class="ldg-col-num">' + fmtNum(entry.usage) + '</td>' +
      debitTd + creditTd +
      '<td class="ldg-col-num">' + fmtPHP(entry.balance)      + '</td>' +
      '<td class="ldg-col-by">'  + escHtml(entry.processedBy) + '</td>' +
    '</tr>';
  }

  // ── Render + paginate ─────────────────────────────────────────────────────────
  function renderTable() {
    if (!_record) return;

    var rows  = _activeTab === 'bills' ? (_record.bills || []) : (_record.otherCharges || []);
    var body  = _activeTab === 'bills' ? billsBody  : othersBody;
    var empty = _activeTab === 'bills' ? billsEmpty : othersEmpty;

    if (!body) return;

    // Clear previous dynamic rows — leave the empty-state <tr> in place
    body.querySelectorAll('tr:not(.ldg-empty-row)').forEach(function (r) {
      r.parentNode.removeChild(r);
    });

    var total = rows.length;

    if (total === 0) {
      if (empty) empty.style.display = '';
      updatePagination(0);
      return;
    }

    if (empty) empty.style.display = 'none';

    // Clamp page before slicing
    var maxPage = Math.ceil(total / _rowsPerPage);
    if (_currentPage > maxPage) _currentPage = maxPage;
    if (_currentPage < 1)       _currentPage = 1;

    var start = (_currentPage - 1) * _rowsPerPage;
    var slice = rows.slice(start, Math.min(start + _rowsPerPage, total));

    var html = '';
    slice.forEach(function (entry) { html += buildRow(entry); });

    if (empty) {
      empty.insertAdjacentHTML('beforebegin', html);
    } else {
      body.insertAdjacentHTML('beforeend', html);
    }

    updatePagination(total);
  }

  // ── Pagination state ─────────────────────────────────────────────────────────
  function updatePagination(total) {
    // Pagination controls live only on the Bills panel; skip silently for Others
    if (_activeTab !== 'bills') return;

    var totalPages = total > 0 ? Math.ceil(total / _rowsPerPage) : 1;
    if (_currentPage > totalPages) _currentPage = totalPages;

    var dispStart = total > 0 ? (_currentPage - 1) * _rowsPerPage + 1 : 0;
    var dispEnd   = total > 0 ? Math.min(_currentPage * _rowsPerPage, total) : 0;

    if (rangeLabel) rangeLabel.textContent = dispStart + '\u2013' + dispEnd + ' of ' + total;

    var onFirst = _currentPage <= 1;
    var onLast  = _currentPage >= totalPages || total === 0;

    if (firstBtn) firstBtn.disabled = onFirst;
    if (prevBtn)  prevBtn.disabled  = onFirst;
    if (nextBtn)  nextBtn.disabled  = onLast;
    if (lastBtn)  lastBtn.disabled  = onLast;
  }

  // ── Tab switching ─────────────────────────────────────────────────────────────
  function switchTab(tab) {
    _activeTab   = tab;
    _currentPage = 1;

    var isBills = (tab === 'bills');

    if (tabBills) {
      tabBills.classList.toggle('ldg-tab--active', isBills);
      tabBills.setAttribute('aria-selected', String(isBills));
    }
    if (tabOthers) {
      tabOthers.classList.toggle('ldg-tab--active', !isBills);
      tabOthers.setAttribute('aria-selected', String(!isBills));
    }

    if (panelBills)  panelBills.style.display  = isBills  ? '' : 'none';
    if (panelOthers) panelOthers.style.display = !isBills ? '' : 'none';

    // Reset pagination label and buttons while tab is Bills but before render
    if (isBills && rangeLabel) rangeLabel.textContent = '0\u20130 of 0';

    renderTable();
  }

  // ── Reset all visible state ───────────────────────────────────────────────────
  // Called on close so reopening a different account is always clean.
  function resetState() {
    _record      = null;
    _activeTab   = 'bills';
    _currentPage = 1;
    _rowsPerPage = rowsSelect ? (parseInt(rowsSelect.value, 10) || 10) : 10;

    // Clear table bodies
    [billsBody, othersBody].forEach(function (body) {
      if (!body) return;
      body.querySelectorAll('tr:not(.ldg-empty-row)').forEach(function (r) {
        r.parentNode.removeChild(r);
      });
    });

    // Show empty-state rows
    if (billsEmpty)  billsEmpty.style.display  = '';
    if (othersEmpty) othersEmpty.style.display = '';

    // Reset header fields to dashes
    [elAccName, elAccNo, elAddress, elRateCode, elMeterNo].forEach(function (el) {
      if (el) el.textContent = '\u2014';
    });
    if (elBalance) elBalance.textContent = '\u20B10.00';
    if (elStatus)  { elStatus.textContent = '\u2014'; elStatus.className = 'ldg-status-badge'; }

    // Reset pagination display
    if (rangeLabel) rangeLabel.textContent = '0\u20130 of 0';
    [firstBtn, prevBtn, nextBtn, lastBtn].forEach(function (b) { if (b) b.disabled = true; });

    // Reset rows-per-page selector to default
    if (rowsSelect) rowsSelect.value = '10';
    _rowsPerPage = 10;

    // Reset tabs to Bills
    if (tabBills)  { tabBills.classList.add('ldg-tab--active');    tabBills.setAttribute('aria-selected', 'true'); }
    if (tabOthers) { tabOthers.classList.remove('ldg-tab--active'); tabOthers.setAttribute('aria-selected', 'false'); }
    if (panelBills)  panelBills.style.display  = '';
    if (panelOthers) panelOthers.style.display = 'none';

    // Reset star button
    if (starBtn) starBtn.classList.remove('ldg-star--active');
  }

  // ── Open ─────────────────────────────────────────────────────────────────────
  function openModal(accountNo) {
    var rec = getLedgerByAccountNo(accountNo);

    if (!rec) {
      var toastCard = document.getElementById('toast');
      var backdrop  = document.getElementById('toastBackdrop');
      if (toastCard) {
        document.getElementById('toastTitle').textContent          = 'Ledger';
        document.getElementById('toastMsg').textContent            =
          'No ledger data found for account ' + (accountNo || '\u2014') + '.';
        document.getElementById('toastIconSuccess').style.display  = 'none';
        document.getElementById('toastIconDanger').style.display   = 'block';
        toastCard.className = 'toast-card show danger';
        backdrop.className  = 'toast-backdrop show';
        clearTimeout(window._ldgToastTimer);
        window._ldgToastTimer = setTimeout(function () {
          toastCard.className = 'toast-card';
          backdrop.className  = 'toast-backdrop';
        }, 3000);
      }
      return;
    }

    // Always start clean so switching accounts leaves no stale data
    resetState();

    _record      = rec;
    _activeTab   = 'bills';
    _currentPage = 1;

    populateHeader(rec);
    switchTab('bills');

    // Remember what had focus so we can restore it on close
    _lastFocus = document.activeElement;

    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';

    // Move focus into the modal — close button is the logical first target
    setTimeout(function () {
      if (closeBtn) closeBtn.focus();
    }, 80);
  }

  // ── Close ────────────────────────────────────────────────────────────────────
  function closeModal() {
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
    resetState();

    // Restore focus to wherever it was before the modal opened
    if (_lastFocus && typeof _lastFocus.focus === 'function') {
      _lastFocus.focus();
      _lastFocus = null;
    }
  }

  // ── Event wiring ─────────────────────────────────────────────────────────────

  // Close paths: button / backdrop / Escape
  if (closeBtn) closeBtn.addEventListener('click', closeModal);

  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') {
      e.stopPropagation();
      closeModal();
    }
  });

  // Tabs
  if (tabBills)  tabBills.addEventListener('click',  function () { switchTab('bills');  });
  if (tabOthers) tabOthers.addEventListener('click', function () { switchTab('others'); });

  // Rows-per-page
  if (rowsSelect) {
    rowsSelect.addEventListener('change', function () {
      _rowsPerPage = parseInt(this.value, 10) || 10;
      _currentPage = 1;
      renderTable();
    });
  }

  // Pagination buttons — all recalculate totalPages from live data
  function totalPages() {
    if (!_record) return 1;
    var rows = _activeTab === 'bills' ? (_record.bills || []) : (_record.otherCharges || []);
    return Math.max(1, Math.ceil(rows.length / _rowsPerPage));
  }

  if (firstBtn) firstBtn.addEventListener('click', function () {
    if (_currentPage !== 1) { _currentPage = 1; renderTable(); }
  });
  if (prevBtn)  prevBtn.addEventListener('click', function () {
    if (_currentPage > 1) { _currentPage--; renderTable(); }
  });
  if (nextBtn)  nextBtn.addEventListener('click', function () {
    var tp = totalPages();
    if (_currentPage < tp) { _currentPage++; renderTable(); }
  });
  if (lastBtn)  lastBtn.addEventListener('click', function () {
    var tp = totalPages();
    if (_currentPage !== tp) { _currentPage = tp; renderTable(); }
  });

  // Star toggle
  if (starBtn) starBtn.addEventListener('click', function () {
    this.classList.toggle('ldg-star--active');
  });

  // 3-dot menu stub
  if (menuBtn) menuBtn.addEventListener('click', function () {
    if (_record) console.log('[Ledger] menu for', _record.accountNo);
  });

  // History link stub
  if (historyLink) historyLink.addEventListener('click', function (e) {
    e.preventDefault();
    if (_record) console.log('[Ledger] history for', _record.accountNo);
  });

  // Action button stubs
  overlay.querySelectorAll('.ldg-action-btn[data-action]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      console.log('[Ledger]', this.dataset.action, 'for', _record ? _record.accountNo : '(none)');
    });
  });

  // ── "Ledger" link-chip → open ─────────────────────────────────────────────────
  // Capture phase intercepts before collections.js reaches the Coming Soon fallback.
  document.querySelectorAll('.link-chip[data-modal="ledger"]').forEach(function (chip) {
    chip.addEventListener('click', function (e) {
      e.stopImmediatePropagation();

      var accNoEl   = document.getElementById('fAccNo');
      var accountNo = (accNoEl && accNoEl.textContent.trim() !== '\u2014' && accNoEl.textContent.trim() !== '')
        ? accNoEl.textContent.trim()
        : (window._ocCurrentAccount ? window._ocCurrentAccount.accountNumber : null);

      openModal(accountNo);
    }, true);
  });

  // Public opener (callable from other modules)
  window.openLedgerModal = openModal;

}());
