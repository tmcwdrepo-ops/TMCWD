/**
 * reading.js — Present Reading Creation & Table Management
 * Account Number field: live search (debounced) populates table below.
 * Clicking a table row fills the form. Zone+Book filter loads readings separately.
 */

(function () {
  'use strict';

  // ─── State ───────────────────────────────────────────────────────────────────
  let readingsList  = [];   // rows currently shown in the table
  let searchCache   = {};   // query → result cache to avoid redundant requests
  let currentPage   = 1;
  let pageSize      = 5;
  let debounceTimer = null;
  let currentReadingSheetId = 0;
  let scheduleRequestSequence = 0;

  // ─── Boot ─────────────────────────────────────────────────────────────────────
  document.addEventListener('DOMContentLoaded', function () {
    initFormDefaults();
    loadZones();
    loadReaders();
    bindAccountSearch();
    bindZoneBookFilters();
    bindReadingCalculations();
    bindFormSubmission();
    bindPagination();
    renderTable();
  });

  // ─── Load zones, books, readers from DB ──────────────────────────────────────
  async function loadZones() {
    const zoneSelect = document.getElementById('zoneSelect');
    if (!zoneSelect) return;

    try {
      const res = await fetch('/ZoneBook/GetZone');
      if (!res.ok) return;
      const zones = await res.json();

      // GetZones returns distinct ZoneBook records — extract unique zone numbers
      const seen = new Set();
      zones.forEach(z => {
        if (seen.has(z.zone)) return;
        seen.add(z.zone);
        const opt = document.createElement('option');
        opt.value = z.zone;
        opt.textContent = `Zone ${String(z.zone).padStart(2, '0')}`;
        zoneSelect.appendChild(opt);
      });
    } catch (err) {
      console.error('Failed to load zones:', err);
    }
  }

  async function loadBooksByZone(zone) {
    const bookSelect = document.getElementById('bookSelect');
    if (!bookSelect) return;

    // Reset books
    bookSelect.innerHTML = '<option value="">Select Book</option>';
    bookSelect.disabled = true;

    if (!zone) return;

    try {
      const res = await fetch(`/ZoneBook/GetBooksByZone?zone=${zone}`);
      if (!res.ok) return;
      const books = await res.json();

      books.forEach(b => {
        const opt = document.createElement('option');
        opt.value = b.book;
        opt.textContent = `Book ${String(b.book).padStart(2, '0')}`;
        bookSelect.appendChild(opt);
      });
      bookSelect.disabled = false;
    } catch (err) {
      console.error('Failed to load books:', err);
    }
  }

  async function loadReaders() {
    const readerSelect = document.getElementById('readerSelect');
    if (!readerSelect) return;

    try {
      const res = await fetch('/ReadingSheet/GetReaders');
      if (!res.ok) return;
      const readers = await res.json();

      readers.forEach(r => {
        const opt = document.createElement('option');
        opt.value = r.id;
        opt.textContent = r.name;
        readerSelect.appendChild(opt);
      });
    } catch (err) {
      console.error('Failed to load readers:', err);
    }
  }

  // ─── Default dates ────────────────────────────────────────────────────────────
  function initFormDefaults() {
    ['billingDate', 'dueDate'].forEach(id => {
      const input = document.getElementById(id);
      if (!input) return;
      input.value = '';
      input.readOnly = true;
      input.title = 'This date is supplied by the selected reading sheet.';
    });

    const readingDate = document.getElementById('readingDate');
    if (readingDate) {
      readingDate.value = '';
      readingDate.readOnly = false;
      readingDate.title = 'Choose the actual date the meter reading was taken.';
    }
  }

  function dateInputValue(value) {
    if (!value) return '';
    const match = String(value).match(/^\d{4}-\d{2}-\d{2}/);
    if (match) return match[0];
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return '';
    return [
      parsed.getFullYear(),
      String(parsed.getMonth() + 1).padStart(2, '0'),
      String(parsed.getDate()).padStart(2, '0')
    ].join('-');
  }

  function clearReadingSheetSchedule() {
    currentReadingSheetId = 0;
    ['readingDate', 'billingDate', 'dueDate'].forEach(id => {
      const input = document.getElementById(id);
      if (input) input.value = '';
    });
    const readingDate = document.getElementById('readingDate');
    if (readingDate) readingDate.max = '';
  }

  async function loadReadingSheetSchedule() {
    const zone = parseInt(document.getElementById('zoneSelect')?.value) || 0;
    const book = parseInt(document.getElementById('bookSelect')?.value) || 0;
    const assignedTo = parseInt(document.getElementById('readerSelect')?.value) || 0;
    const requestSequence = ++scheduleRequestSequence;

    clearReadingSheetSchedule();
    if (!zone || !book || !assignedTo) return;

    try {
      const sheet = await fetchActiveReadingSheet(zone, book, assignedTo);
      if (requestSequence !== scheduleRequestSequence) return;
      if (!sheet) return;
      const readingDate = dateInputValue(sheet.readingDate);
      const billingDate = dateInputValue(sheet.billingDate);
      const dueDate = dateInputValue(sheet.dueDate);
      if (!sheet.id || !readingDate || !billingDate || !dueDate) return;

      currentReadingSheetId = Number(sheet.id);
      document.getElementById('readingDate').value = readingDate;
      document.getElementById('readingDate').max = addDaysToInputDate(billingDate, -1);
      document.getElementById('billingDate').value = billingDate;
      document.getElementById('dueDate').value = dueDate;
    } catch (err) {
      if (requestSequence === scheduleRequestSequence) clearReadingSheetSchedule();
      console.error('Failed to load the reading-sheet schedule:', err);
    }
  }

  async function fetchActiveReadingSheet(zone, book, assignedTo) {
    const response = await fetch(
      `/ReadingSheet/GetActiveForManualReading?zone=${zone}&book=${book}&assignedTo=${assignedTo}`
    );
    if (response.ok) return response.json();

    // Compatibility fallback for a web process that has not yet restarted
    // with GetActiveForManualReading. GetAllReadingSheet is already available.
    const fallbackResponse = await fetch('/ReadingSheet/GetAllReadingSheet');
    if (!fallbackResponse.ok) return null;

    const sheets = await fallbackResponse.json();
    const active = sheets
      .filter(sheet => Number(sheet.zone) === zone
        && Number(sheet.book) === book
        && Number(sheet.assignedTo) === assignedTo
        && (Number(sheet.status) === 1 || Number(sheet.status) === 2)
        && sheet.billingPeriodStart
        && sheet.billingDate
        && sheet.dueDate)
      .sort((left, right) => new Date(right.billingDate) - new Date(left.billingDate))[0];

    return active ? {
      id: active.id,
      readingDate: active.billingPeriodStart,
      billingDate: active.billingDate,
      dueDate: active.dueDate
    } : null;
  }

  function addDaysToInputDate(value, days) {
    const parts = value.split('-').map(Number);
    const date = new Date(parts[0], parts[1] - 1, parts[2]);
    date.setDate(date.getDate() + days);
    return [
      date.getFullYear(),
      String(date.getMonth() + 1).padStart(2, '0'),
      String(date.getDate()).padStart(2, '0')
    ].join('-');
  }

  // ─── Live account search ──────────────────────────────────────────────────────
  function bindAccountSearch() {
    const input = document.getElementById('accountNumber');
    if (!input) return;

    input.setAttribute('autocomplete', 'off');

    // Enter key — immediately search and auto-fill without waiting for debounce
    input.addEventListener('keydown', function (e) {
      if (e.key !== 'Enter') return;
      e.preventDefault();
      const q = this.value.trim();
      if (!q) return;
      clearTimeout(debounceTimer);
      performSearch(q);
    });

    input.addEventListener('input', async function () {
      clearTimeout(debounceTimer);
      const q = this.value.trim();

      // Clear form fields whenever the user edits the input
      clearFormFields();
      calculateUsageAndAmount();

      if (!q) {
        currentPage  = 1;
        const zone = document.getElementById('zoneSelect')?.value;
        const book = document.getElementById('bookSelect')?.value;

        if (zone && book) {
          await loadReadingsByZoneBook();
        } else {
          readingsList = [];
        }

        renderTable();
        return;
      }

      debounceTimer = setTimeout(() => performSearch(q), 300);
    });
  }

  async function performSearch(q) {
    // Return cached result immediately
    const cacheKey = `${q}|${currentReadingSheetId}`;
    if (searchCache[cacheKey]) {
      readingsList = searchCache[cacheKey];
      currentPage  = 1;
      renderTable();

      // If the query is an exact match on one result, auto-fill the form
      autoFillIfExactMatch(q, readingsList);
      return;
    }

    try {
      const searchUrl = `/Billing/SearchAccounts?q=${encodeURIComponent(q)}`
        + (currentReadingSheetId > 0 ? `&readingSheetId=${currentReadingSheetId}` : '');
      const res = await fetch(searchUrl);
      if (!res.ok) {
        const errorText = await res.text();
        console.error('Search failed:', res.status, errorText);
        showPopup({ 
          title: 'Search Error', 
          message: `Failed to search accounts: ${res.status} ${res.statusText}`, 
          type: 'warning' 
        });
        readingsList = [];
        currentPage  = 1;
        renderTable();
        return;
      }

      const data = await res.json();
      // Map to internal shape
      const mapped = data.map(item => ({
        accountId     : item.accountId,
        accountNo     : item.accountNumber,
        name          : item.name,
        meterNumber   : item.meterNumber,
        address       : item.address,
        rateClass     : item.classification,
        meterSize     : item.meterSize || 0.5,
        present       : item.presentReading,
        previous      : item.previousReading,
        usage         : item.usage,
        amount        : typeof item.amount === 'number'
                          ? item.amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                          : item.amount,
        zone          : '',
        book          : ''
      }));

      searchCache[cacheKey]  = mapped;
      readingsList    = mapped;
      currentPage     = 1;
      renderTable();

      autoFillIfExactMatch(q, mapped);
    } catch (err) {
      console.error('Search error:', err);
      showPopup({ 
        title: 'Search Error', 
        message: 'Error searching accounts: ' + err.message, 
        type: 'warning' 
      });
      readingsList = [];
      currentPage  = 1;
      renderTable();
    }
  }

  /** If the query exactly matches one account number, fill the top form. */
  function autoFillIfExactMatch(q, list) {
    const exact = list.find(r => r.accountNo.toLowerCase() === q.toLowerCase());
    if (exact) fillFormFromRow(exact);
  }

  /** Populate the read-only form fields from a search result row. */
  function fillFormFromRow(row) {
    const set = (id, val) => { const el = document.getElementById(id); if (el) el.value = val ?? ''; };
    set('accountNumber',   row.accountNo);
    set('accountName',     row.name);
    set('meterNumber',     row.meterNumber);
    set('address',         row.address);
    set('previousReading', row.previous);
    set('rateClass',       row.rateClass || 'Residential');
    set('meterSize',       row.meterSize  || '0.5');
    calculateUsageAndAmount();
  }

  function clearFormFields() {
    const clear = (id) => { const el = document.getElementById(id); if (el) el.value = ''; };
    clear('accountName');
    clear('meterNumber');
    clear('address');
    clear('previousReading');
  }

  // ─── Zone + Book filter ───────────────────────────────────────────────────────
  function bindZoneBookFilters() {
    const zone = document.getElementById('zoneSelect');
    const book = document.getElementById('bookSelect');
    const reader = document.getElementById('readerSelect');

    if (zone) {
      zone.addEventListener('change', async function () {
        clearReadingSheetSchedule();
        await loadBooksByZone(this.value);
        currentPage = 1;
        readingsList = [];
        renderTable();
      });
    }

    const onChange = async () => {
      currentPage = 1;
      await loadReadingSheetSchedule();
      await loadReadingsByZoneBook();
      renderTable();
    };

    if (book) book.addEventListener('change', onChange);
    if (reader) reader.addEventListener('change', async function () {
      await loadReadingSheetSchedule();
      await loadReadingsByZoneBook();
      renderTable();
    });
  }

  async function loadReadingsByZoneBook() {
    const zone = document.getElementById('zoneSelect')?.value;
    const book = document.getElementById('bookSelect')?.value;

    if (!zone || !book) { readingsList = []; return; }

    try {
      if (currentReadingSheetId <= 0) {
        readingsList = [];
        return;
      }
      const res = await fetch(`/Billing/GetReadingsByZoneBook?zone=${zone}&book=${book}&readingSheetId=${currentReadingSheetId}`);
      if (!res.ok) { readingsList = []; return; }

      const data = await res.json();
      readingsList = data.map(item => ({
        accountId     : item.accountId,
        accountNo     : item.accountNumber,
        name          : item.name,
        meterNumber   : item.meterNumber,
        address       : item.address,
        rateClass     : item.classification,
        meterSize     : item.meterSize || 0.5,
        present       : item.presentReading,
        previous      : item.previousReading,
        usage         : item.usage,
        amount        : typeof item.amount === 'number'
                          ? item.amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                          : item.amount,
        zone,
        book
      }));
    } catch (err) {
      console.error('Zone/book load error:', err);
      readingsList = [];
    }
  }

  // ─── Usage + amount calculation ───────────────────────────────────────────────
  function bindReadingCalculations() {
    const present = document.getElementById('presentReading');
    const prev    = document.getElementById('previousReading');
    if (present) present.addEventListener('input', calculateUsageAndAmount);
    if (prev)    prev.addEventListener('input',    calculateUsageAndAmount);
  }

  function calculateUsageAndAmount() {
    const present   = parseFloat(document.getElementById('presentReading')?.value)  || 0;
    const prev      = parseFloat(document.getElementById('previousReading')?.value) || 0;
    const usage     = Math.max(0, present - prev);
    const rateClass = document.getElementById('rateClass')?.value  || 'Residential';
    const meterSize = parseFloat(document.getElementById('meterSize')?.value) || 0.5;

    const usageEl  = document.getElementById('calculatedUsage');
    const amountEl = document.getElementById('calculatedAmount');
    if (usageEl) usageEl.value = usage;

    // Full rate table — mirrors BillingController RateByClassification + MinChargeByClassAndSize
    const TIER_RATES = {
      Residential: { r1:18.25, r2:19.55, r3:20.90, r4:23.50 },
      Government:  { r1:18.25, r2:19.55, r3:20.90, r4:23.50 },
      Commercial:  { r1:36.50, r2:39.10, r3:41.80, r4:47.00 },
      Industrial:  { r1:36.50, r2:39.10, r3:41.80, r4:47.00 },
      CommercialA: { r1:31.90, r2:34.20, r3:36.55, r4:41.10 },
      CommercialB: { r1:27.35, r2:29.30, r3:31.35, r4:35.25 },
      CommercialC: { r1:22.80, r2:24.40, r3:26.10, r4:29.35 },
      Wholesale:   { r1:54.75, r2:58.65, r3:62.70, r4:70.50 },
      Bulk:        { r1:54.75, r2:58.65, r3:62.70, r4:70.50 },
    };
    const MIN_CHARGE = {
      Residential: { 0.5:170,   0.75:272,  1:544,   1.5:1360,  2:3400,  3:6120,  4:12240 },
      Government:  { 0.5:170,   0.75:272,  1:544,   1.5:1360,  2:3400,  3:6120,  4:12240 },
      Commercial:  { 0.5:340,   0.75:544,  1:1088,  1.5:2720,  2:6800,  3:12240, 4:24480 },
      Industrial:  { 0.5:340,   0.75:544,  1:1088,  1.5:2720,  2:6800,  3:12240, 4:24480 },
      CommercialA: { 0.5:297.5, 0.75:476,  1:952,   1.5:2380,  2:5950,  3:10710, 4:21420 },
      CommercialB: { 0.5:255,   0.75:408,  1:816,   1.5:2040,  2:5100,  3:9180,  4:18360 },
      CommercialC: { 0.5:212.5, 0.75:340,  1:680,   1.5:1700,  2:4250,  3:7650,  4:15300 },
      Wholesale:   { 0.5:510,   0.75:816,  1:1632,  1.5:4080,  2:10200, 3:18360, 4:36720 },
      Bulk:        { 0.5:510,   0.75:816,  1:1632,  1.5:4080,  2:10200, 3:18360, 4:36720 },
    };

    const rates     = TIER_RATES[rateClass]  || TIER_RATES['Residential'];
    const minCharge = (MIN_CHARGE[rateClass] || MIN_CHARGE['Residential'])[meterSize] || 170;

    // Tiered cumulative charge based on present - previous (usage)
    let total = minCharge;
    if (usage > 10) total += (Math.min(usage, 20) - 10) * rates.r1;
    if (usage > 20) total += (Math.min(usage, 30) - 20) * rates.r2;
    if (usage > 30) total += (Math.min(usage, 40) - 30) * rates.r3;
    if (usage > 40) total += (usage - 40) * rates.r4;
    // The configured maintenance amount is selected server-side by the active sheet's billing date.

    if (amountEl) amountEl.value = total.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  // ─── Save reading ─────────────────────────────────────────────────────────────
  function bindFormSubmission() {
    const btn = document.getElementById('saveReadingBtn');
    if (!btn) return;

    btn.addEventListener('click', async function (e) {
      e.preventDefault();
      if (btn.disabled) return;

      const accountNo = document.getElementById('accountNumber')?.value.trim();
      const name      = document.getElementById('accountName')?.value.trim();
      const present   = parseFloat(document.getElementById('presentReading')?.value);
      const previous  = parseFloat(document.getElementById('previousReading')?.value) || 0;
      const rateClass = document.getElementById('rateClass')?.value || 'Residential';

      if (!accountNo) {
        showPopup({ title: 'Account Number Required', message: 'Please enter an Account Number before saving.', type: 'warning', focusId: 'accountNumber' });
        return;
      }
      if (!Number.isFinite(present) || present < 0 || present < previous) {
        showPopup({ title: 'Invalid Present Reading', message: 'Present Reading must be a nonnegative number and cannot be less than Previous Reading.', type: 'warning', focusId: 'presentReading' });
        return;
      }

      const zone      = parseInt(document.getElementById('zoneSelect')?.value)  || 0;
      const book      = parseInt(document.getElementById('bookSelect')?.value)   || 0;
      const readerId  = parseInt(document.getElementById('readerSelect')?.value) || 0;
      const readingDate = document.getElementById('readingDate')?.value || '';
      const billingDate = document.getElementById('billingDate')?.value || '';
      const dueDate = document.getElementById('dueDate')?.value || '';

      if (!currentReadingSheetId || !readingDate || !billingDate || !dueDate) {
        showPopup({
          title: 'Reading Sheet Required',
          message: 'Select a Zone, Book, and Reader with an active reading sheet and a complete date schedule.',
          type: 'warning'
        });
        return;
      }
      if (readingDate >= billingDate) {
        showPopup({
          title: 'Invalid Reading Date',
          message: 'Reading Date must be before the reading sheet Billing Date.',
          type: 'warning',
          focusId: 'readingDate'
        });
        return;
      }
      if (zone === 0 || book === 0) {
        showPopup({ title: 'Zone and Book Required', message: 'Please select a Zone and Book before saving.', type: 'warning' });
        return;
      }

      const usage     = Math.max(0, present - previous);
      const amountVal = document.getElementById('calculatedAmount')?.value || '0.00';

      btn.disabled = true;
      try {
        const res = await fetch('/Billing/SaveReading', {
          method : 'POST',
          headers: { 'Content-Type': 'application/json' },
          body   : JSON.stringify({
            accountNumber: accountNo,
            presentReading: present,
            zone,
            book,
            readerId,
            readingSheetId: currentReadingSheetId,
            readingDate,
            billingDate,
            dueDate
          })
        });

        if (res.ok) {
          const saved = await res.json();

          // Invalidate search cache so next lookup reflects saved values
          searchCache = {};

          showPopup({ title: 'Reading Saved', message: `Reading for Account #${accountNo} has been saved successfully!`, type: 'success' });

          document.getElementById('presentReading').value = '';
          const cu = document.getElementById('calculatedUsage');  if (cu) cu.value = '';
          const ca = document.getElementById('calculatedAmount'); if (ca) ca.value = '';

          // Prepare for the next account and reload the complete Zone/Book
          // list, including the reading that was just saved.
          const accountInput = document.getElementById('accountNumber');
          if (accountInput) accountInput.value = '';
          clearFormFields();
          await loadReadingsByZoneBook();

          currentPage = 1;
          renderTable();
          accountInput?.focus();
        } else {
          const msg = await res.text();
          showPopup({ title: 'Save Failed', message: 'Failed to save reading: ' + (msg || res.statusText), type: 'warning' });
        }
      } catch (err) {
        console.error('Save error:', err);
        showPopup({ title: 'Error', message: 'An error occurred while saving the reading. Reload the sheet before retrying.', type: 'warning' });
      } finally {
        btn.disabled = false;
      }
    });
  }

  // ─── Filtering ────────────────────────────────────────────────────────────────
  function getFilteredList() {
    const q    = (document.getElementById('accountNumber')?.value.trim() || '').toLowerCase();
    const zone = document.getElementById('zoneSelect')?.value || '';
    const book = document.getElementById('bookSelect')?.value || '';

    // Nothing typed and no zone/book → show prompt
    if (!q && !zone && !book) return null;

    let list = readingsList;

    // If the user is typing, filter client-side (search already fetched the right set)
    if (q) {
      list = list.filter(r =>
        r.accountNo.toLowerCase().includes(q) ||
        r.name.toLowerCase().includes(q)
      );
    } else {
      // Zone / book loaded set — no extra client-side filter needed
    }

    return list;
  }

  // ─── Pagination ───────────────────────────────────────────────────────────────
  function bindPagination() {
    const rows = document.getElementById('rowsPerPage');
    const prev = document.getElementById('prevPageBtn');
    const next = document.getElementById('nextPageBtn');

    if (rows) rows.addEventListener('change', function () {
      pageSize    = parseInt(this.value, 10);
      currentPage = 1;
      renderTable();
    });

    if (prev) prev.addEventListener('click', function () {
      if (currentPage > 1) { currentPage--; renderTable(); }
    });

    if (next) next.addEventListener('click', function () {
      const total = Math.ceil((getFilteredList() || []).length / pageSize) || 1;
      if (currentPage < total) { currentPage++; renderTable(); }
    });
  }

  // ─── Render table ─────────────────────────────────────────────────────────────
  function renderTable() {
    const tbody  = document.getElementById('readingsTableBody');
    const info   = document.getElementById('paginationInfo');
    const prev   = document.getElementById('prevPageBtn');
    const next   = document.getElementById('nextPageBtn');
    const q      = document.getElementById('accountNumber')?.value.trim() || '';
    const zone   = document.getElementById('zoneSelect')?.value || '';
    const book   = document.getElementById('bookSelect')?.value || '';

    if (!tbody) return;

    const filtered = getFilteredList();

    // ── Nothing to show yet ──
    if (filtered === null) {
      tbody.innerHTML = `
        <tr>
          <td colspan="7" class="pr-table-empty">
            Start typing an Account Number, or select a Zone and Book to view records.
          </td>
        </tr>`;
      if (info) info.textContent = '0–0 of 0';
      if (prev) prev.disabled = true;
      if (next) next.disabled = true;
      return;
    }

    // ── Empty result ──
    if (filtered.length === 0) {
      let msg = '';
      if (q) {
        msg = `No accounts found matching "<strong>${escapeHtml(q)}</strong>".`;
      } else {
        const parts = [zone ? `Zone ${zone}` : '', book ? `Book ${book}` : ''].filter(Boolean).join(' and ');
        msg = `No reading records found for ${escapeHtml(parts)}.`;
      }
      tbody.innerHTML = `<tr><td colspan="7" class="pr-table-empty">${msg}</td></tr>`;
      if (info) info.textContent = '0–0 of 0';
      if (prev) prev.disabled = true;
      if (next) next.disabled = true;
      return;
    }

    // ── Paginate ──
    const total      = filtered.length;
    const totalPages = Math.ceil(total / pageSize) || 1;
    // Clamp current page — never let it exceed total pages
    if (currentPage < 1) currentPage = 1;
    if (currentPage > totalPages) currentPage = totalPages;

    const start  = (currentPage - 1) * pageSize;
    const end    = Math.min(start + pageSize, total);
    const page   = filtered.slice(start, end);

    tbody.innerHTML = page.map(item => `
      <tr class="pr-table-row" data-acc="${escapeHtml(item.accountNo)}" style="cursor:pointer;">
        <td>
          <span class="pr-table-acc-link">
            ${escapeHtml(item.accountNo)}
          </span>
        </td>
        <td>${escapeHtml(item.name)}</td>
        <td><span class="pr-badge">${escapeHtml(item.rateClass)}</span></td>
        <td style="font-weight:600;color:var(--color-blue);">${item.present}</td>
        <td>${item.previous}</td>
        <td>${item.usage} m³</td>
        <td style="font-weight:600;">₱${item.present > 0 || item.previous > 0 ? item.amount : '0.00'}</td>
      </tr>
    `).join('');

    // Row click → fill form
    tbody.querySelectorAll('.pr-table-row').forEach(row => {
      row.addEventListener('click', function () {
        const accNo   = this.getAttribute('data-acc');
        const matched = readingsList.find(r => r.accountNo === accNo);
        if (!matched) return;

        fillFormFromRow(matched);

        // Sync the input value then clear the dropdown-like table
        const input = document.getElementById('accountNumber');
        if (input) input.value = accNo;

        // Focus present reading immediately
        document.getElementById('presentReading')?.focus();
      });
    });

    if (info) info.textContent = `${start + 1}–${end} of ${total}`;
    if (prev) prev.disabled = currentPage === 1;
    if (next) next.disabled = currentPage === totalPages;
  }

  // ─── Popup modal ──────────────────────────────────────────────────────────────
  function showPopup({ title, message, type = 'success', focusId = null }) {
    let backdrop = document.getElementById('prNotificationModal');
    if (!backdrop) {
      backdrop = document.createElement('div');
      backdrop.id        = 'prNotificationModal';
      backdrop.className = 'pr-modal-backdrop';
      backdrop.innerHTML = `
        <div class="pr-modal-card" role="dialog" aria-modal="true">
          <div class="pr-modal-icon-wrap" id="prModalIconWrap"></div>
          <h3 class="pr-modal-title"  id="prModalTitle">Notification</h3>
          <p  class="pr-modal-message" id="prModalMessage"></p>
          <button type="button" class="pr-modal-btn-confirm" id="prModalConfirmBtn">OK</button>
        </div>`;
      document.body.appendChild(backdrop);
    }

    const titleEl  = backdrop.querySelector('#prModalTitle');
    const msgEl    = backdrop.querySelector('#prModalMessage');
    const iconWrap = backdrop.querySelector('#prModalIconWrap');
    const btn      = backdrop.querySelector('#prModalConfirmBtn');

    if (titleEl) titleEl.textContent = title   || 'Notification';
    if (msgEl)   msgEl.textContent   = message || '';

    const icons = {
      success : `<polyline points="20 6 9 17 4 12"></polyline>`,
      warning : `<path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line>`,
      info    : `<circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line>`
    };
    if (iconWrap) {
      iconWrap.className = `pr-modal-icon-wrap pr-modal-icon-wrap--${type}`;
      iconWrap.innerHTML = `<svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">${icons[type] || icons.info}</svg>`;
    }

    const close = () => {
      backdrop.classList.remove('is-visible');
      document.removeEventListener('keydown', onKey);
      if (focusId) document.getElementById(focusId)?.focus();
    };
    const onKey = (e) => { if (e.key === 'Escape' || e.key === 'Enter') close(); };

    btn.onclick      = close;
    backdrop.onclick = (e) => { if (e.target === backdrop) close(); };
    document.addEventListener('keydown', onKey);

    requestAnimationFrame(() => { backdrop.classList.add('is-visible'); btn.focus(); });
  }

  // ─── Utility ──────────────────────────────────────────────────────────────────
  function escapeHtml(str) {
    if (!str) return '';
    return String(str)
      .replace(/&/g,  '&amp;')
      .replace(/</g,  '&lt;')
      .replace(/>/g,  '&gt;')
      .replace(/"/g,  '&quot;');
  }

})();
