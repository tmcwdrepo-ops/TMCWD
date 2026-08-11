//  Collection Remittance Modal \u2014 open / close 
// ============================================================
(function initRemittanceModal() {
  const overlay     = document.getElementById('crOverlay');
  const closeBtn    = document.getElementById('crCloseBtn');
  const dcrBtn      = document.getElementById('dcrBtn');
  const crDateInput = document.getElementById('crDate');

  if (!overlay || !dcrBtn) return;

  // ── Populate collector dropdown from SAMPLE_COLLECTORS ──────────────────────
  // TODO: replace with fetch('/api/Billing/Collectors').then(r => r.json())
  (function populateCollectorDropdown() {
    var select = document.getElementById('crCollector');
    if (!select) return;
    var collectors = (typeof SAMPLE_COLLECTORS !== 'undefined') ? SAMPLE_COLLECTORS : [];
    collectors.forEach(function (name) {
      var opt = document.createElement('option');
      opt.value = name;
      opt.textContent = name;
      select.appendChild(opt);
    });
  }());

  // Seed today's date into the Date field each time the modal opens
  function seedDate() {
    const today = new Date();
    crDateInput.value = today.toISOString().slice(0, 10);
  }

  function openModal() {
    seedDate();
    // Reset remittance fields state if the fields module is already initialised
    if (typeof window.crRecalculateTotals === 'function') {
      window.crRecalculateTotals();
    }
    overlay.style.display = 'flex';
    // Focus the first count input after the animation settles
    setTimeout(function () {
      const firstInput = document.getElementById('count-1000');
      if (firstInput) firstInput.focus();
    }, 220);
  }

  function closeModal() {
    overlay.style.display = 'none';
  }

  // DCR nav item â†’ open
  dcrBtn.addEventListener('click', function (e) {
    e.preventDefault();
    openModal();
  });

  // X button â†’ close
  closeBtn.addEventListener('click', closeModal);

  // Click on the dim overlay (outside the card) â†’ close
  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });

  // Escape key â†’ close
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });
}());


// ============================================================
//  Collection Remittance Modal \u2014  Denomination Math
// ============================================================
(function initDenominationMath() {

  // Face values for bill denominations (coins & check handled separately)
  const FACE_VALUES = {
    '1000': 1000,
    '500':  500,
    '200':  200,
    '100':  100,
    '50':   50,
    '20':   20,
    '10':   10,
    '5':    5
  };

  // Keys whose "count" input IS the amount directly
  const DIRECT_AMOUNT_KEYS = ['coins', 'check'];

  // All denomination keys in display order
  const ALL_KEYS = [...Object.keys(FACE_VALUES), ...DIRECT_AMOUNT_KEYS];

  // ---------- Formatting ----------
  function fmtAmount(n) {
    return Number(n).toLocaleString('en-PH', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  // ---------- Per-row calculation ----------
  // Returns the numeric amount for a given denomination key.
  function calculateRowAmount(key) {
    const input = document.getElementById('count-' + key);
    if (!input) return 0;

    const raw = parseFloat(input.value);
    const count = isNaN(raw) || raw < 0 ? 0 : raw;

    if (DIRECT_AMOUNT_KEYS.includes(key)) {
      return count;                   // coins / check: input value IS the amount
    }
    return count * FACE_VALUES[key];  // bills: count x face value
  }

  // ---------- Cash-on-hand stub ----------
  // TODO: replace with a real API call once the backend endpoint is ready.
  // Should accept orStart, orEnd, collector and return the expected cash total
  // for that O.R. range from the system.
  function getExpectedCashOnHand(orStart, orEnd, collector) {
    // Stub \u2014 returns a mock value so Short/Over math can be exercised in the UI.
    return 0;
  }

  // Receives the already-computed subtotals from recalculateTotals() so we
  // don't re-walk the DOM a second time.
  function updateSummaryCard(billsSum, coinsAmt, checkAmt) {
    // --- Check (mirrors the Check row amount) ---
    const checkAmtEl = document.getElementById('checkAmount');
    if (checkAmtEl) checkAmtEl.textContent = fmtAmount(checkAmt);

    // --- Online (no table row yet \u2014 stubbed at 0) ---
    // TODO: wire to an Online input or API value in a future phase.
    const onlineAmt   = 0;
    const onlineAmtEl = document.getElementById('onlineAmount');
    if (onlineAmtEl) onlineAmtEl.textContent = fmtAmount(onlineAmt);

    // --- Cash on Hand (from stub / future API) ---
    const orStart   = (document.getElementById('orStart')   || {}).value || '';
    const orEnd     = (document.getElementById('orEnd')     || {}).value || '';
    const collector = (document.getElementById('crCollector') || {}).value || '';
    const cashOnHandAmt = getExpectedCashOnHand(orStart, orEnd, collector);
    const cashOnHandEl  = document.getElementById('cashOnHand');
    if (cashOnHandEl) cashOnHandEl.textContent = fmtAmount(cashOnHandAmt);

    // --- Counted Cash = Bills + Coins only (no check, no online) ---
    const countedCashAmt = billsSum + coinsAmt;
    const countedCashEl  = document.getElementById('countedCash');
    if (countedCashEl) countedCashEl.textContent = fmtAmount(countedCashAmt);

    // --- Short / Over = Counted Cash - Cash on Hand ---
    const shortOverAmt = countedCashAmt - cashOnHandAmt;
    const shortOverEl  = document.getElementById('shortOver');
    if (shortOverEl) {
      if (shortOverAmt === 0) {
        shortOverEl.textContent = '0.00';
        shortOverEl.style.color = '#16a34a'; // green
      } else if (shortOverAmt < 0) {
        shortOverEl.textContent = '-' + fmtAmount(Math.abs(shortOverAmt));
        shortOverEl.style.color = '#dc2626'; // red  \u2014 short
      } else {
        shortOverEl.textContent = '+' + fmtAmount(shortOverAmt);
        shortOverEl.style.color = '#16a34a'; // green \u2014 over
      }
    }
  }

  // ---------- Full recalculation ----------
  // Called after any count input changes.
  // Updates every amount-* cell, billsTotal, totalCollection, and the
  // Exported to window so external code can trigger a recalc.
  function recalculateTotals() {
    let billsSum  = 0;
    let coinsAmt  = 0;
    let checkAmt  = 0;

    ALL_KEYS.forEach(function (key) {
      const amt     = calculateRowAmount(key);
      const amtCell = document.getElementById('amount-' + key);
      if (amtCell) amtCell.textContent = fmtAmount(amt);

      if (DIRECT_AMOUNT_KEYS.includes(key)) {
        if (key === 'coins') coinsAmt = amt;
        if (key === 'check') checkAmt = amt;
      } else {
        billsSum += amt;
      }
    });

    // --- Bills total (bill denominations only) ---
    const billsTotalEl = document.getElementById('billsTotal');
    if (billsTotalEl) billsTotalEl.textContent = fmtAmount(billsSum);

    // --- Other Charges 
    const otherChargesEl = document.getElementById('otherCharges');
    const otherCharges   = otherChargesEl
      ? parseFloat(otherChargesEl.textContent.replace(/,/g, '')) || 0
      : 0;

    // --- Total Collection = bills + coins + check + other charges ---
    const total = billsSum + coinsAmt + checkAmt + otherCharges;
    const totalCollectionEl = document.getElementById('totalCollection');
    if (totalCollectionEl) totalCollectionEl.textContent = fmtAmount(total);

    // update the rest of the summary card ---
    updateSummaryCard(billsSum, coinsAmt, checkAmt);
  }

  // Expose so later phases / OR-field change handlers can trigger a full recalc.
  window.crRecalculateTotals = recalculateTotals;

  // ---------- Input sanitisation ----------
  // Blocks non-numeric keys; allows one decimal point for coins/check rows.
  // ArrowUp / ArrowDown move focus to the previous / next count input.
  function sanitiseCountInput(e) {
    const isDirect = DIRECT_AMOUNT_KEYS.includes(e.target.id.replace('count-', ''));
    const allowed  = ['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight',
                      'Tab', 'Home', 'End'];

    // ---- Up / Down arrow â†’ navigate between rows ----
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      e.stopPropagation();

      // Query live from DOM every time \u2014 guaranteed correct order
      const inputs = Array.from(
        document.querySelectorAll('#crOverlay .cr-count-input')
      );
      const idx = inputs.indexOf(e.target);
      if (idx === -1) return;
      const next = e.key === 'ArrowDown'
        ? inputs[(idx + 1) % inputs.length]
        : inputs[(idx - 1 + inputs.length) % inputs.length];
      if (next) { next.focus(); next.select(); }
      return;
    }

    if (allowed.includes(e.key)) return;
    if (isDirect && e.key === '.' && !e.target.value.includes('.')) return;
    if (!/^\d$/.test(e.key)) e.preventDefault();
  }

  // ---------- Bind events once the modal DOM is present ----------
  function bindCountInputs() {
    ALL_KEYS.forEach(function (key) {
      const input = document.getElementById('count-' + key);
      if (!input) return;

      // Numeric-only keypress guard
      input.addEventListener('keydown', sanitiseCountInput);

      // Active-focus highlight \u2014 add on focus, remove on blur
      input.addEventListener('focus', function () {
        document.querySelectorAll('.cr-count-input').forEach(function (el) {
          el.classList.remove('cr-count-input--focus');
        });
        this.classList.add('cr-count-input--focus');
      });
      input.addEventListener('blur', function () {
        this.classList.remove('cr-count-input--focus');
      });

      // Recalculate on every value change
      input.addEventListener('input', function () {
        // Strip non-numeric chars (paste protection), allow one dot for direct keys
        const isDirect = DIRECT_AMOUNT_KEYS.includes(key);
        let val = isDirect
          ? this.value.replace(/[^0-9.]/g, '')
          : this.value.replace(/[^0-9]/g, '');

        // Allow only one decimal point
        if (isDirect) {
          const parts = val.split('.');
          if (parts.length > 2) val = parts[0] + '.' + parts.slice(1).join('');
        }

        if (this.value !== val) this.value = val;

        recalculateTotals();
      });
    });
  }

  // Re-run summary when OR range or collector changes (Cash on Hand depends on them)
  ['orStart', 'orEnd', 'crCollector'].forEach(function (id) {
    const el = document.getElementById(id);
    if (el) el.addEventListener('change', recalculateTotals);
  });

  // Run immediately (elements already exist in the hidden overlay)
  bindCountInputs();

}());


// ============================================================
//  Collection Remittance Modal \u2014 it Field Wiring & State
// ============================================================
(function initRemittanceFields() {

  // ----------------------------------------------------------
  // Collector list \u2014 stub array.
  // Collectors sourced from collection_sample.js
  // TODO: replace with a real API call to load active collectors.
  const COLLECTORS = (typeof SAMPLE_COLLECTORS !== 'undefined') ? SAMPLE_COLLECTORS : [];

  // ----------------------------------------------------------
  // Central state object \u2014 single source of truth for the modal.
  // ----------------------------------------------------------
  window.remittanceState = {
    date:        '',
    collector:   '',
    orStart:     '',
    orEnd:       '',
    isDeposit:   true,
    counts: {
      '1000':  '',
      '500':   '',
      '200':   '',
      '100':   '',
      '50':    '',
      '20':    '',
      '10':    '',
      '5':     '',
      'coins': '',
      'check': ''
    }
  };

  // ----------------------------------------------------------
  // Helpers
  // ----------------------------------------------------------
  function today() {
    return new Date().toISOString().slice(0, 10);
  }

  // ----------------------------------------------------------
  // Populate the collector <select> from the COLLECTORS array.
  // Clears any options already in the markup so there are no
  // duplicates if the modal is opened more than once.
  // ----------------------------------------------------------
  function populateCollectorDropdown() {
    const select = document.getElementById('crCollector');
    if (!select) return;

    select.innerHTML = '';
    COLLECTORS.forEach(function (name) {
      const opt = document.createElement('option');
      opt.value       = name;
      opt.textContent = name;
      select.appendChild(opt);
    });

    // Seed state with the initially-selected value
    remittanceState.collector = select.value;
  }

  // ----------------------------------------------------------
  // Reset the whole modal back to a clean open state.
  // ----------------------------------------------------------
  function resetModalState() {
    // Date
    const crDate = document.getElementById('crDate');
    const dateVal = today();
    if (crDate) crDate.value = dateVal;
    remittanceState.date = dateVal;

    // Collector \u2014 re-populate and pick the first entry
    populateCollectorDropdown();
    const select = document.getElementById('crCollector');
    if (select) remittanceState.collector = select.value;

    // OR fields
    const orStartEl = document.getElementById('orStart');
    const orEndEl   = document.getElementById('orEnd');
    if (orStartEl) orStartEl.value = '';
    if (orEndEl)   orEndEl.value   = '';
    remittanceState.orStart = '';
    remittanceState.orEnd   = '';

    // Deposit checkbox
    const depositCb = document.getElementById('depositCheckbox');
    if (depositCb) depositCb.checked = true;
    remittanceState.isDeposit = true;

    // Count inputs
    Object.keys(remittanceState.counts).forEach(function (key) {
      const input = document.getElementById('count-' + key);
      if (input) input.value = '';
      remittanceState.counts[key] = '';
    });

    // Refresh all computed display values
    if (typeof window.crRecalculateTotals === 'function') {
      window.crRecalculateTotals();
    }
  }

  // ----------------------------------------------------------
  // Bind field-level event listeners (run once at page load).
  // ----------------------------------------------------------
  function bindFields() {

    // ---- Date ----
    const crDate = document.getElementById('crDate');
    if (crDate) {
      crDate.addEventListener('change', function () {
        remittanceState.date = this.value;
      });
    }

    // ---- Collector ----
    const crCollector = document.getElementById('crCollector');
    if (crCollector) {
      crCollector.addEventListener('change', function () {
        remittanceState.collector = this.value;
        // Cash on Hand depends on collector \u2014 recalculate summary
        if (typeof window.crRecalculateTotals === 'function') {
          window.crRecalculateTotals();
        }
      });
    }

    // ---- O.R. Start ----
    const orStartEl = document.getElementById('orStart');
    if (orStartEl) {
      orStartEl.addEventListener('input', function () {
        remittanceState.orStart = this.value.trim();
      });
      orStartEl.addEventListener('change', function () {
        remittanceState.orStart = this.value.trim();
        if (typeof window.crRecalculateTotals === 'function') {
          window.crRecalculateTotals();
        }
      });
    }

    // ---- O.R. End ----
    const orEndEl = document.getElementById('orEnd');
    if (orEndEl) {
      orEndEl.addEventListener('input', function () {
        remittanceState.orEnd = this.value.trim();
      });
      orEndEl.addEventListener('change', function () {
        remittanceState.orEnd = this.value.trim();
        if (typeof window.crRecalculateTotals === 'function') {
          window.crRecalculateTotals();
        }
      });
    }

    // ---- O.R. Refresh button \u2014 clear both OR fields & recalc ----
    const orRefreshBtn = document.getElementById('orRefreshBtn');
    if (orRefreshBtn) {
      orRefreshBtn.addEventListener('click', function () {
        if (orStartEl) orStartEl.value = '';
        if (orEndEl)   orEndEl.value   = '';
        remittanceState.orStart = '';
        remittanceState.orEnd   = '';
        if (typeof window.crRecalculateTotals === 'function') {
          window.crRecalculateTotals();
        }
      });
    }

    // ---- Deposit checkbox ----
    const depositCb = document.getElementById('depositCheckbox');
    if (depositCb) {
      depositCb.addEventListener('change', function () {
        remittanceState.isDeposit = this.checked;
      });
    }

    // ---- Count inputs \u2014 keep remittanceState.counts in sync ----
    Object.keys(remittanceState.counts).forEach(function (key) {
      const input = document.getElementById('count-' + key);
      if (!input) return;
      input.addEventListener('input', function () {
        remittanceState.counts[key] = this.value;
      });
    });
  }

  // resetModalState is called inside openModal (via initRemittanceModal's dcrBtn listener).
  // No separate patchOpenModal listener needed \u2014 removed to avoid duplicate handlers.

  // ----------------------------------------------------------
  // Initial setup on page load
  // ----------------------------------------------------------
  populateCollectorDropdown();
  bindFields();

}());


// ============================================================
//  Collection Remittance Modal \u2014   Save & Validation
// ============================================================
(function initRemittanceSave() {

  // ----------------------------------------------------------
  // Inline error helpers
  // ----------------------------------------------------------

  // Attach one <span class="cr-field-error"> after the given element.
  // Re-uses an existing one if already present so we never stack duplicates.
  function showFieldError(anchorEl, message) {
    if (!anchorEl) return;
    let err = anchorEl.parentElement.querySelector('.cr-field-error');
    if (!err) {
      err = document.createElement('span');
      err.className = 'cr-field-error';
      anchorEl.parentElement.appendChild(err);
    }
    err.textContent = message;
    err.style.display = 'block';
    anchorEl.classList.add('cr-input-error');
  }

  function clearFieldError(anchorEl) {
    if (!anchorEl) return;
    const err = anchorEl.parentElement.querySelector('.cr-field-error');
    if (err) err.style.display = 'none';
    anchorEl.classList.remove('cr-input-error');
  }

  function clearAllErrors() {
    document.querySelectorAll('.cr-field-error').forEach(function (el) {
      el.style.display = 'none';
    });
    document.querySelectorAll('.cr-input-error').forEach(function (el) {
      el.classList.remove('cr-input-error');
    });
  }

  // Auto-clear an error as soon as the user edits the offending field
  function autoClearOnInput(el) {
    if (!el) return;
    el.addEventListener('input',  function () { clearFieldError(el); }, { once: true });
    el.addEventListener('change', function () { clearFieldError(el); }, { once: true });
  }

  // ----------------------------------------------------------
  // 1. Validation
  // ----------------------------------------------------------
  function validateRemittance() {
    clearAllErrors();
    let valid = true;

    const crDate      = document.getElementById('crDate');
    const crCollector = document.getElementById('crCollector');
    const orStartEl   = document.getElementById('orStart');
    const orEndEl     = document.getElementById('orEnd');

    // Date
    if (!crDate || !crDate.value.trim()) {
      showFieldError(crDate, 'Date is required.');
      autoClearOnInput(crDate);
      valid = false;
    }

    // Collector
    if (!crCollector || !crCollector.value.trim()) {
      showFieldError(crCollector, 'Please select a collector.');
      autoClearOnInput(crCollector);
      valid = false;
    }

    // O.R. Start
    if (!orStartEl || !orStartEl.value.trim()) {
      showFieldError(orStartEl, 'O.R. Start is required.');
      autoClearOnInput(orStartEl);
      valid = false;
    }

    // O.R. End
    if (!orEndEl || !orEndEl.value.trim()) {
      showFieldError(orEndEl, 'O.R. End is required.');
      autoClearOnInput(orEndEl);
      valid = false;
    }

    // At least one denomination counted
    const state  = window.remittanceState;
    const hasCounts = state && Object.values(state.counts).some(function (v) {
      return parseFloat(v) > 0;
    });

    if (!hasCounts) {
      // Highlight the first count input as the anchor for the error
      const firstCount = document.getElementById('count-1000');
      showFieldError(firstCount, 'Enter at least one denomination count.');
      autoClearOnInput(firstCount);
      valid = false;
    }

    return valid;
  }

  // ----------------------------------------------------------
  // 2. Build payload
  // ----------------------------------------------------------
  function readDisplayValue(id) {
    const el = document.getElementById(id);
    return el ? parseFloat(el.textContent.replace(/,/g, '')) || 0 : 0;
  }

  function buildPayload() {
    const state = window.remittanceState;

    // Normalise counts to numbers
    const counts = {};
    Object.keys(state.counts).forEach(function (key) {
      counts[key] = parseFloat(state.counts[key]) || 0;
    });

    return {
      date:            state.date,
      collector:       state.collector,
      orStart:         state.orStart,
      orEnd:           state.orEnd,
      isDeposit:       state.isDeposit,
      counts:          counts,
      otherCharges:    readDisplayValue('otherCharges'),
      billsTotal:      readDisplayValue('billsTotal'),
      totalCollection: readDisplayValue('totalCollection'),
      checkAmount:     readDisplayValue('checkAmount'),
      onlineAmount:    readDisplayValue('onlineAmount'),
      cashOnHand:      readDisplayValue('cashOnHand'),
      countedCash:     readDisplayValue('countedCash'),
      shortOver:       readDisplayValue('shortOver')
    };
  }

  // ----------------------------------------------------------
  // 3. Submit stub
  // TODO: replace the console.log + mock Promise with a real
  //       fetch/POST to the remittance API endpoint, e.g.:
  //       return fetch('/api/Billing/Remittance', {
  //         method: 'POST',
  //         headers: { 'Content-Type': 'application/json' },
  //         body: JSON.stringify(payload)
  //       }).then(r => { if (!r.ok) throw new Error(r.statusText); });
  // ----------------------------------------------------------
  function submitRemittance(payload) {
    console.log('[submitRemittance] payload:', payload);
    return Promise.resolve({ success: true });
  }

  // ----------------------------------------------------------
  // 4. Reset form after successful save
  
  // ----------------------------------------------------------
  function resetRemittanceForm() {
    clearAllErrors();

    // Reset all count inputs
    const state = window.remittanceState;
    if (state) {
      Object.keys(state.counts).forEach(function (key) {
        const input = document.getElementById('count-' + key);
        if (input) input.value = '';
        state.counts[key] = '';
      });
      state.orStart   = '';
      state.orEnd     = '';
      state.isDeposit = true;
    }

    const orStartEl  = document.getElementById('orStart');
    const orEndEl    = document.getElementById('orEnd');
    const depositCb  = document.getElementById('depositCheckbox');
    if (orStartEl) orStartEl.value  = '';
    if (orEndEl)   orEndEl.value    = '';
    if (depositCb) depositCb.checked = true;

    // Refresh all computed display values back to zero
    if (typeof window.crRecalculateTotals === 'function') {
      window.crRecalculateTotals();
    }
  }

  // ----------------------------------------------------------
  // 5. Close modal helper 
  // ----------------------------------------------------------
  function closeModal() {
    const overlay = document.getElementById('crOverlay');
    if (overlay) overlay.style.display = 'none';
  }

  // ----------------------------------------------------------
  // Result modal helpers
  // ----------------------------------------------------------
  function showResultModal(success, message) {
    const overlay  = document.getElementById('crResultOverlay');
    const title    = document.getElementById('crResultTitle');
    const msg      = document.getElementById('crResultMsg');
    const iconOk   = document.getElementById('crResultIconSuccess');
    const iconErr  = document.getElementById('crResultIconError');
    if (!overlay) return;

    // Set state class for colour theming
    overlay.classList.remove('cr-result--success', 'cr-result--error');
    overlay.classList.add(success ? 'cr-result--success' : 'cr-result--error');

    title.textContent = success ? 'Saved Successfully' : 'Save Failed';
    msg.textContent   = message;

    iconOk.style.display = success ? 'block' : 'none';
    iconErr.style.display = success ? 'none'  : 'block';

    overlay.style.display = 'flex';
  }

  function hideResultModal() {
    const overlay = document.getElementById('crResultOverlay');
    if (overlay) overlay.style.display = 'none';
  }

  // OK button dismisses the result modal
  const resultOkBtn = document.getElementById('crResultOkBtn');
  if (resultOkBtn) {
    resultOkBtn.addEventListener('click', hideResultModal);
  }

  // ----------------------------------------------------------
  // Save button wiring
  // ----------------------------------------------------------
  const saveBtn = document.getElementById('saveRemittanceBtn');
  if (!saveBtn) return;

  saveBtn.addEventListener('click', function () {
    if (!validateRemittance()) return;

    const payload = buildPayload();

    // Disable button + loading state
    saveBtn.disabled    = true;
    saveBtn.textContent = 'Saving\u2026';

    submitRemittance(payload)
      .then(function () {
        closeModal();
        resetRemittanceForm();
        showResultModal(true, 'The collection remittance has been recorded successfully.');
      })
      .catch(function (err) {
        console.error('[submitRemittance] error:', err);
        showResultModal(false, 'Something went wrong while saving. Please try again.');
      })
      .finally(function () {
        saveBtn.disabled    = false;
        saveBtn.textContent = 'Save';
      });
  });

  // ----------------------------------------------------------
  // Inject error styles once (avoids a separate CSS dependency)
  // ----------------------------------------------------------
  (function injectErrorStyles() {
    if (document.getElementById('cr-error-styles')) return;
    const style = document.createElement('style');
    style.id = 'cr-error-styles';
    style.textContent = [
      '.cr-field-error{',
        'display:none;',
        'font-size:.73rem;',
        'color:#dc2626;',
        'margin-top:3px;',
      '}',
      '.cr-input-error,',
      '.cr-input-error.cr-select{',
        'border-color:#dc2626 !important;',
        'box-shadow:0 0 0 3px rgba(220,38,38,.12) !important;',
      '}'
    ].join('');
    document.head.appendChild(style);
  }());

}());
