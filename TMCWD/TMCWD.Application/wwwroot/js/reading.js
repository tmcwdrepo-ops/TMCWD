/**
 * present_reading.js — Present Reading Creation & Table Management
 */

(function () {
  'use strict';

  // Retrieve sample data from window.CONCESSIONAIRES_SAMPLE and window.READINGS_SAMPLE_LIST
  const getConcessionairesSample = () => window.CONCESSIONAIRES_SAMPLE || window.CONCESSIONAIRES_DB || {};
  
  // State
  let readingsList = Array.isArray(window.READINGS_SAMPLE_LIST) 
    ? [...window.READINGS_SAMPLE_LIST] 
    : (Array.isArray(window.DEFAULT_READINGS_LIST) ? [...window.DEFAULT_READINGS_LIST] : []);
    
  let currentPage = 1;
  let pageSize = 5;

  document.addEventListener('DOMContentLoaded', function () {
    // Re-sync initial readings list if loaded after DOM
    if (readingsList.length === 0 && Array.isArray(window.READINGS_SAMPLE_LIST)) {
      readingsList = [...window.READINGS_SAMPLE_LIST];
    }
    initFormDefaults();
    bindAccountLookup();
    bindZoneBookFilters();
    bindReadingCalculations();
    bindFormSubmission();
    bindPagination();
    renderTable();
  });

  // Set default dates (Today, Billing date, Due date +15 days)
  function initFormDefaults() {
    const today = new Date();
    const dueDate = new Date();
    dueDate.setDate(today.getDate() + 15);

    const formatISO = (d) => d.toISOString().split('T')[0];

    const billingDateEl = document.getElementById('billingDate');
    const dueDateEl = document.getElementById('dueDate');
    const readingDateEl = document.getElementById('readingDate');

    if (billingDateEl && !billingDateEl.value) billingDateEl.value = formatISO(today);
    if (readingDateEl && !readingDateEl.value) readingDateEl.value = formatISO(today);
    if (dueDateEl && !dueDateEl.value) dueDateEl.value = formatISO(dueDate);
  }

  // Account Lookup Handler
  function bindAccountLookup() {
    const accountNoInput = document.getElementById('accountNumber');
    if (!accountNoInput) return;

    const performLookup = () => {
      const accNo = accountNoInput.value.trim();
      const nameInput = document.getElementById('accountName');
      const meterNoInput = document.getElementById('meterNumber');
      const addressInput = document.getElementById('address');
      const prevReadingInput = document.getElementById('previousReading');
      const rateClassInput = document.getElementById('rateClass');

      const sampleDb = getConcessionairesSample();
      if (sampleDb[accNo]) {
        const data = sampleDb[accNo];
        if (nameInput) nameInput.value = data.name;
        if (meterNoInput) meterNoInput.value = data.meterNo;
        if (addressInput) addressInput.value = data.address;
        if (prevReadingInput) prevReadingInput.value = data.prevReading;
        if (rateClassInput) rateClassInput.value = data.rateClass;
        calculateUsageAndAmount();
      } else if (accNo.length >= 4) {
        // Fallback generator for unlisted accounts
        if (nameInput) nameInput.value = 'Concessionaire #' + accNo;
        if (meterNoInput) meterNoInput.value = 'M-' + Math.floor(10000 + Math.random() * 90000);
        if (addressInput) addressInput.value = 'Trece Martires City';
        if (prevReadingInput) prevReadingInput.value = '100';
        calculateUsageAndAmount();
      } else {
        // Reset auto-populated fields if account number is cleared or short
        if (nameInput) nameInput.value = '';
        if (meterNoInput) meterNoInput.value = '';
        if (addressInput) addressInput.value = '';
        if (prevReadingInput) prevReadingInput.value = '';
        calculateUsageAndAmount();
      }

      currentPage = 1;
      renderTable();
    };

    accountNoInput.addEventListener('input', performLookup);
    accountNoInput.addEventListener('change', performLookup);
  }

  // Zone & Book Filters Handler
  function bindZoneBookFilters() {
    const zoneSelect = document.getElementById('zoneSelect');
    const bookSelect = document.getElementById('bookSelect');

    const handleFilterChange = () => {
      currentPage = 1;
      renderTable();
    };

    if (zoneSelect) zoneSelect.addEventListener('change', handleFilterChange);
    if (bookSelect) bookSelect.addEventListener('change', handleFilterChange);
  }

  // Calculation Handler for Usage & Amount
  function bindReadingCalculations() {
    const presentInput = document.getElementById('presentReading');
    const prevInput = document.getElementById('previousReading');

    if (presentInput) presentInput.addEventListener('input', calculateUsageAndAmount);
    if (prevInput) prevInput.addEventListener('input', calculateUsageAndAmount);
  }

  function calculateUsageAndAmount() {
    const presentVal = parseFloat(document.getElementById('presentReading')?.value) || 0;
    const prevVal = parseFloat(document.getElementById('previousReading')?.value) || 0;
    const usageInput = document.getElementById('calculatedUsage');
    const amountInput = document.getElementById('calculatedAmount');
    const rateClass = document.getElementById('rateClass')?.value || 'Residential';

    const usage = Math.max(0, presentVal - prevVal);
    if (usageInput) usageInput.value = usage;

    // Rate structure computation sample
    let minRate = rateClass === 'Commercial' ? 450 : 220;
    let minUsage = 10;
    let excessRate = rateClass === 'Commercial' ? 45 : 25;

    let totalAmount = minRate;
    if (usage > minUsage) {
      totalAmount += (usage - minUsage) * excessRate;
    }

    if (amountInput) {
      amountInput.value = totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
  }

  // Save / Form Submission
  function bindFormSubmission() {
    const saveBtn = document.getElementById('saveReadingBtn');
    if (!saveBtn) return;

    saveBtn.addEventListener('click', function (e) {
      e.preventDefault();

      const accountNo = document.getElementById('accountNumber')?.value.trim();
      const name = document.getElementById('accountName')?.value.trim();
      const present = parseFloat(document.getElementById('presentReading')?.value);
      const previous = parseFloat(document.getElementById('previousReading')?.value) || 0;
      const rateClass = document.getElementById('rateClass')?.value || 'Residential';

      if (!accountNo) {
        showPopup({
          title: 'Account Number Required',
          message: 'Please enter an Account Number before saving.',
          type: 'warning',
          focusId: 'accountNumber'
        });
        return;
      }

      if (isNaN(present)) {
        showPopup({
          title: 'Invalid Present Reading',
          message: 'Please enter a valid numeric Present Reading.',
          type: 'warning',
          focusId: 'presentReading'
        });
        return;
      }

      const usage = Math.max(0, present - previous);
      const amountVal = document.getElementById('calculatedAmount')?.value || '0.00';

      // Check if account exists in current table to update or append
      const existingIdx = readingsList.findIndex(r => r.accountNo === accountNo);
      if (existingIdx >= 0) {
        readingsList[existingIdx] = {
          ...readingsList[existingIdx],
          name: name || readingsList[existingIdx].name,
          rateClass: rateClass,
          present: present,
          previous: previous,
          usage: usage,
          amount: amountVal
        };
      } else {
        readingsList.unshift({
          id: Date.now(),
          accountNo: accountNo,
          name: name || 'Concessionaire #' + accountNo,
          rateClass: rateClass,
          present: present,
          previous: previous,
          usage: usage,
          amount: amountVal
        });
      }

      // Show modal popup notification
      showPopup({
        title: 'Reading Saved',
        message: 'Reading for Account #' + accountNo + ' has been saved successfully!',
        type: 'success'
      });

      // Reset reading input fields but keep account number so saved row remains visible in table
      document.getElementById('presentReading').value = '';
      if (document.getElementById('calculatedUsage')) document.getElementById('calculatedUsage').value = '';
      if (document.getElementById('calculatedAmount')) document.getElementById('calculatedAmount').value = '';

      currentPage = 1;
      renderTable();
    });
  }

  // Helper to get filtered list of reading records based on dropdowns or search input
  function getFilteredList() {
    const accountNoInput = document.getElementById('accountNumber');
    const zoneSelect = document.getElementById('zoneSelect');
    const bookSelect = document.getElementById('bookSelect');

    const rawTyped = accountNoInput ? accountNoInput.value.trim() : '';
    const typedAcc = rawTyped.toLowerCase();
    const selectedZone = zoneSelect ? zoneSelect.value : '';
    const selectedBook = bookSelect ? bookSelect.value : '';

    let list = readingsList;

    if (typedAcc) {
      list = list.filter(item => 
        item.accountNo.toLowerCase().includes(typedAcc) ||
        item.name.toLowerCase().includes(typedAcc)
      );
    } else if (selectedZone || selectedBook) {
      if (selectedZone) {
        list = list.filter(item => item.zone === selectedZone);
      }
      if (selectedBook) {
        list = list.filter(item => item.book === selectedBook);
      }
    } else {
      return null;
    }
    return list;
  }

  // Table Pagination
  function bindPagination() {
    const rowsPerPageSelect = document.getElementById('rowsPerPage');
    const prevBtn = document.getElementById('prevPageBtn');
    const nextBtn = document.getElementById('nextPageBtn');

    if (rowsPerPageSelect) {
      rowsPerPageSelect.addEventListener('change', function () {
        pageSize = parseInt(this.value, 10);
        currentPage = 1;
        renderTable();
      });
    }

    if (prevBtn) {
      prevBtn.addEventListener('click', function () {
        if (currentPage > 1) {
          currentPage--;
          renderTable();
        }
      });
    }

    if (nextBtn) {
      nextBtn.addEventListener('click', function () {
        const filteredList = getFilteredList() || [];
        const totalPages = Math.ceil(filteredList.length / pageSize) || 1;
        if (currentPage < totalPages) {
          currentPage++;
          renderTable();
        }
      });
    }
  }

  function renderTable() {
    const tbody = document.getElementById('readingsTableBody');
    const infoEl = document.getElementById('paginationInfo');
    const prevBtn = document.getElementById('prevPageBtn');
    const nextBtn = document.getElementById('nextPageBtn');
    const accountNoInput = document.getElementById('accountNumber');
    const zoneSelect = document.getElementById('zoneSelect');
    const bookSelect = document.getElementById('bookSelect');

    if (!tbody) return;

    const filteredList = getFilteredList();

    if (filteredList === null) {
      tbody.innerHTML = `
        <tr>
          <td colspan="7" class="pr-table-empty">
            Please select a Zone and Book, or enter an Account Number to view reading records.
          </td>
        </tr>
      `;
      if (infoEl) infoEl.textContent = '0–0 of 0';
      if (prevBtn) prevBtn.disabled = true;
      if (nextBtn) nextBtn.disabled = true;
      return;
    }

    if (filteredList.length === 0) {
      const rawTyped = accountNoInput ? accountNoInput.value.trim() : '';
      const selectedZone = zoneSelect ? zoneSelect.value : '';
      const selectedBook = bookSelect ? bookSelect.value : '';

      let emptyMsg = '';
      if (rawTyped) {
        emptyMsg = `No reading records found for Account "${escapeHtml(rawTyped)}".`;
      } else {
        const zoneText = selectedZone ? `Zone ${selectedZone}` : '';
        const bookText = selectedBook ? `Book ${selectedBook}` : '';
        const comb = [zoneText, bookText].filter(Boolean).join(' and ');
        emptyMsg = `No reading records found for ${escapeHtml(comb)}.`;
      }

      tbody.innerHTML = `
        <tr>
          <td colspan="7" class="pr-table-empty">
            ${emptyMsg}
          </td>
        </tr>
      `;
      if (infoEl) infoEl.textContent = '0–0 of 0';
      if (prevBtn) prevBtn.disabled = true;
      if (nextBtn) nextBtn.disabled = true;
      return;
    }

    const totalRecords = filteredList.length;
    const totalPages = Math.ceil(totalRecords / pageSize) || 1;
    if (currentPage > totalPages) currentPage = totalPages;

    const startIndex = (currentPage - 1) * pageSize;
    const endIndex = Math.min(startIndex + pageSize, totalRecords);
    const pageData = filteredList.slice(startIndex, endIndex);

    tbody.innerHTML = pageData.map(item => `
      <tr>
        <td>
          <a href="javascript:void(0);" class="pr-table-acc-link" data-acc="${escapeHtml(item.accountNo)}">
            ${escapeHtml(item.accountNo)}
          </a>
        </td>
        <td>${escapeHtml(item.name)}</td>
        <td><span class="pr-badge">${escapeHtml(item.rateClass)}</span></td>
        <td style="font-weight: 600; color: var(--color-blue);">${item.present}</td>
        <td>${item.previous}</td>
        <td>${item.usage} m³</td>
        <td style="font-weight: 600;">₱${item.amount}</td>
      </tr>
    `).join('');

    // Bind click events on account number links to fill the top form
    tbody.querySelectorAll('.pr-table-acc-link').forEach(link => {
      link.addEventListener('click', function (e) {
        e.preventDefault();
        const accNo = this.getAttribute('data-acc');
        const accountNoInput = document.getElementById('accountNumber');
        if (accountNoInput) {
          accountNoInput.value = accNo;
          // Trigger events to update read-only fields
          accountNoInput.dispatchEvent(new Event('input'));
          accountNoInput.dispatchEvent(new Event('change'));
          // Set focus directly to Present Reading
          document.getElementById('presentReading')?.focus();
        }
      });
    });

    if (infoEl) {
      infoEl.textContent = `${startIndex + 1}–${endIndex} of ${totalRecords}`;
    }

    if (prevBtn) prevBtn.disabled = currentPage === 1;
    if (nextBtn) nextBtn.disabled = currentPage === totalPages;
  }

  // Popup Modal Dialog Notification
  function showPopup({ title, message, type = 'success', focusId = null }) {
    let backdrop = document.getElementById('prNotificationModal');
    if (!backdrop) {
      backdrop = document.createElement('div');
      backdrop.id = 'prNotificationModal';
      backdrop.className = 'pr-modal-backdrop';
      backdrop.innerHTML = `
        <div class="pr-modal-card" role="dialog" aria-modal="true">
          <div class="pr-modal-icon-wrap" id="prModalIconWrap"></div>
          <h3 class="pr-modal-title" id="prModalTitle">Notification</h3>
          <p class="pr-modal-message" id="prModalMessage"></p>
          <button type="button" class="pr-modal-btn-confirm" id="prModalConfirmBtn">OK</button>
        </div>
      `;
      document.body.appendChild(backdrop);
    }

    const titleEl = backdrop.querySelector('#prModalTitle');
    const msgEl = backdrop.querySelector('#prModalMessage');
    const iconWrap = backdrop.querySelector('#prModalIconWrap');
    const confirmBtn = backdrop.querySelector('#prModalConfirmBtn');

    if (titleEl) titleEl.textContent = title || 'Notification';
    if (msgEl) msgEl.textContent = message || '';

    if (iconWrap) {
      iconWrap.className = 'pr-modal-icon-wrap pr-modal-icon-wrap--' + type;
      if (type === 'success') {
        iconWrap.innerHTML = `
          <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="20 6 9 17 4 12"></polyline>
          </svg>
        `;
      } else if (type === 'warning') {
        iconWrap.innerHTML = `
          <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path>
            <line x1="12" y1="9" x2="12" y2="13"></line>
            <line x1="12" y1="17" x2="12.01" y2="17"></line>
          </svg>
        `;
      } else {
        iconWrap.innerHTML = `
          <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="8" x2="12" y2="12"></line>
            <line x1="12" y1="16" x2="12.01" y2="16"></line>
          </svg>
        `;
      }
    }

    const closeModal = () => {
      backdrop.classList.remove('is-visible');
      document.removeEventListener('keydown', handleKeyDown);
      if (focusId) {
        document.getElementById(focusId)?.focus();
      }
    };

    const handleKeyDown = (e) => {
      if (e.key === 'Escape' || e.key === 'Enter') {
        closeModal();
      }
    };

    confirmBtn.onclick = closeModal;
    backdrop.onclick = (e) => {
      if (e.target === backdrop) closeModal();
    };

    document.addEventListener('keydown', handleKeyDown);

    requestAnimationFrame(() => {
      backdrop.classList.add('is-visible');
      confirmBtn.focus();
    });
  }

  function escapeHtml(str) {
    if (!str) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

})();

