/**
 * create_reading.js — Phase 1
 * Modal shell: open / close + dev-harness theme toggle.
 * Later phases append to this file — nothing here gets deleted.
 */

/* ============================================================
   ELEMENTS
   ============================================================ */

var crBackdrop     = document.getElementById('crBackdrop');
var crCloseBtn     = document.getElementById('crCloseBtn');
var openModalBtn   = document.getElementById('openModalBtn');   // dev harness
var toggleThemeBtn = document.getElementById('toggleThemeBtn'); // dev harness

/* ============================================================
   OPEN / CLOSE
   ============================================================ */

function openCreateModal() {
  crBackdrop.hidden = false;
  crBackdrop.setAttribute('aria-hidden', 'false');
}

function closeCreateModal() {
  crBackdrop.hidden = true;
  crBackdrop.setAttribute('aria-hidden', 'true');
}

/* ============================================================
   EVENTS
   ============================================================ */

// Close button
crCloseBtn.addEventListener('click', closeCreateModal);

// Click outside the modal card
crBackdrop.addEventListener('click', function (e) {
  if (e.target === crBackdrop) closeCreateModal();
});

// Escape key
document.addEventListener('keydown', function (e) {
  if (e.key === 'Escape') closeCreateModal();
});

// Dev harness — open
if (openModalBtn) {
  openModalBtn.addEventListener('click', openCreateModal);
}

// Dev harness — theme toggle (mirrors appshell.js handleThemeToggle)
if (toggleThemeBtn) {
  toggleThemeBtn.addEventListener('click', function () {
    var html    = document.documentElement;
    var current = html.getAttribute('data-theme') || 'light';
    html.setAttribute('data-theme', current === 'dark' ? 'light' : 'dark');
  });
}

/* ============================================================
   Phase 2 — Override Start Date checkbox
   ============================================================ */

var crOverrideStart      = document.getElementById('crOverrideStart');
var crBillingPeriodStart = document.getElementById('crBillingPeriodStart');

/**
 * Sync the disabled state of Billing Period Start with the checkbox.
 */
function syncOverrideStart() {
  if (!crOverrideStart || !crBillingPeriodStart) return;
  crBillingPeriodStart.disabled = !crOverrideStart.checked;
}

if (crOverrideStart) {
  crOverrideStart.addEventListener('change', syncOverrideStart);
  // Set initial state (unchecked → disabled)
  syncOverrideStart();
}

/* ============================================================
   Phase 3 — Meter Reader searchable combobox
   ============================================================ */

/* Mock data — 15 meter readers */
var METER_READERS = [
  'Juan dela Cruz',
  'Ana Lim',
  'Liza Torres',
  'Grace Aquino',
  'Ben Ramos',
  'Clara Bautista',
  'Dennis Pascual',
  'Elena Varga',
  'Felix Soriano',
  'Gloria Navarro',
  'Hector Flores',
  'Iris Magtanggol',
  'Joel Manalo',
  'Karen Dela Rosa',
  'Leo Cabrera'
];

/* Elements */
var crMeterReaderInput    = document.getElementById('crMeterReaderInput');
var crMeterReaderList     = document.getElementById('crMeterReaderList');
var crMeterReaderCombobox = document.getElementById('crMeterReaderCombobox');

/* Currently selected value */
var crMeterReaderValue = '';

/**
 * Render filtered options into the dropdown list.
 * @param {string} query
 */
function renderMeterReaderOptions(query) {
  var term = query.trim().toLowerCase();
  var filtered = METER_READERS.filter(function (name) {
    return name.toLowerCase().indexOf(term) !== -1;
  });

  crMeterReaderList.innerHTML = '';

  if (filtered.length === 0) {
    var empty = document.createElement('li');
    empty.className = 'cr-combobox__option cr-combobox__option--empty';
    empty.textContent = 'No results found.';
    empty.setAttribute('role', 'option');
    crMeterReaderList.appendChild(empty);
    return;
  }

  filtered.forEach(function (name) {
    var li = document.createElement('li');
    li.className = 'cr-combobox__option';
    li.textContent = name;
    li.setAttribute('role', 'option');
    li.setAttribute('aria-selected', name === crMeterReaderValue ? 'true' : 'false');

    li.addEventListener('mousedown', function (e) {
      // mousedown fires before input blur — prevent blur closing list first
      e.preventDefault();
      selectMeterReader(name);
    });

    crMeterReaderList.appendChild(li);
  });
}

/**
 * Open the dropdown.
 */
function openMeterReaderList() {
  renderMeterReaderOptions(crMeterReaderInput.value);
  crMeterReaderList.hidden = false;
  crMeterReaderInput.setAttribute('aria-expanded', 'true');
}

/**
 * Close the dropdown.
 */
function closeMeterReaderList() {
  crMeterReaderList.hidden = true;
  crMeterReaderInput.setAttribute('aria-expanded', 'false');
}

/**
 * Select a meter reader, update input value, close list.
 * @param {string} name
 */
function selectMeterReader(name) {
  crMeterReaderValue = name;
  crMeterReaderInput.value = name;
  closeMeterReaderList();
}

/* Events */
if (crMeterReaderInput) {

  crMeterReaderInput.addEventListener('focus', function () {
    openMeterReaderList();
  });

  crMeterReaderInput.addEventListener('input', function () {
    crMeterReaderValue = ''; // clear selection if user edits
    openMeterReaderList();
  });

  crMeterReaderInput.addEventListener('blur', function () {
    // Small delay so mousedown on an option fires first
    setTimeout(closeMeterReaderList, 150);
  });

  crMeterReaderInput.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
      closeMeterReaderList();
      crMeterReaderInput.blur();
    }
  });
}

/* Close list if clicking outside the combobox */
document.addEventListener('click', function (e) {
  if (crMeterReaderCombobox && !crMeterReaderCombobox.contains(e.target)) {
    closeMeterReaderList();
  }
});

/* ============================================================
   Phase 4 — Scope dropdown → reveal / hide sequence range
   ============================================================ */

var crScope      = document.getElementById('crScope');
var crScopeRange = document.getElementById('crScopeRange');
var crFromSeq    = document.getElementById('crFromSeq');
var crToSeq      = document.getElementById('crToSeq');

/**
 * Show or hide the sequence range inputs based on Scope value.
 */
function syncScopeRange() {
  if (!crScope || !crScopeRange) return;
  var isRanged = crScope.value === 'ranged';
  crScopeRange.hidden = !isRanged;

  // Clear values when hidden so they don't accidentally submit
  if (!isRanged) {
    if (crFromSeq) crFromSeq.value = '';
    if (crToSeq)   crToSeq.value   = '';
  }
}

if (crScope) {
  crScope.addEventListener('change', syncScopeRange);
  // Initial state
  syncScopeRange();
}

/* ============================================================
   Phase 5 — Action buttons + toast
   ============================================================ */

var crLoadAccountsBtn  = document.getElementById('crLoadAccountsBtn');
var crCreateTemplateBtn = document.getElementById('crCreateTemplateBtn');
var crSaveBtn          = document.getElementById('crSaveBtn');
var crDeleteTemplateBtn = document.getElementById('crDeleteTemplateBtn');
var crEditTemplateBtn  = document.getElementById('crEditTemplateBtn');
var crTemplateActions  = document.getElementById('crTemplateActions');
var crToast            = document.getElementById('crToast');

var crToastTimer = null;

/**
 * Show the success toast for `duration` ms then hide it.
 * @param {string} message
 * @param {number} [duration=2800]
 */
function showToast(message, duration) {
  if (!crToast) return;
  clearTimeout(crToastTimer);
  crToast.textContent = message;
  crToast.hidden = false;
  crToastTimer = setTimeout(function () {
    crToast.hidden = true;
  }, duration || 2800);
}

/* Load Accounts — wired after Phase 7 where renderAccountsTable is defined */
var crAccountsLoaded = false;

/* Save — show toast then close modal */
if (crSaveBtn) {
  crSaveBtn.addEventListener('click', function () {
    showToast('Reading sheet saved successfully');
    setTimeout(closeCreateModal, 1000);
  });
}

/* Delete Template — stub */
if (crDeleteTemplateBtn) {
  crDeleteTemplateBtn.addEventListener('click', function () {
    alert('Delete Template — coming in a later phase.');
  });
}

/* Edit Template — stub */
if (crEditTemplateBtn) {
  crEditTemplateBtn.addEventListener('click', function () {
    alert('Edit Template — coming in a later phase.');
  });
}

/* Show / hide Delete+Edit template buttons based on header dropdown */
var crTemplateSelect = document.getElementById('crTemplateSelect');
if (crTemplateSelect) {
  crTemplateSelect.addEventListener('change', function () {
    var hasTemplate = crTemplateSelect.value !== '';
    if (crTemplateActions) crTemplateActions.hidden = !hasTemplate;
  });
}

/* ============================================================
   Phase 6 — Accounts mock data + table rendering
   ============================================================ */

var MOCK_ACCOUNTS = [
  { acctNo: '2024-0001', name: 'Maria Santos',       address: 'Purok 3',  barangay: 'Brgy. De Ocampo',   type: 'Residential', zone: 'zone1', book: 'book1', billed: true,  seq: 1  },
  { acctNo: '2024-0002', name: 'Roberto Reyes',      address: 'Purok 7',  barangay: 'Brgy. Hugo Perez',  type: 'Residential', zone: 'zone1', book: 'book1', billed: false, seq: 2  },
  { acctNo: '2024-0003', name: 'Carlo Mendoza',      address: 'Purok 1',  barangay: 'Brgy. Lapidario',   type: 'Commercial',  zone: 'zone1', book: 'book2', billed: true,  seq: 3  },
  { acctNo: '2024-0004', name: 'Dante Villanueva',   address: 'Purok 5',  barangay: 'Brgy. Conchu',      type: 'Residential', zone: 'zone2', book: 'book2', billed: false, seq: 4  },
  { acctNo: '2024-0005', name: 'Noel Castillo',      address: 'Purok 2',  barangay: 'Brgy. Gregorio',    type: 'Residential', zone: 'zone2', book: 'book3', billed: true,  seq: 5  },
  { acctNo: '2024-0006', name: 'Rachel Domingo',     address: 'Purok 9',  barangay: 'Brgy. Aguado',      type: 'Industrial',  zone: 'zone2', book: 'book3', billed: false, seq: 6  },
  { acctNo: '2024-0007', name: 'Samuel Ong',         address: 'Purok 4',  barangay: 'Brgy. Cabezas',     type: 'Residential', zone: 'zone3', book: 'book4', billed: true,  seq: 7  },
  { acctNo: '2024-0008', name: 'Teresa Padilla',     address: 'Purok 6',  barangay: 'Brgy. Luciano',     type: 'Residential', zone: 'zone3', book: 'book4', billed: false, seq: 8  },
  { acctNo: '2024-0009', name: 'Ulysses Tan',        address: 'Purok 11', barangay: 'Brgy. Sanchez',     type: 'Commercial',  zone: 'zone3', book: 'book5', billed: true,  seq: 9  },
  { acctNo: '2024-0010', name: 'Vera Lim',           address: 'Purok 8',  barangay: 'Brgy. Osorio',      type: 'Residential', zone: 'zone4', book: 'book5', billed: false, seq: 10 },
  { acctNo: '2024-0011', name: 'Walter Gomez',       address: 'Purok 10', barangay: 'Brgy. Santiago',    type: 'Residential', zone: 'zone4', book: 'book6', billed: true,  seq: 11 },
  { acctNo: '2024-0012', name: 'Xena Fuentes',       address: 'Purok 13', barangay: 'Brgy. Imelda',      type: 'Commercial',  zone: 'zone4', book: 'book6', billed: false, seq: 12 }
];

var crTablePanel  = document.getElementById('crTablePanel');
var crAccountsTbody = document.getElementById('crAccountsTbody');
var crTableEmpty  = document.getElementById('crTableEmpty');

/**
 * Filter MOCK_ACCOUNTS by zone, book, and scope.
 * @param {string} zone  e.g. 'zone1' or '' for all
 * @param {string} book  e.g. 'book2' or '' for all
 * @param {string} scope 'all' | 'unbilled' | 'ranged'
 * @param {number} fromSeq
 * @param {number} toSeq
 * @returns {Array}
 */
function filterAccounts(zone, book, scope, fromSeq, toSeq) {
  return MOCK_ACCOUNTS.filter(function (a) {
    if (zone && a.zone !== zone) return false;
    if (book && a.book !== book) return false;
    if (scope === 'unbilled' && a.billed) return false;
    if (scope === 'ranged') {
      if (fromSeq && a.seq < fromSeq) return false;
      if (toSeq   && a.seq > toSeq)   return false;
    }
    return true;
  });
}

/**
 * Render filtered accounts into the table body.
 * @param {Array} accounts
 */
function renderAccountsTable(accounts) {
  if (!crAccountsTbody) return;

  crAccountsTbody.innerHTML = '';

  if (accounts.length === 0) {
    crTableEmpty.hidden  = false;
    crTablePanel.hidden  = false;
    return;
  }

  crTableEmpty.hidden = true;
  crTablePanel.hidden = false;

  accounts.forEach(function (a, idx) {
    var tr = document.createElement('tr');

    tr.innerHTML =
      '<td class="cr-td cr-td--num">' + (idx + 1) + '</td>' +
      '<td class="cr-td">' +
        '<span class="cr-cell-main">' + a.acctNo + '</span>' +
        '<span class="cr-cell-sub">'  + a.name   + '</span>' +
      '</td>' +
      '<td class="cr-td">' +
        '<span class="cr-cell-main">' + a.address   + '</span>' +
        '<span class="cr-cell-sub">'  + a.barangay  + '</span>' +
      '</td>' +
      '<td class="cr-td">' +
        '<span class="cr-type-badge">' + a.type + '</span>' +
      '</td>' +
      '<td class="cr-td cr-td--seq">' + a.seq + '</td>';

    crAccountsTbody.appendChild(tr);
  });
}

/* Re-wire Load Accounts button to use the table */
/* (wiring is now handled in Phase 5 section above) */

/* ============================================================
   Load Accounts — wire button after all dependencies are ready
   ============================================================ */

CR_PAGE_SIZE = 4;

/* SVG icons for the Load Accounts button states */
var CR_ICON_LOAD =
  '<svg viewBox="0 0 16 16" fill="none" aria-hidden="true">' +
    '<path d="M8 2v8M5 7l3 3 3-3" stroke="currentColor" stroke-width="1.4"' +
    '      stroke-linecap="round" stroke-linejoin="round"/>' +
    '<path d="M2 12h12" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/>' +
  '</svg>';

var CR_ICON_HIDE =
  '<svg viewBox="0 0 16 16" fill="none" aria-hidden="true">' +
    '<path d="M8 14V6M5 9l3-3 3 3" stroke="currentColor" stroke-width="1.4"' +
    '      stroke-linecap="round" stroke-linejoin="round"/>' +
    '<path d="M2 4h12" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/>' +
  '</svg>';

var CR_ICON_SHOW =
  '<svg viewBox="0 0 16 16" fill="none" aria-hidden="true">' +
    '<path d="M2 5l6 6 6-6" stroke="currentColor" stroke-width="1.4"' +
    '      stroke-linecap="round" stroke-linejoin="round"/>' +
  '</svg>';

/**
 * Update the Load Accounts button label and icon.
 * @param {'load'|'hide'|'show'} state
 */
function setLoadAccountsBtnState(state) {
  if (!crLoadAccountsBtn) return;
  var icon, label;
  if (state === 'hide') {
    icon  = CR_ICON_HIDE;
    label = 'Hide Accounts';
  } else if (state === 'show') {
    icon  = CR_ICON_SHOW;
    label = 'Show Accounts';
  } else {
    icon  = CR_ICON_LOAD;
    label = 'Load Accounts';
  }
  crLoadAccountsBtn.innerHTML = icon + label;
}

if (crLoadAccountsBtn) {
  crLoadAccountsBtn.addEventListener('click', function () {
    var panel = document.getElementById('crTablePanel');
    if (!panel) return;

    if (!crAccountsLoaded) {
      /* First click — filter and render */
      var zone  = (document.getElementById('crZone')  || {}).value || '';
      var book  = (document.getElementById('crBook')  || {}).value || '';
      var scope = (document.getElementById('crScope') || {}).value || 'all';
      var from  = parseInt((document.getElementById('crFromSeq') || {}).value, 10) || 0;
      var to    = parseInt((document.getElementById('crToSeq')   || {}).value, 10) || 0;

      renderAccountsTable(filterAccounts(zone, book, scope, from, to));
      crAccountsLoaded = true;
      crLoadAccountsBtn.classList.add('is-active');
      setLoadAccountsBtnState('hide');
    } else {
      /* Subsequent clicks — toggle panel visibility */
      panel.hidden = !panel.hidden;
      crLoadAccountsBtn.classList.toggle('is-active', !panel.hidden);
      setLoadAccountsBtnState(panel.hidden ? 'show' : 'hide');
    }
  });
}/* ============================================================
   Phase 7 — Pagination
   ============================================================ */

var CR_PAGE_SIZE = 4;

var crPagination  = document.getElementById('crPagination');
var crPagSummary  = document.getElementById('crPagSummary');
var crPagControls = document.getElementById('crPagControls');
var crPrevBtn     = document.getElementById('crPrevBtn');
var crNextBtn     = document.getElementById('crNextBtn');

/* Pagination state — set when renderAccountsTable is called */
var crFilteredAccounts = [];
var crCurrentPage      = 1;

/**
 * Slice one page from the filtered accounts array.
 * @param {number} page  1-based
 * @returns {Array}
 */
function crPageSlice(page) {
  var start = (page - 1) * CR_PAGE_SIZE;
  return crFilteredAccounts.slice(start, start + CR_PAGE_SIZE);
}

/**
 * Render pagination controls and the current page of rows.
 */
function renderCrPagination() {
  var total      = crFilteredAccounts.length;
  var totalPages = Math.max(1, Math.ceil(total / CR_PAGE_SIZE));
  var page       = crCurrentPage;
  var start      = total === 0 ? 0 : (page - 1) * CR_PAGE_SIZE + 1;
  var end        = Math.min(page * CR_PAGE_SIZE, total);

  /* Summary label */
  if (crPagSummary) {
    crPagSummary.textContent = 'Showing ' + start + '–' + end + ' of ' + total + ' records';
  }

  /* Remove old page-number buttons */
  if (crPagControls) {
    crPagControls.querySelectorAll('.cr-pag-page').forEach(function (b) { b.remove(); });
  }

  /* Insert page-number buttons between Prev and Next */
  for (var p = 1; p <= totalPages; p++) {
    var btn = document.createElement('button');
    btn.type      = 'button';
    btn.className = 'cr-pag-page' + (p === page ? ' is-current' : '');
    btn.textContent = String(p);
    btn.dataset.page = String(p);
    if (p === page) btn.setAttribute('aria-current', 'page');
    crPagControls.insertBefore(btn, crNextBtn);
  }

  if (crPrevBtn) crPrevBtn.disabled = page <= 1;
  if (crNextBtn) crNextBtn.disabled = page >= totalPages;

  if (crPagination) crPagination.hidden = total === 0;

  /* Re-render table rows for this page */
  renderCrTableRows(crPageSlice(page));
}

/**
 * Render only the <tbody> rows (no pagination logic).
 * Called by renderCrPagination — separated so Phase 6's
 * renderAccountsTable can delegate here.
 * @param {Array} rows
 */
function renderCrTableRows(rows) {
  if (!crAccountsTbody) return;
  crAccountsTbody.innerHTML = '';

  rows.forEach(function (a, idx) {
    var globalIdx = (crCurrentPage - 1) * CR_PAGE_SIZE + idx + 1;
    var tr = document.createElement('tr');
    tr.innerHTML =
      '<td class="cr-td cr-td--num">' + globalIdx + '</td>' +
      '<td class="cr-td">' +
        '<span class="cr-cell-main">' + a.acctNo + '</span>' +
        '<span class="cr-cell-sub">'  + a.name   + '</span>' +
      '</td>' +
      '<td class="cr-td">' +
        '<span class="cr-cell-main">' + a.address  + '</span>' +
        '<span class="cr-cell-sub">'  + a.barangay + '</span>' +
      '</td>' +
      '<td class="cr-td">' +
        '<span class="cr-type-badge">' + a.type + '</span>' +
      '</td>' +
      '<td class="cr-td cr-td--seq">' + a.seq + '</td>';
    crAccountsTbody.appendChild(tr);
  });
}

/* Override Phase 6's renderAccountsTable to use pagination */
renderAccountsTable = function (accounts) {
  crFilteredAccounts = accounts;
  crCurrentPage      = 1;

  if (!crTablePanel) return;
  crTablePanel.hidden = false;

  if (accounts.length === 0) {
    if (crTableEmpty)  crTableEmpty.hidden  = false;
    if (crPagination)  crPagination.hidden  = true;
    if (crAccountsTbody) crAccountsTbody.innerHTML = '';
    return;
  }

  if (crTableEmpty) crTableEmpty.hidden = true;
  renderCrPagination();
};

/* Prev / Next / page-number click delegation */
if (crPrevBtn) {
  crPrevBtn.addEventListener('click', function () {
    if (crCurrentPage > 1) {
      crCurrentPage--;
      renderCrPagination();
    }
  });
}

if (crNextBtn) {
  crNextBtn.addEventListener('click', function () {
    var totalPages = Math.ceil(crFilteredAccounts.length / CR_PAGE_SIZE);
    if (crCurrentPage < totalPages) {
      crCurrentPage++;
      renderCrPagination();
    }
  });
}

if (crPagControls) {
  crPagControls.addEventListener('click', function (e) {
    var btn = e.target.closest('.cr-pag-page');
    if (!btn || btn.classList.contains('is-current')) return;
    crCurrentPage = parseInt(btn.dataset.page, 10);
    renderCrPagination();
  });
}

/* ============================================================
   Phase 8 — Template selection logic
   ============================================================ */

/**
 * Mock template data.
 * Keys must match the <option value="..."> in the header dropdown.
 * Dates are in yyyy-MM-dd format to match <input type="date">.
 */
var MOCK_TEMPLATES = {
  'tpl_monthly': {
    label:              'Monthly Standard',
    billingDate:        '2025-07-01',
    dueDate:            '2025-07-15',
    disconnectionDate:  '2025-08-01',
    billingPeriodStart: '2025-07-01'
  },
  'tpl_quarterly': {
    label:              'Quarterly Round',
    billingDate:        '2025-07-01',
    dueDate:            '2025-07-31',
    disconnectionDate:  '2025-09-01',
    billingPeriodStart: '2025-04-01'
  },
  'tpl_special': {
    label:              'Special Reading',
    billingDate:        '2025-07-10',
    dueDate:            '2025-07-20',
    disconnectionDate:  '2025-08-10',
    billingPeriodStart: '2025-07-10'
  }
};

/* Re-grab elements (they were declared in Phase 5 but we need them here) */
var crBillingDate       = document.getElementById('crBillingDate');
var crDueDate           = document.getElementById('crDueDate');
var crDisconnectionDate = document.getElementById('crDisconnectionDate');

/**
 * Apply a template's values to the date fields, then
 * re-run the override-start sync so the lock state is correct.
 *
 * @param {string} tplKey  — key into MOCK_TEMPLATES, or '' to clear
 */
function applyTemplate(tplKey) {
  var tpl = MOCK_TEMPLATES[tplKey] || null;

  /* Fill or clear the four date fields */
  if (crBillingDate)       crBillingDate.value       = tpl ? tpl.billingDate        : '';
  if (crDueDate)           crDueDate.value           = tpl ? tpl.dueDate            : '';
  if (crDisconnectionDate) crDisconnectionDate.value = tpl ? tpl.disconnectionDate  : '';
  if (crBillingPeriodStart) crBillingPeriodStart.value = tpl ? tpl.billingPeriodStart : '';

  /* Re-apply the override checkbox state so the field is correctly
     enabled/disabled regardless of what the template filled in.      */
  syncOverrideStart();

  /* Show or hide the Delete / Edit template buttons */
  if (crTemplateActions) crTemplateActions.hidden = !tpl;
}

/* Re-wire the template dropdown (was partially wired in Phase 5
   only for the button visibility — now replace with full logic)   */
if (crTemplateSelect) {
  /* Clone to drop the Phase 5 change listener */
  var freshTemplateSelect = crTemplateSelect.cloneNode(true);
  crTemplateSelect.parentNode.replaceChild(freshTemplateSelect, crTemplateSelect);
  crTemplateSelect = freshTemplateSelect;

  crTemplateSelect.addEventListener('change', function () {
    applyTemplate(crTemplateSelect.value);
  });
}

/* ============================================================
   Templates quick-picker dropdown
   ============================================================ */

var crTemplatesMenuBtn   = document.getElementById('crTemplatesMenuBtn');
var crTemplatesDropdown  = document.getElementById('crTemplatesDropdown');

/**
 * Toggle the Templates dropdown open/closed.
 */
function toggleTemplatesDropdown() {
  if (!crTemplatesDropdown) return;
  crTemplatesDropdown.hidden = !crTemplatesDropdown.hidden;
}

/**
 * Close the Templates dropdown.
 */
function closeTemplatesDropdown() {
  if (crTemplatesDropdown) crTemplatesDropdown.hidden = true;
}

if (crTemplatesMenuBtn) {
  crTemplatesMenuBtn.addEventListener('click', function (e) {
    e.stopPropagation();
    toggleTemplatesDropdown();
  });
}

/* Clicking an item applies the template and syncs the header select */
if (crTemplatesDropdown) {
  crTemplatesDropdown.addEventListener('click', function (e) {
    var item = e.target.closest('.cr-templates-dropdown__item');
    if (!item) return;

    var tplKey = item.dataset.tpl;
    closeTemplatesDropdown();

    /* Apply the template dates */
    applyTemplate(tplKey);

    /* Sync the header <select> to match */
    if (crTemplateSelect) {
      crTemplateSelect.value = tplKey;
    }
  });
}

/* Close dropdown when clicking anywhere outside */
document.addEventListener('click', function (e) {
  if (crTemplatesMenuBtn && !crTemplatesMenuBtn.closest('.cr-header__templates-wrap').contains(e.target)) {
    closeTemplatesDropdown();
  }
});

/* Close dropdown when modal closes */
var _origClose = closeCreateModal;
closeCreateModal = function () {
  closeTemplatesDropdown();
  /* Reset Load Accounts state */
  crAccountsLoaded = false;
  var panel = document.getElementById('crTablePanel');
  if (panel) panel.hidden = true;
  if (crLoadAccountsBtn) crLoadAccountsBtn.classList.remove('is-active');
  setLoadAccountsBtnState('load');
  _origClose();
};
