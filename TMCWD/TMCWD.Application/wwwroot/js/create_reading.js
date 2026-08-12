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
   loadCrTemplatesOnce();
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
//WALLY WORK HERE
// var METER_READERS = [
//   'Juan dela Cruz',
//   'Ana Lim',
//   'Liza Torres',
//   'Grace Aquino',
//   'Ben Ramos',
//   'Clara Bautista',
//   'Dennis Pascual',
//   'Elena Varga',
//   'Felix Soriano',
//   'Gloria Navarro',
//   'Hector Flores',
//   'Iris Magtanggol',
//   'Joel Manalo',
//   'Karen Dela Rosa',
//   'Leo Cabrera'
// ];

// const METER_READERS = loadMeterReaders();
// console.log('Meter Readers:', METER_READERS);
var METER_READERS = [];
loadMeterReaders();

/* Elements */
var crMeterReaderInput    = document.getElementById('crMeterReaderInput');
var crMeterReaderList     = document.getElementById('crMeterReaderList');
var crMeterReaderCombobox = document.getElementById('crMeterReaderCombobox');
var selectedZone = 0;
var selectedBook = 0;
var selectedScope = 0;
var currentFromSeq = 0;
var currentToSeq = 0;

/* Currently selected value */
var crMeterReaderValue = '';

async function loadMeterReaders() {
    METER_READERS = await fetch('/Admin/GetUsersByRole' + '?role=4', {
        method: 'GET',
        headers: { 
            'Content-Type': 'application/json'
        }
    }).then(response => {
        if (!response.ok || response.status == 204) return null;
            return response.json();
    }).then(result => {
        return result;
    });
}

/**
 * Render filtered options into the dropdown list.
 * @param {string} query
 */
function renderMeterReaderOptions(query) {
  var term = query.trim().toLowerCase();
  var filtered = METER_READERS.filter(function (data) {
    return data.name.toLowerCase().indexOf(term) !== -1;
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

  filtered.forEach(function (data) {
    var li = document.createElement('li');
    li.className = 'cr-combobox__option';
    li.textContent = data.name;
    li.data = data.id;
    li.setAttribute('role', 'option');
    li.setAttribute('aria-selected', data.name === crMeterReaderValue ? 'true' : 'false');

    li.addEventListener('mousedown', function (e) {
      // mousedown fires before input blur — prevent blur closing list first
      e.preventDefault();
      selectMeterReader(data);
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
function selectMeterReader(data) {
  console.log('Meter Reader Data:', data);
  crMeterReaderValue = data.id;
  crMeterReaderInput.dataset.id = data.id;
  crMeterReaderInput.value = data.name;
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
var crZone       = document.getElementById('crZone');
const crBook     = document.getElementById('crBook');

if (crZone) {
    crZone.addEventListener('change', async (evt) => {
        const newZone = evt.target.value;
        if (selectedZone == newZone) return;
        selectedZone = newZone;
        crAccountsLoaded = false;
        setLoadAccountsBtnState('load');
        if (crBook) {
            const books = await loadBooks(selectedZone);
            if (books) {
                crBook.replaceChildren();
                const noValOption = document.createElement('option');
                noValOption.value = 0;
                noValOption.textContent = 'Select Book...';
                crBook.appendChild(noValOption);
                [...books].forEach((book) => {
                    const option = document.createElement('option');
                    option.value = book.book;
                    option.textContent = 'Book ' + book.book;
                    crBook.appendChild(option);
                });
            }
        }
    });
}

if (crBook) {
    crBook.addEventListener('change', (evt) => {
        const newBook = evt.target.value;
        if (selectedBook == newBook) return;
        selectedBook = newBook;
        setLoadAccountsBtnState('load');
        crAccountsLoaded = false;
    });
}

if (crFromSeq) {
    crFromSeq.addEventListener('input', (evt) => {
        const newFrom = evt.target.value;
        if (currentFromSeq == newFrom) return;
        currentFromSeq = newFrom;
        setLoadAccountsBtnState('load');
        crAccountsLoaded = false;
    });
}

if (crToSeq) {
    crToSeq.addEventListener('input', (evt) => {
        const newTo = evt.target.value;
        if (currentToSeq == newTo) return;
        currentToSeq = newTo;
        setLoadAccountsBtnState('load');
        crAccountsLoaded = false;
    });
}

/**
 * load book based on selected zone
 */
async function loadBooks(zone) {
    return await fetch('/ZoneBook/GetBooksByZone?zone=' + zone, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json'
        }
    }).then(response => {
        if (!response.ok || response.status == 204) return null;
        return response.json();
    }).then(result => {
        return result;
    });
}

/**
 * load accounts based on selected zone, book, scope
 */

async function loadAccounts(zone, book, seqFrom, seqTo) {
    return await fetch('/account/GetByZoneBookAndSequence/?zone=' + zone + '&book=' + book + '&seqFrom=' + seqFrom + '&seqTo=' + seqTo, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json'
        }
    }).then(response => {
        if (!response.ok || response.status == 204) return null
        return response.json();
    }).then(result => {
        return result;
    });;
}

/**
 * Show or hide the sequence range inputs based on Scope value.
 */
function syncScopeRange() {
  if (!crScope || !crScopeRange) return;
  if (selectedScope == crScope.value) return;
  var isRanged = crScope.value === 'ranged';
  selectedScope = crScope.value;
  crScopeRange.hidden = !isRanged;
  setLoadAccountsBtnState('load');
  crAccountsLoaded = false;
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

var crLoadAccountsBtn   = document.getElementById('crLoadAccountsBtn');
var crCreateTemplateBtn = document.getElementById('crCreateTemplateBtn');
var crSaveBtn           = document.getElementById('crSaveBtn');
var crDeleteTemplateBtn = document.getElementById('crDeleteTemplateBtn');
var crEditTemplateBtn   = document.getElementById('crEditTemplateBtn');
var crTemplateActions   = document.getElementById('crTemplateActions');
var crToast             = document.getElementById('crToast');

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
        if (!validateCrDates()) {
            if (crDueDate && !crDueDate.checkValidity()) crDueDate.reportValidity();
            else if (crDisconnectionDate) crDisconnectionDate.reportValidity();
            return;
        }
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
   Phase 6 — Real Data
=============================================================== */

function loadCrAccounts() {
    var zone = (document.getElementById('crZone') || {}).value || '';
    var book = (document.getElementById('crBook') || {}).value || '';
    var scope = (document.getElementById('crScope') || {}).value || 'all';
    var from = parseInt((document.getElementById('crFromSeq') || {}).value, 10) || 0;
    var to = parseInt((document.getElementById('crToSeq') || {}).value, 10) || 0;

    var ACCOUNTS = [];

    var crTablePanel = document.getElementById('crTablePanel');
    var crAccountsTbody = document.getElementById('crAccountsTbody');
    var crTableEmpty = document.getElementById('crTableEmpty');
}

/**
 * Filter MOCK_ACCOUNTS by zone, book, and scope.
 * @param {string} zone  e.g. 'zone1' or '' for all
 * @param {string} book  e.g. 'book2' or '' for all
 * @param {string} scope 'all' | 'unbilled' | 'ranged'
 * @param {number} fromSeq
 * @param {number} toSeq
 * @returns {Array}
 */
async function filterAccounts(zone, book, scope, fromSeq, toSeq) {

    var customers = await fetch('/account/GetByZoneBookAndSequence?zone=' + zone + '&book=' + book + '&seqFrom=' + fromSeq + '&seqTo=' + toSeq, {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json'
        }
    }).then(response => {
        if (!response.ok || response.status == null) return null;
        return response.json();
    }).then(result => {
        return result;
    });
    
    return customers.filter(function (a) {
        if ((zone && a.zone == zone) && (book && a.book == book)) return true;
        if (scope === 'unbilled' && !a.billed) return true;
        if (scope === 'ranged')
            if ((fromSeq && a.sequence >= fromSeq) && (toSeq && a.sequence <= toSeq)) return true;
        //if (scope === 'ranged') {
        //  if (fromSeq && a.seq < fromSeq) return false;
        //  if (toSeq   && a.seq > toSeq)   return false;
    });
}

if (crLoadAccountsBtn) {
    crLoadAccountsBtn.addEventListener('click', function () {
        var panel = document.getElementById('crTablePanel');
        if (!panel) return;

        if (!crAccountsLoaded) {
            var zone = (document.getElementById('crZone') || {}).value || '';
            var book = (document.getElementById('crBook') || {}).value || '';
            if (!zone || !book) {
                alert('Select a Zone and Book first.');
                return;
            }
            loadCrAccounts();
        } else {
            panel.hidden = !panel.hidden;
            crLoadAccountsBtn.classList.toggle('is-active', !panel.hidden);
            setLoadAccountsBtnState(panel.hidden ? 'show' : 'hide');
        }
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
      console.log('A:', a);
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
      '<td class="cr-td cr-td--seq">' + a.sequence ?? 0 + '</td>';

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
  crLoadAccountsBtn.addEventListener('click', async function () {
    var panel = document.getElementById('crTablePanel');
    if (!panel) return;

    if (!crAccountsLoaded) {
      /* First click — filter and render */
      var zone  = (document.getElementById('crZone')  || {}).value || '';
      var book  = (document.getElementById('crBook')  || {}).value || '';
      var scope = (document.getElementById('crScope') || {}).value || 'all';
      var from  = parseInt((document.getElementById('crFromSeq') || {}).value, 10) || 0;
      var to    = parseInt((document.getElementById('crToSeq') || {}).value, 10) || 0;

      var accts = await filterAccounts(zone, book, scope, from, to);
      renderAccountsTable(accts);
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
}

/* ============================================================
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
      '<td class="cr-td cr-td--seq">' + a.sequence + '</td>';
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
   Phase 8 — Template selection logic (real data)
   ============================================================ */

var crRealTemplates = []; // populated via 'templates:updated' event from create_temp.js
var crRealZones = [];

var crTemplatesLoaded = false;

function loadCrTemplatesOnce() {
    return fetch('/ReadingSheet/GetReadingSheetTemplates')
        .then(function (res) { return res.ok ? res.json() : []; })
        .then(function (data) {
            crRealTemplates = data || [];
            renderCrTemplateOptions();
            crTemplatesLoaded = true;
        });
}

/* Re-grab elements (they were declared in Phase 5 but we need them here) */
var crBillingDate = document.getElementById('crBillingDate');
var crDueDate = document.getElementById('crDueDate');
var crDisconnectionDate = document.getElementById('crDisconnectionDate');

/* ============================================================
   Date validation — Due >= Billing, Disconnection >= Due
   ============================================================ */

function syncCrDateMins() {
    if (crDueDate) {
        crDueDate.min = crBillingDate && crBillingDate.value ? crBillingDate.value : '';
    }
    if (crDisconnectionDate) {
        crDisconnectionDate.min = crDueDate && crDueDate.value ? crDueDate.value : '';
    }
}

function validateCrDates() {
    var valid = true;

    if (crDueDate) crDueDate.setCustomValidity('');
    if (crDisconnectionDate) crDisconnectionDate.setCustomValidity('');

    if (crBillingDate && crDueDate && crBillingDate.value && crDueDate.value) {
        if (crDueDate.value < crBillingDate.value) {
            crDueDate.setCustomValidity('Due date cannot be before the billing date.');
            valid = false;
        }
    }

    if (crDueDate && crDisconnectionDate && crDueDate.value && crDisconnectionDate.value) {
        if (crDisconnectionDate.value < crDueDate.value) {
            crDisconnectionDate.setCustomValidity('Disconnection date cannot be before the due date.');
            valid = false;
        }
    }

    return valid;
}

if (crBillingDate) {
    crBillingDate.addEventListener('change', function () {
        syncCrDateMins();
        validateCrDates();
        if (crDueDate) crDueDate.reportValidity();
    });
}

if (crDueDate) {
    crDueDate.addEventListener('change', function () {
        syncCrDateMins();
        validateCrDates();
        crDueDate.reportValidity();
    });
}

if (crDisconnectionDate) {
    crDisconnectionDate.addEventListener('change', function () {
        validateCrDates();
        crDisconnectionDate.reportValidity();
    });
}

/* Set initial min values in case fields already have preset values on load */
syncCrDateMins();

function loadCrZonesOnce() {
    if (crRealZones.length) return Promise.resolve();
    return fetch('/ReadingSheet/GetZones')
        .then(function (res) { return res.ok ? res.json() : []; })
        .then(function (data) {
            crRealZones = data || [];
            var zoneSelect = document.getElementById('crZone');
            if (zoneSelect) {
                zoneSelect.innerHTML = '<option value="">Select Zone...</option>';
                crRealZones.forEach(function (z) {
                    var opt = document.createElement('option');
                    opt.value = z.value;
                    opt.textContent = z.label;
                    zoneSelect.appendChild(opt);
                });
            }
        });
}

function loadCrBooksForZone(zoneValue) {
    var bookSelect = document.getElementById('crBook');
    if (!bookSelect) return Promise.resolve();
    bookSelect.innerHTML = '<option value="">Select Book...</option>';
    if (!zoneValue) return Promise.resolve();
    return fetch('/ReadingSheet/GetBooksByZone?zone=' + encodeURIComponent(zoneValue))
        .then(function (res) { return res.ok ? res.json() : []; })
        .then(function (data) {
            (data || []).forEach(function (b) {
                var opt = document.createElement('option');
                opt.value = b.value;
                opt.textContent = b.label;
                bookSelect.appendChild(opt);
            });
        });
}

/* Real Zone select now drives real Book select for the main form too */
(function () {
    var zoneSelect = document.getElementById('crZone');
    if (zoneSelect) {
        zoneSelect.addEventListener('change', function () {
            loadCrBooksForZone(zoneSelect.value);
        });
    }
    loadCrZonesOnce();
})();

/**
 * Populate the header <select> and the quick-picker <ul> from
 * whatever templates create_temp.js currently has loaded.
 */
function renderCrTemplateOptions() {
    if (!crTemplateSelect) return;

    crTemplateSelect.innerHTML = '<option value="">Select Reading...</option>';
    crRealTemplates.forEach(function (t) {
        var opt = document.createElement('option');
        opt.value = t.id;
        opt.textContent = t.name;
        crTemplateSelect.appendChild(opt);
    });

    if (crTemplatesDropdown) {
        crTemplatesDropdown.innerHTML = '';
        crRealTemplates.forEach(function (t) {
            var li = document.createElement('li');
            li.className = 'cr-templates-dropdown__item';
            li.dataset.tpl = t.id;
            li.textContent = t.name;
            crTemplatesDropdown.appendChild(li);
        });
    }
}

document.addEventListener('templates:updated', function (e) {
    crRealTemplates = (e.detail && e.detail.templates) || [];
    renderCrTemplateOptions();
});

/**
 * Apply a template's values to the form.
 * Billing/Due/Disconnection dates are NOT part of the real template
 * yet (backend has no columns for them), so they are left untouched.
 *
 * @param {string} tplId  — matches a real template's id, or '' to clear
 */
function applyTemplate(tplId) {
    var tpl = crRealTemplates.find(function (t) { return String(t.id) === String(tplId); }) || null;

    if (tpl) {
        if (crMeterReaderInput) {
            crMeterReaderInput.value = tpl.readerName;
            crMeterReaderValue = tpl.readerName;
        }
        var zoneSelect = document.getElementById('crZone');
        if (zoneSelect) {
            zoneSelect.value = tpl.zone;
            loadCrBooksForZone(tpl.zone).then(function () {
                var bookSelect = document.getElementById('crBook');
                if (bookSelect) bookSelect.value = tpl.book;
                crAccountsLoaded = false; // force a fresh fetch for the new zone/book
                loadCrAccounts();
            });
        }
    }

    syncOverrideStart();

    if (crTemplateActions) crTemplateActions.hidden = !tpl;
}

if (crTemplateSelect) {
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

var crTemplatesMenuBtn = document.getElementById('crTemplatesMenuBtn');
var crTemplatesDropdown = document.getElementById('crTemplatesDropdown');

function toggleTemplatesDropdown() {
    if (!crTemplatesDropdown) return;
    crTemplatesDropdown.hidden = !crTemplatesDropdown.hidden;
}

function closeTemplatesDropdown() {
    if (crTemplatesDropdown) crTemplatesDropdown.hidden = true;
}

if (crTemplatesMenuBtn) {
    crTemplatesMenuBtn.addEventListener('click', function (e) {
        e.stopPropagation();
        toggleTemplatesDropdown();
    });
}

if (crTemplatesDropdown) {
    crTemplatesDropdown.addEventListener('click', function (e) {
        var item = e.target.closest('.cr-templates-dropdown__item');
        if (!item) return;

        var tplId = item.dataset.tpl;
        closeTemplatesDropdown();
        applyTemplate(tplId);
        if (crTemplateSelect) crTemplateSelect.value = tplId;
    });
}

/* Close dropdown when clicking anywhere outside */
document.addEventListener('click', async function (e) {
  if (crTemplatesMenuBtn && !crTemplatesMenuBtn.closest('.cr-header__templates-wrap').contains(e.target)) {
    closeTemplatesDropdown();
  }
});

var _origClose = closeCreateModal;

var closeCreateModal = function () {
    closeTemplatesDropdown();
    crAccountsLoaded = false;
    var panel = document.getElementById('crTablePanel');
    if (panel) panel.hidden = true;
    if (crLoadAccountsBtn) crLoadAccountsBtn.classList.remove('is-active');
    setLoadAccountsBtnState('load');
    _origClose();
};