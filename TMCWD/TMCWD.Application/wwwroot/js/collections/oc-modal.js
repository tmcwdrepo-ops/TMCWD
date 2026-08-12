// ============================================================
//  Other Charges Modal  (oc-*)
// ============================================================
(function initOtherChargesModal() {
  'use strict';

  //  DOM refs 
  var infoBtn          = document.getElementById('ocInfoBtn');
  var overlay          = document.getElementById('ocOverlay');
  var modalCard        = overlay ? overlay.querySelector('.oc-receipt-modal') : null;
  var closeBtn         = document.getElementById('ocModalClose');
  var tableBody        = document.getElementById('ocTableBody');
  var totalEl          = document.getElementById('ocTotalDue');
  var subtitleEl       = document.getElementById('ocReceiptSubtitle');
  var accNoInput       = document.getElementById('ocAccNo');
  var accNameInput     = document.getElementById('ocAccName');
  var tinInput         = document.getElementById('ocTIN');
  var addressInput     = document.getElementById('ocAddress');
  var suggestionsEl    = document.getElementById('ocAccSuggestions');
  var chargeTypeSelect = document.getElementById('ocChargeType');
  var amountInput      = document.getElementById('ocAmount');
  var addBtn           = document.getElementById('ocAddBtn');
  var payToggle        = document.getElementById('ocPayToggle');
  var tenderedInput    = document.getElementById('ocAmountTendered');
  var remainingBlockEl = document.getElementById('ocRemainingBlock');
  var remainingLabelEl = document.getElementById('ocRemainingLabel');
  var remainingValEl   = document.getElementById('ocRemainingVal');
  var payBtn           = document.getElementById('ocPayBtn');

  if (!infoBtn || !overlay || !modalCard) return;

  //  Module state 
  var _ocPayMethod   = 'cash';
  var _suggIdx       = -1;
  var _ocSearchTimer = null;

  // Others view state
  var _ocOthPayMethod  = 'cash';
  var _ocOthCharges    = [];   // rows added in Others view

  window._ocCurrentAccount = null;

  //  Formatting helper 
  function fmtPHP(n) {
    return '\u20B1' + Number(n).toLocaleString('en-PH', {
      minimumFractionDigits: 2, maximumFractionDigits: 2
    });
  }

  //  Phase 1: Populate charge type dropdown 
  function populateChargeTypeDropdown() {
    if (!chargeTypeSelect || chargeTypeSelect.dataset.populated === '1') return;
    var data = (typeof OC_CHARGE_TYPES !== 'undefined') ? OC_CHARGE_TYPES : [];
    data.forEach(function (ct) {
      var opt = document.createElement('option');
      opt.value = ct.id; opt.textContent = ct.label;
      opt.dataset.defaultAmount = ct.defaultAmount;
      chargeTypeSelect.appendChild(opt);
    });
    chargeTypeSelect.dataset.populated = '1';
  }

  //  Phase 1: Fill / clear account fields 
  function fillAccountFields(acc) {
    var e = '\u2014';
    if (accNoInput)   accNoInput.value   = acc ? (acc.accountNumber || e) : e;
    if (accNameInput) accNameInput.value = acc ? (acc.name          || e) : e;
    if (tinInput)     tinInput.value     = acc ? (acc.tin           || e) : e;
    if (addressInput) addressInput.value = acc ? (acc.address       || e) : e;
  }

  //  Phase 1: Render charges table 
  function renderTable() {
    if (!tableBody) return;
    var acc     = window._ocCurrentAccount;
    var charges = (acc && Array.isArray(acc.otherCharges)) ? acc.otherCharges : [];
    renderTableFromRows(charges);
  }

  //  Phase 4: Render from explicit rows array 
  function renderTableFromRows(rows) {
    if (!tableBody) return;
    if (!rows || rows.length === 0) {
      tableBody.innerHTML =
        '<tr><td colspan="2" class="oc-receipt-empty">No other charges on record.</td></tr>';
      if (totalEl) totalEl.textContent = fmtPHP(0);
      return;
    }
    var grand = 0;
    tableBody.innerHTML = rows.map(function (c) {
      var amt = c.amount || 0; grand += amt;
      return '<tr><td>' + (c.chargeType || '\u2014') + '</td>' +
             '<td class="oc-receipt-col-amount">' + fmtPHP(amt) + '</td></tr>';
    }).join('');
    if (totalEl) totalEl.textContent = fmtPHP(grand);
  }

  //  Phase 3: searchAccount 
  // TODO: replace body with fetch('/api/Billing/AccountLookup?q='+term)
  function searchAccount(term) {
    var t = term.trim();
    if (!t) return Promise.resolve([]);

    return fetch('/api/Billing/AccountLookup?q=' + encodeURIComponent(t))
      .then(function(res) {
        if (!res.ok) throw new Error('Network response was not ok');
        return res.json();
      })
      .catch(function(err) {
        console.error('Account lookup error:', err);
        return [];
      });
  }

  //  Phase 3: Suggestion helpers 
  function hideSuggestions() {
    if (suggestionsEl) suggestionsEl.style.display = 'none';
    _suggIdx = -1;
  }

  function showSuggestions(matches) {
    if (!suggestionsEl) return;
    if (matches.length === 0) {
      suggestionsEl.innerHTML = '<div class="oc-sugg-empty">No accounts found</div>';
      suggestionsEl.style.display = 'block'; _suggIdx = -1; return;
    }
    suggestionsEl.innerHTML = matches.map(function (r, i) {
      return '<div class="oc-sugg-item" data-idx="' + i + '">' +
        '<div class="oc-sugg-accno">' + r.accountNumber + '</div>' +
        '<div class="oc-sugg-name">'  + r.name + '</div>' +
        '<div class="oc-sugg-meta">'  + (r.tin ? 'TIN: ' + r.tin + ' \u2022 ' : '') + (r.address || '') + '</div>' +
      '</div>';
    }).join('');
    suggestionsEl.style.display = 'block';
    suggestionsEl._matches = matches; _suggIdx = -1;
    suggestionsEl.querySelectorAll('.oc-sugg-item').forEach(function (item) {
      item.addEventListener('mousedown', function (e) {
        e.preventDefault();
        var rec = suggestionsEl._matches && suggestionsEl._matches[parseInt(this.dataset.idx, 10)];
        if (rec) selectSuggestion(rec);
      });
    });
  }

  function navigateSuggestions(dir) {
    if (!suggestionsEl || suggestionsEl.style.display === 'none') return;
    var items = suggestionsEl.querySelectorAll('.oc-sugg-item');
    if (!items.length) return;
    if (_suggIdx >= 0) items[_suggIdx].classList.remove('oc-sugg-highlighted');
    _suggIdx = dir === 'down'
      ? (_suggIdx + 1) % items.length
      : (_suggIdx <= 0 ? items.length - 1 : _suggIdx - 1);
    items[_suggIdx].classList.add('oc-sugg-highlighted');
    items[_suggIdx].scrollIntoView({ block: 'nearest' });
  }

  function selectSuggestion(rec) {
    window._ocCurrentAccount = rec;
    fillAccountFields(rec);
    renderTable();
    hideSuggestions();
    if (accNoInput) accNoInput.value = rec.accountNumber;
    calculateTotals();
  }

  //  Phase 3 listeners 
  if (accNoInput) {
    accNoInput.addEventListener('input', function () {
      var val = this.value.trim();
      clearTimeout(_ocSearchTimer);
      if (!val) { hideSuggestions(); return; }
      _ocSearchTimer = setTimeout(function () {
        searchAccount(val).then(function(matches) {
          showSuggestions(matches);
        });
      }, 300);
    });

    accNoInput.addEventListener('keydown', function (e) {
      var visible = suggestionsEl && suggestionsEl.style.display !== 'none';
      if (e.key === 'ArrowDown') { e.preventDefault(); navigateSuggestions('down'); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); navigateSuggestions('up'); }
      else if (e.key === 'Escape') { hideSuggestions(); }
      else if (e.key === 'Enter') {
        e.preventDefault();
        if (visible && _suggIdx >= 0 && suggestionsEl._matches && suggestionsEl._matches[_suggIdx])
          selectSuggestion(suggestionsEl._matches[_suggIdx]);
        else if (visible && suggestionsEl._matches && suggestionsEl._matches.length)
          selectSuggestion(suggestionsEl._matches[0]);
      }
    });

    accNoInput.addEventListener('blur', function () { setTimeout(hideSuggestions, 150); });
  }

  //  Phase 4: Inline-error helpers 
  function _getAddRowError() {
    var el = modalCard.querySelector('.oc-add-row-error');
    if (el) return el;
    var span = document.createElement('span');
    span.className = 'oc-add-row-error';
    var row = modalCard.querySelector('.oc-receipt-add-row');
    if (row && row.parentNode) row.parentNode.insertBefore(span, row.nextSibling);
    return span;
  }
  function showAddError(msg) {
    var el = _getAddRowError();
    el.textContent = msg; el.style.display = 'block';
    clearTimeout(el._hideTimer);
    el._hideTimer = setTimeout(function () { el.style.display = 'none'; }, 3000);
  }
  function clearAddError() {
    var el = modalCard.querySelector('.oc-add-row-error');
    if (el) { el.style.display = 'none'; clearTimeout(el._hideTimer); }
  }

  // Inject styles once
  (function () {
    if (document.getElementById('oc-add-row-error-style')) return;
    var s = document.createElement('style');
    s.id = 'oc-add-row-error-style';
    s.textContent =
      '.oc-add-row-error{display:none;font-size:.73rem;font-weight:600;' +
        'color:var(--color-red,#ff453a);padding:4px 16px 6px;' +
        'background:rgba(255,69,58,.08);border-bottom:1px solid var(--color-border);}' +
      '.oc-receipt-input--invalid{border-color:var(--color-red,#ff453a) !important;' +
        'box-shadow:0 0 0 3px rgba(255,69,58,.13) !important;}' +
      '.oc-receipt-select--invalid{border-color:var(--color-red,#ff453a) !important;' +
        'box-shadow:0 0 0 3px rgba(255,69,58,.13) !important;}';
    document.head.appendChild(s);
  }());

  //  Phase 4: addChargeLine 
  function addChargeLine(chargeTypeId, amount) {
    clearAddError();
    var hasType  = chargeTypeId && chargeTypeId.trim() !== '';
    var validAmt = typeof amount === 'number' && isFinite(amount) && amount > 0;

    if (!hasType) {
      if (chargeTypeSelect) chargeTypeSelect.classList.add('oc-receipt-select--invalid');
      showAddError('Please select a Charge Type.');
      if (chargeTypeSelect) chargeTypeSelect.focus();
      return false;
    }
    if (!validAmt) {
      if (amountInput) amountInput.classList.add('oc-receipt-input--invalid');
      showAddError('Amount must be greater than 0.');
      if (amountInput) amountInput.focus();
      return false;
    }

    var types  = (typeof OC_CHARGE_TYPES !== 'undefined') ? OC_CHARGE_TYPES : [];
    var rec    = types.find(function (ct) { return ct.id === chargeTypeId; });
    var row    = { chargeType: rec ? rec.label : chargeTypeId,
                   chargeTypeId: chargeTypeId, amount: amount };

    // Lookup mode — must have an account loaded
    var acc = window._ocCurrentAccount;
    if (!acc) {
      showAddError('No account selected. Search for an account first.');
      return false;
    }
    if (!Array.isArray(acc.otherCharges)) acc.otherCharges = [];
    acc.otherCharges.push(row);

    var newBal = acc.otherCharges.reduce(function (s, c) { return s + (c.amount || 0); }, 0);
    acc.otherChargesBalance = newBal;
    var badge = document.getElementById('fOtherCharges');
    if (badge) badge.textContent = fmtPHP(newBal);

    renderTable();

    if (chargeTypeSelect) chargeTypeSelect.value = '';
    if (amountInput)      amountInput.value      = '';
    if (chargeTypeSelect) chargeTypeSelect.focus();
    return true;
  }

  if (chargeTypeSelect) {
    chargeTypeSelect.addEventListener('change', function () {
      this.classList.remove('oc-receipt-select--invalid'); clearAddError();
    });
  }
  if (amountInput) {
    amountInput.addEventListener('input', function () {
      this.classList.remove('oc-receipt-input--invalid'); clearAddError();
    });
  }

  //  Phase 2: setPaymentMethod 
  function setPaymentMethod(method) {
    if (!payToggle) return;
    _ocPayMethod = method;
    payToggle.querySelectorAll('.oc-receipt-pay-opt').forEach(function (btn) {
      var on = btn.dataset.pay === method;
      btn.classList.toggle('oc-receipt-pay-opt--active', on);
      btn.setAttribute('aria-checked', on ? 'true' : 'false');
    });
  }
  if (payToggle) {
    payToggle.addEventListener('click', function (e) {
      var btn = e.target.closest('.oc-receipt-pay-opt');
      if (btn) setPaymentMethod(btn.dataset.pay);
    });
  }

  //  Phase 5: calculateTotals 
  function calculateTotals() {
    var totalDue = 0;
    if (tableBody) {
      tableBody.querySelectorAll('.oc-receipt-col-amount').forEach(function (cell) {
        var v = parseFloat(cell.textContent.replace(/[₱,\s]/g, ''));
        if (!isNaN(v)) totalDue += v;
      });
    }
    if (totalEl) totalEl.textContent = fmtPHP(totalDue);

    if (!remainingBlockEl || !remainingLabelEl || !remainingValEl) return;
    var tendered    = tenderedInput ? parseFloat(tenderedInput.value) : NaN;
    var hasTendered = !isNaN(tendered) && tendered > 0;

    if (!hasTendered) {
      remainingBlockEl.classList.remove('oc-remaining--ok', 'oc-remaining--short');
      remainingLabelEl.textContent = 'Remaining';
      remainingValEl.textContent   = fmtPHP(0);
      updatePayBtn(); return;
    }
    var diff = tendered - totalDue;
    if (diff >= 0) {
      remainingBlockEl.classList.replace
        ? remainingBlockEl.classList.replace('oc-remaining--short', 'oc-remaining--ok')
        : (remainingBlockEl.classList.remove('oc-remaining--short'),
           remainingBlockEl.classList.add('oc-remaining--ok'));
      remainingLabelEl.textContent = 'Change';
      remainingValEl.textContent   = fmtPHP(diff);
    } else {
      remainingBlockEl.classList.replace
        ? remainingBlockEl.classList.replace('oc-remaining--ok', 'oc-remaining--short')
        : (remainingBlockEl.classList.remove('oc-remaining--ok'),
           remainingBlockEl.classList.add('oc-remaining--short'));
      remainingLabelEl.textContent = 'Short By';
      remainingValEl.textContent   = fmtPHP(Math.abs(diff));
    }
    updatePayBtn();
  }

  // Phase 5 trigger: row added
  if (addBtn) {
    addBtn.addEventListener('click', function () {
      var typeId = chargeTypeSelect ? chargeTypeSelect.value        : '';
      var amount = amountInput      ? parseFloat(amountInput.value) : NaN;
      var added  = addChargeLine(typeId, isNaN(amount) ? 0 : amount);
      if (added) calculateTotals();
    });
  }
  // Phase 5 trigger: tendered changed
  if (tenderedInput) {
    tenderedInput.addEventListener('input', function () { calculateTotals(); });
  }

  //  OTHERS VIEW 
  // Refs for the Others view panel elements
  var othersView        = document.getElementById('ocOthersView');
  var otherBtn          = null;   // toggle button removed
  var lookupView        = null;   // lookup view removed
  var othAccNoInput     = document.getElementById('ocOthAccNo');
  var othAccNameInput   = document.getElementById('ocOthAccName');
  var othSuggestionsEl  = document.getElementById('ocOthAccSuggestions');
  var othChargeType     = document.getElementById('ocOthChargeType');
  var othAmount         = document.getElementById('ocOthAmount');
  var othAddBtn         = document.getElementById('ocOthAddBtn');
  var othTableBody      = document.getElementById('ocOthTableBody');
  var othTotalDueEl     = document.getElementById('ocOthTotalDue');
  var othBalanceEl      = document.getElementById('ocOthBalance');
  var othTenderedInput  = document.getElementById('ocOthTendered');
  var othRemainingRow   = document.querySelector('#ocOthersView .oc-others-remaining-row');
  var othRemainingLabel = document.getElementById('ocOthRemainingLabel');
  var othRemainingVal   = document.getElementById('ocOthRemainingVal');
  var othPayBtn         = document.getElementById('ocOthPayBtn');
  var _othSuggIdx       = -1;
  var _othSearchTimer   = null;

  // Populate Others charge type dropdown from same OC_CHARGE_TYPES data
  function populateOthChargeTypeDropdown() {
    if (!othChargeType || othChargeType.dataset.populated === '1') return;
    var data = (typeof OC_CHARGE_TYPES !== 'undefined') ? OC_CHARGE_TYPES : [];
    data.forEach(function (ct) {
      var opt = document.createElement('option');
      opt.value = ct.id; opt.textContent = ct.label;
      othChargeType.appendChild(opt);
    });
    othChargeType.dataset.populated = '1';
  }

  function setOthersView(active) {
    var defaultView = document.getElementById('ocDefaultView');
    var othersView  = document.getElementById('ocOthersView');
    if (defaultView) defaultView.style.display = active ? 'none' : '';
    if (othersView)  othersView.style.display  = active ? '' : 'none';
    if (subtitleEl)  subtitleEl.textContent    = active ? 'Others Mode' : 'Lookup Mode';
    var btn = document.getElementById('ocModeOthers');
    if (btn) btn.classList.toggle('oc-mode-card--active', active);
  }

  // Others button — toggles between default and Others view
  var modeOthersBtn = document.getElementById('ocModeOthers');
  if (modeOthersBtn) {
    modeOthersBtn.addEventListener('click', function () {
      var isOthers = document.getElementById('ocOthersView').style.display !== 'none';
      setOthersView(!isOthers);
      if (!isOthers) populateOthChargeTypeDropdown();
    });
  }

  // Render Others view table from _ocOthCharges
  function renderOthTable() {
    if (!othTableBody) return;
    if (!_ocOthCharges.length) {
      othTableBody.innerHTML =
        '<tr><td colspan="3" class="oc-receipt-empty">No charges added yet.</td></tr>';
      if (othTotalDueEl) othTotalDueEl.textContent = '0.00';
      if (othBalanceEl)  othBalanceEl.textContent  = '0.00';
      updateOthPayBtn();
      return;
    }
    var total = 0;
    othTableBody.innerHTML = _ocOthCharges.map(function (c) {
      total += c.amount || 0;
      return '<tr>' +
        '<td>' + (c.chargeType || '\u2014') + '</td>' +
        '<td style="text-align:right;font-family:\'IBM Plex Mono\',monospace;">' + fmtPHP(c.amount || 0) + '</td>' +
        '<td class="oc-receipt-col-amount">' + fmtPHP(c.amount || 0) + '</td>' +
      '</tr>';
    }).join('');
    if (othTotalDueEl) othTotalDueEl.textContent = total.toLocaleString('en-PH', { minimumFractionDigits: 2 });
    if (othBalanceEl)  othBalanceEl.textContent  = total.toLocaleString('en-PH', { minimumFractionDigits: 2 });
    calcOthTotals();
  }

  // Recalculate Others view Remaining + enable/disable Pay
  function calcOthTotals() {
    var due      = parseFloat((othTotalDueEl ? othTotalDueEl.textContent : '0').replace(/,/g, '')) || 0;
    var tendered = othTenderedInput ? parseFloat(othTenderedInput.value) : NaN;
    var has      = !isNaN(tendered) && tendered > 0;

    if (!othRemainingRow || !othRemainingLabel || !othRemainingVal) { updateOthPayBtn(); return; }

    if (!has) {
      othRemainingRow.classList.remove('oc-remaining--ok', 'oc-remaining--short');
      othRemainingLabel.textContent = 'Remaining';
      othRemainingVal.textContent   = '0.00';
    } else {
      var diff = tendered - due;
      if (diff >= 0) {
        othRemainingRow.classList.remove('oc-remaining--short');
        othRemainingRow.classList.add('oc-remaining--ok');
        othRemainingLabel.textContent = 'Change';
        othRemainingVal.textContent   = diff.toLocaleString('en-PH', { minimumFractionDigits: 2 });
      } else {
        othRemainingRow.classList.remove('oc-remaining--ok');
        othRemainingRow.classList.add('oc-remaining--short');
        othRemainingLabel.textContent = 'Short By';
        othRemainingVal.textContent   = Math.abs(diff).toLocaleString('en-PH', { minimumFractionDigits: 2 });
      }
    }
    updateOthPayBtn();
  }

  function updateOthPayBtn() {
    if (!othPayBtn) return;
    var due      = parseFloat((othTotalDueEl ? othTotalDueEl.textContent : '0').replace(/,/g, '')) || 0;
    var tendered = othTenderedInput ? parseFloat(othTenderedInput.value) : NaN;
    othPayBtn.disabled = !(due > 0 && !isNaN(tendered) && tendered >= due);
  }

  // Others view: add charge line
  if (othAddBtn) {
    othAddBtn.addEventListener('click', function () {
      var typeId = othChargeType ? othChargeType.value        : '';
      var amount = othAmount     ? parseFloat(othAmount.value) : NaN;
      if (!typeId) { if (othChargeType) othChargeType.focus(); return; }
      if (!amount || amount <= 0) { if (othAmount) othAmount.focus(); return; }
      var types = (typeof OC_CHARGE_TYPES !== 'undefined') ? OC_CHARGE_TYPES : [];
      var rec   = types.find(function (ct) { return ct.id === typeId; });
      _ocOthCharges.push({ chargeType: rec ? rec.label : typeId, chargeTypeId: typeId, amount: amount });
      if (othChargeType) othChargeType.value = '';
      if (othAmount)     othAmount.value     = '';
      renderOthTable();
      if (othChargeType) othChargeType.focus();
    });
  }

  // Others view: tendered changed
  if (othTenderedInput) {
    othTenderedInput.addEventListener('input', calcOthTotals);
  }

  // Others view: Cash/Check radio
  overlay.querySelectorAll('input[name="ocOthPayMethod"]').forEach(function (radio) {
    radio.addEventListener('change', function () {
      _ocOthPayMethod = this.value;
    });
  });

  // Others view: account number search (same logic as Lookup, different elements)
  if (othAccNoInput) {
    othAccNoInput.addEventListener('input', function () {
      var val = this.value.trim();
      clearTimeout(_othSearchTimer);
      if (!val) { hideOthSugg(); return; }
      _othSearchTimer = setTimeout(function () {
        searchAccount(val).then(function(matches) {
          showOthSugg(matches);
        });
      }, 300);
    });
    othAccNoInput.addEventListener('keydown', function (e) {
      var vis = othSuggestionsEl && othSuggestionsEl.style.display !== 'none';
      if (e.key === 'ArrowDown') { e.preventDefault(); navOthSugg('down'); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); navOthSugg('up'); }
      else if (e.key === 'Escape') { hideOthSugg(); }
      else if (e.key === 'Enter') {
        e.preventDefault();
        if (vis && _othSuggIdx >= 0 && othSuggestionsEl._matches && othSuggestionsEl._matches[_othSuggIdx])
          selectOthSugg(othSuggestionsEl._matches[_othSuggIdx]);
        else if (vis && othSuggestionsEl._matches && othSuggestionsEl._matches.length)
          selectOthSugg(othSuggestionsEl._matches[0]);
      }
    });
    othAccNoInput.addEventListener('blur', function () { setTimeout(hideOthSugg, 150); });
  }

  function hideOthSugg() {
    if (othSuggestionsEl) othSuggestionsEl.style.display = 'none';
    _othSuggIdx = -1;
  }
  function showOthSugg(matches) {
    if (!othSuggestionsEl) return;
    if (!matches.length) {
      othSuggestionsEl.innerHTML = '<div class="oc-sugg-empty">No accounts found</div>';
      othSuggestionsEl.style.display = 'block'; _othSuggIdx = -1; return;
    }
    othSuggestionsEl.innerHTML = matches.map(function (r, i) {
      return '<div class="oc-sugg-item" data-idx="' + i + '">' +
        '<div class="oc-sugg-accno">' + r.accountNumber + '</div>' +
        '<div class="oc-sugg-name">'  + r.name + '</div>' +
        '<div class="oc-sugg-meta">'  + (r.tin ? 'TIN: ' + r.tin + ' \u2022 ' : '') + (r.address || '') + '</div>' +
      '</div>';
    }).join('');
    othSuggestionsEl.style.display = 'block';
    othSuggestionsEl._matches = matches; _othSuggIdx = -1;
    othSuggestionsEl.querySelectorAll('.oc-sugg-item').forEach(function (item) {
      item.addEventListener('mousedown', function (e) {
        e.preventDefault();
        var rec = othSuggestionsEl._matches && othSuggestionsEl._matches[parseInt(this.dataset.idx, 10)];
        if (rec) selectOthSugg(rec);
      });
    });
  }
  function navOthSugg(dir) {
    if (!othSuggestionsEl || othSuggestionsEl.style.display === 'none') return;
    var items = othSuggestionsEl.querySelectorAll('.oc-sugg-item');
    if (!items.length) return;
    if (_othSuggIdx >= 0) items[_othSuggIdx].classList.remove('oc-sugg-highlighted');
    _othSuggIdx = dir === 'down' ? (_othSuggIdx + 1) % items.length : (_othSuggIdx <= 0 ? items.length - 1 : _othSuggIdx - 1);
    items[_othSuggIdx].classList.add('oc-sugg-highlighted');
    items[_othSuggIdx].scrollIntoView({ block: 'nearest' });
  }
  function selectOthSugg(rec) {
    if (othAccNoInput)   othAccNoInput.value   = rec.accountNumber;
    if (othAccNameInput) othAccNameInput.value = rec.name;
    hideOthSugg();
  }

  // Others view: Pay button
  if (othPayBtn) {
    othPayBtn.addEventListener('click', function () {
      if (this.disabled) return;
      var due      = parseFloat((othTotalDueEl ? othTotalDueEl.textContent : '0').replace(/,/g, '')) || 0;
      var tendered = othTenderedInput ? parseFloat(othTenderedInput.value) : 0;
      var payload  = {
        mode:           'others',
        accountNumber:  othAccNoInput   ? othAccNoInput.value.trim()   : '',
        accountName:    othAccNameInput ? othAccNameInput.value.trim() : '',
        paymentMethod:  _ocOthPayMethod,
        totalDue:       due,
        amountTendered: tendered,
        change:         parseFloat((tendered - due).toFixed(2)),
        charges:        _ocOthCharges.slice()
      };
      console.log('[OC Others submitPayment] payload:', payload);
      // TODO: replace with fetch('/api/Billing/OtherCharges/Pay', { method:'POST', body:JSON.stringify(payload) })
      setOthersView(false);
      var card = document.getElementById('toast');
      var bd   = document.getElementById('toastBackdrop');
      if (card) {
        document.getElementById('toastTitle').textContent              = 'Success';
        document.getElementById('toastMsg').textContent                =
          'Other Charges (Others) payment of ' + fmtPHP(due) + ' recorded.';
        document.getElementById('toastIconSuccess').style.display = 'block';
        document.getElementById('toastIconDanger').style.display  = 'none';
        card.className = 'toast-card show'; bd.className = 'toast-backdrop show';
        clearTimeout(window._ocPayToastTimer);
        window._ocPayToastTimer = setTimeout(function () {
          card.className = 'toast-card'; bd.className = 'toast-backdrop';
        }, 3000);
      }
      closeModal();
    });
  }

  // "Others" / "Lookup" button — removed, no toggle needed
  //  Phase 6: updatePayBtn + submitPayment 
  function updatePayBtn() {
    if (!payBtn) return;
    var due      = totalEl ? (parseFloat(totalEl.textContent.replace(/[₱,\s]/g, '')) || 0) : 0;
    var tendered = tenderedInput ? parseFloat(tenderedInput.value) : NaN;
    payBtn.disabled = !(due > 0 && !isNaN(tendered) && tendered > 0 && tendered >= due);
  }

  function submitPayment() {
    var due      = totalEl ? (parseFloat(totalEl.textContent.replace(/[₱,\s]/g, '')) || 0) : 0;
    var tendered = tenderedInput ? parseFloat(tenderedInput.value) : NaN;

    if (!window._ocCurrentAccount) {
      showAddError('No account selected. Search for an account first.');
      if (accNoInput) accNoInput.focus(); return;
    }
    if (due <= 0) {
      showAddError('Add at least one charge before paying.');
      if (chargeTypeSelect) chargeTypeSelect.focus(); return;
    }
    if (isNaN(tendered) || tendered < due) {
      showAddError('Amount Tendered must be \u2265 Total Due.');
      if (tenderedInput) tenderedInput.focus(); return;
    }

    var acc     = window._ocCurrentAccount;
    var charges = (acc && Array.isArray(acc.otherCharges)) ? acc.otherCharges.slice() : [];
    var payload = {
      accountNumber:  accNoInput   ? accNoInput.value.trim()   : '',
      accountName:    accNameInput ? accNameInput.value.trim() : '',
      tin:            tinInput     ? tinInput.value.trim()     : '',
      address:        addressInput ? addressInput.value.trim() : '',
      paymentMethod:  _ocPayMethod,
      totalDue:       due,
      amountTendered: tendered,
      change:         parseFloat((tendered - due).toFixed(2)),
      charges:        charges
    };

    payBtn.disabled    = true;
    payBtn.textContent = 'Processing\u2026';

    // TODO: replace with fetch('/api/Billing/OtherCharges/Pay', { method:'POST', ... })
    console.log('[OC submitPayment] payload:', payload);
    Promise.resolve()
      .then(function () {
        closeModal();
        var card = document.getElementById('toast');
        var bd   = document.getElementById('toastBackdrop');
        if (card) {
          document.getElementById('toastTitle').textContent              = 'Success';
          document.getElementById('toastMsg').textContent                =
            'Other Charges payment of ' + fmtPHP(due) + ' recorded (' + _ocPayMethod.toUpperCase() + ').';
          document.getElementById('toastIconSuccess').style.display = 'block';
          document.getElementById('toastIconDanger').style.display  = 'none';
          card.className = 'toast-card show';
          bd.className   = 'toast-backdrop show';
          clearTimeout(window._ocPayToastTimer);
          window._ocPayToastTimer = setTimeout(function () {
            card.className = 'toast-card'; bd.className = 'toast-backdrop';
          }, 3000);
        }
      })
      .catch(function (err) {
        console.error('[OC submitPayment] error:', err);
        if (payBtn) { payBtn.disabled = false; payBtn.textContent = 'Pay'; }
        showAddError('Payment failed. Please try again.');
      });
  }

  // Phase 6 listener — attached last
  if (payBtn) {
    payBtn.addEventListener('click', function () {
      if (!this.disabled) submitPayment();
    });
  }

  //  Open 
  function openModal() {
    populateChargeTypeDropdown();
    populateOthChargeTypeDropdown();

    var defaultView = document.getElementById('ocDefaultView');
    var othersView2 = document.getElementById('ocOthersView');
    if (defaultView) defaultView.style.display = '';
    if (othersView2) othersView2.style.display  = 'none';
    if (subtitleEl) subtitleEl.textContent = 'Lookup Mode';

    fillAccountFields(window._ocCurrentAccount);
    renderTable();

    if (chargeTypeSelect) { chargeTypeSelect.value = ''; chargeTypeSelect.classList.remove('oc-receipt-select--invalid'); }
    if (amountInput)      { amountInput.value = '';      amountInput.classList.remove('oc-receipt-input--invalid'); }
    if (tenderedInput)    tenderedInput.value = '';
    clearAddError();

    setPaymentMethod('cash');

    if (remainingBlockEl) remainingBlockEl.classList.remove('oc-remaining--ok', 'oc-remaining--short');
    if (remainingLabelEl) remainingLabelEl.textContent = 'Remaining';
    if (remainingValEl)   remainingValEl.textContent   = fmtPHP(0);

    if (payBtn) { payBtn.disabled = true; payBtn.textContent = 'Pay'; }

    // Reset Others view fields
    if (othAccNoInput)   othAccNoInput.value   = '';
    if (othAccNameInput) othAccNameInput.value = '';
    if (othTenderedInput) othTenderedInput.value = '';
    if (othChargeType)   othChargeType.value   = '';
    if (othAmount)       othAmount.value        = '';
    if (othTotalDueEl)   othTotalDueEl.textContent = '0.00';
    if (othBalanceEl)    othBalanceEl.textContent  = '0.00';
    if (othRemainingLabel) othRemainingLabel.textContent = 'Remaining';
    if (othRemainingVal)   othRemainingVal.textContent   = '0.00';
    if (othRemainingRow)   othRemainingRow.classList.remove('oc-remaining--ok', 'oc-remaining--short');
    if (othPayBtn) othPayBtn.disabled = true;
    hideOthSugg();
    // reset radios
    overlay.querySelectorAll('input[name="ocOthPayMethod"]').forEach(function (r) {
      r.checked = r.value === 'cash';
    });
    if (othTableBody) {
      othTableBody.innerHTML =
        '<tr><td colspan="3" class="oc-receipt-empty">No charges added yet.</td></tr>';
    }

    calculateTotals();
    clearTimeout(_ocSearchTimer);
    hideSuggestions();

    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';

    setTimeout(function () { if (accNoInput) accNoInput.focus(); }, 180);
  }

  //  Close 
  function closeModal() {
    clearTimeout(_ocSearchTimer);
    clearTimeout(_othSearchTimer);
    hideSuggestions();
    hideOthSugg();
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
  }

  //  Open/close wiring 
  infoBtn.addEventListener('click', function (e) { e.stopPropagation(); openOceModal(); });

  document.querySelectorAll('.link-chip[data-modal="others"]').forEach(function (chip) {
    chip.addEventListener('click', function (e) { e.stopPropagation(); openModal(); });
  });

  if (closeBtn) closeBtn.addEventListener('click', closeModal);

  overlay.addEventListener('click', function (e) { if (e.target === overlay) closeModal(); });

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

  populateChargeTypeDropdown();

}());

// ── Check Payment toggle (both views) ────────────────────────────────────────
(function initOcCheckPayment() {
  var BANKS = (typeof SAMPLE_BANKS !== 'undefined') ? SAMPLE_BANKS : [];

  function populateBankDropdown(selectId) {
    var sel = document.getElementById(selectId);
    if (!sel || sel.dataset.populated === '1') return;
    BANKS.forEach(function (b) {
      var opt = document.createElement('option');
      opt.value = b; opt.textContent = b;
      sel.appendChild(opt);
    });
    sel.dataset.populated = '1';
  }

  function wireCheckToggle(radioName, checkFieldsId, bankSelectId) {
    var radios = document.querySelectorAll('input[name="' + radioName + '"]');
    var fields = document.getElementById(checkFieldsId);
    if (!fields) return;

    radios.forEach(function (radio) {
      radio.addEventListener('change', function () {
        var isCheck = this.value === 'check';
        fields.style.display = isCheck ? 'flex' : 'none';
        if (isCheck) populateBankDropdown(bankSelectId);
      });
    });
  }

  wireCheckToggle('ocOthPayMethod',  'ocCheckFields',  'ocCheckBank');
  wireCheckToggle('ocOthPayMethod2', 'ocCheckFields2', 'ocCheckBank2');
}());


// ============================================================
//  Other Charges Entry Modal (oce-*)
//  Opened by the + button on the Other Charges Balance card.
//  Separate from the OC payment modal (oc-*).
// ============================================================

(function initOtherChargesEntryModal() {
  'use strict';

  var overlay      = document.getElementById('oceOverlay');
  var closeBtn     = document.getElementById('oceClose');
  var accNoInput   = document.getElementById('oceAccNo');
  var accNameInput = document.getElementById('oceAccName');
  var suggestEl    = document.getElementById('oceAccSuggestions');
  var chargeType   = document.getElementById('oceChargeType');
  var payableIn    = document.getElementById('ocePayableIn');
  var particulars  = document.getElementById('oceParticulars');
  var amountInput  = document.getElementById('oceAmount');
  var amountError  = document.getElementById('oceAmountError');
  var addBtn       = document.getElementById('oceAddBtn');
  var tableBody    = document.getElementById('oceTableBody');
  var totalDueEl   = document.getElementById('oceTotalDue');

  if (!overlay) return;

  var _charges     = [];
  var _searchTimer = null;
  var _suggIdx     = -1;

  // Populate dropdowns from collection_sample.js
  function populateDropdowns() {
    // Charge types
    if (chargeType && chargeType.dataset.populated !== '1') {
      var types = (typeof OC_CHARGE_TYPES !== 'undefined') ? OC_CHARGE_TYPES : [];
      types.forEach(function (ct) {
        var opt = document.createElement('option');
        opt.value = ct.id; opt.textContent = ct.label;
        chargeType.appendChild(opt);
      });
      chargeType.dataset.populated = '1';
    }
    // Payable In
    if (payableIn && payableIn.dataset.populated !== '1') {
      var opts = (typeof SAMPLE_PAYABLE_IN !== 'undefined') ? SAMPLE_PAYABLE_IN : [];
      opts.forEach(function (o) {
        var opt = document.createElement('option');
        opt.value = o; opt.textContent = o;
        payableIn.appendChild(opt);
      });
      payableIn.dataset.populated = '1';
    }
  }

  // Render table + total
  function renderTable() {
    if (!tableBody) return;
    if (!_charges.length) {
      tableBody.innerHTML = '<tr><td colspan="4" class="oce-empty">No charges added yet.</td></tr>';
      if (totalDueEl) totalDueEl.textContent = '0';
      return;
    }
    var total = 0;
    tableBody.innerHTML = _charges.map(function (c) {
      total += c.amount || 0;
      return '<tr>' +
        '<td>' + (c.description || '\u2014') + '</td>' +
        '<td>' + (c.payableIn   || '\u2014') + '</td>' +
        '<td>' + (c.particulars || '\u2014') + '</td>' +
        '<td class="oce-col-right" style="font-family:\'IBM Plex Mono\',monospace;">' +
          '\u20B1' + Number(c.amount).toLocaleString('en-PH', { minimumFractionDigits: 2 }) +
        '</td>' +
      '</tr>';
    }).join('');
    if (totalDueEl) totalDueEl.textContent =
      Number(total).toLocaleString('en-PH', { minimumFractionDigits: 2 });
  }

  // Fill account fields + auto-populate charges from account record only
  function fillAccount(acc) {
    if (accNameInput) accNameInput.value = acc ? (acc.name || '') : '';
    _charges = [];
    if (acc && Array.isArray(acc.otherCharges)) {
      _charges = acc.otherCharges.map(function (c) {
        return {
          description: c.chargeType  || c.description || '',
          payableIn:   c.payableIn   || '',
          particulars: c.particulars || '',
          amount:      c.amount      || 0
        };
      });
    }
    renderTable();
  }

  // Account number live search
  function hideSugg() {
    if (suggestEl) suggestEl.style.display = 'none';
    _suggIdx = -1;
  }

  function showSugg(matches) {
    if (!suggestEl) return;
    if (!matches.length) {
      suggestEl.innerHTML = '<div class="oc-sugg-empty">No accounts found</div>';
      suggestEl.style.display = 'block'; return;
    }
    suggestEl.innerHTML = matches.map(function (r, i) {
      return '<div class="oc-sugg-item" data-idx="' + i + '">' +
        '<div class="oc-sugg-accno">' + r.accountNumber + '</div>' +
        '<div class="oc-sugg-name">'  + r.name + '</div>' +
      '</div>';
    }).join('');
    suggestEl.style.display = 'block';
    suggestEl._matches = matches; _suggIdx = -1;
    suggestEl.querySelectorAll('.oc-sugg-item').forEach(function (item) {
      item.addEventListener('mousedown', function (e) {
        e.preventDefault();
        var rec = suggestEl._matches[parseInt(this.dataset.idx, 10)];
        if (!rec) return;
        accNoInput.value = rec.accountNumber;
        fillAccount(rec);
        hideSugg();
      });
    });
  }

  if (accNoInput) {
    accNoInput.addEventListener('input', function () {
      var val = this.value.trim();
      clearTimeout(_searchTimer);
      // Clear account name when user edits
      if (accNameInput) accNameInput.value = '';
      _charges = []; renderTable();
      if (!val) { hideSugg(); return; }
      _searchTimer = setTimeout(function () {
        var lu = (typeof OC_ACCOUNT_LOOKUP !== 'undefined') ? OC_ACCOUNT_LOOKUP : [];
        var ca = (typeof COLLECTION_ACCOUNTS !== 'undefined') ? COLLECTION_ACCOUNTS : [];
        var t  = val.toLowerCase();
        var results = [];
        var seen = {};
        lu.forEach(function (r) {
          if (seen[r.accountNumber]) return;
          if (r.accountNumber.toLowerCase().indexOf(t) !== -1 || r.name.toLowerCase().indexOf(t) !== -1) {
            seen[r.accountNumber] = true;
            results.push(r);
          }
        });
        ca.forEach(function (entry) {
          var acc = entry.account;
          if (!acc || seen[acc.accountNumber]) return;
          if (acc.accountNumber.toLowerCase().indexOf(t) !== -1 || acc.name.toLowerCase().indexOf(t) !== -1) {
            seen[acc.accountNumber] = true;
            results.push(acc);
          }
        });
        showSugg(results);
      }, 300);
    });
    accNoInput.addEventListener('keydown', function (e) {
      var vis = suggestEl && suggestEl.style.display !== 'none';
      if (e.key === 'ArrowDown' && vis) { e.preventDefault(); /* nav future */ }
      else if (e.key === 'Escape') hideSugg();
    });
    accNoInput.addEventListener('blur', function () { setTimeout(hideSugg, 150); });
  }

  // Add charge line
  if (addBtn) {
    addBtn.addEventListener('click', function () {
      var typeId = chargeType ? chargeType.value : '';
      var amt    = amountInput ? parseFloat(amountInput.value) : NaN;
      var valid  = true;

      if (!amt || amt <= 0) {
        if (amountError) amountError.style.display = 'block';
        if (amountInput) amountInput.focus();
        valid = false;
      } else {
        if (amountError) amountError.style.display = 'none';
      }
      if (!valid) return;

      var types = (typeof OC_CHARGE_TYPES !== 'undefined') ? OC_CHARGE_TYPES : [];
      var rec   = types.find(function (ct) { return ct.id === typeId; });
      _charges.push({
        description: rec ? rec.label : (typeId || '\u2014'),
        payableIn:   payableIn   ? payableIn.value   : '',
        particulars: particulars ? particulars.value  : '',
        amount:      amt
      });
      if (chargeType)  chargeType.value  = '';
      if (payableIn)   payableIn.value   = '';
      if (particulars) particulars.value = '';
      if (amountInput) amountInput.value = '';
      renderTable();
      if (chargeType) chargeType.focus();
    });
  }

  // Clear error on amount input
  if (amountInput) {
    amountInput.addEventListener('input', function () {
      if (amountError) amountError.style.display = 'none';
    });
  }

  // Open
  window.openOceModal = function () {
    populateDropdowns();
    // Do not pre-fill — table stays empty until user types and selects an account
    if (accNoInput)   accNoInput.value   = '';
    if (accNameInput) accNameInput.value = '';
    _charges = [];
    renderTable();
    if (chargeType)  chargeType.value  = '';
    if (payableIn)   payableIn.value   = '';
    if (particulars) particulars.value = '';
    if (amountInput) amountInput.value = '';
    if (amountError) amountError.style.display = 'none';
    hideSugg();
    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';
    setTimeout(function () { if (accNoInput) accNoInput.focus(); }, 150);
  };

  // Close
  function closeModal() {
    clearTimeout(_searchTimer);
    hideSugg();
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
  }

  if (closeBtn) closeBtn.addEventListener('click', closeModal);
  overlay.addEventListener('click', function (e) { if (e.target === overlay) closeModal(); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

}());
