// collections.js
// Interactivity for the TMCWD Collections page.
// Expects COLLECTION_ACCOUNTS to already be defined (loaded from collection_sample.js).

document.addEventListener('DOMContentLoaded', function () {

  const accountsData = (typeof COLLECTION_ACCOUNTS !== 'undefined') ? COLLECTION_ACCOUNTS : null;
  const defaultAccount = accountsData ? accountsData[0] : null;

  const billsBody      = document.getElementById('billsBody');
  const invoiceInput    = document.getElementById('invoiceInput');
  const acctCountNum    = document.getElementById('acctCountNum');
  const acctCountLabel  = document.getElementById('acctCountLabel');
  const statusBadge     = document.getElementById('statusBadge');
  const fAccNo          = document.getElementById('fAccNo');
  const fMeter          = document.getElementById('fMeter');
  const fName           = document.getElementById('fName');
  const fAddress        = document.getElementById('fAddress');
  const fType           = document.getElementById('fType');
  const fOtherCharges   = document.getElementById('fOtherCharges');
  const sTotalBill      = document.getElementById('sTotalBill');
  const sPCA            = document.getElementById('sPCA');
  const sMMF            = document.getElementById('sMMF');
  const sTotalDue       = document.getElementById('sTotalDue');
  const sPenalty        = document.getElementById('sPenalty');
  const sSubTotal       = document.getElementById('sSubTotal');
  const totalBalance    = document.getElementById('totalBalance');
  const totalBalanceLabel = document.getElementById('totalBalanceLabel');
  const amountToPay     = document.getElementById('amountToPay');
  const amountTendered  = document.getElementById('amountTendered');
  const remainingRow    = document.getElementById('remainingRow');
  const remainingLabel  = document.getElementById('remainingLabel');
  const remainingVal    = document.getElementById('remainingVal');
  const searchInput     = document.getElementById('searchInput');
  const resetBtn        = document.getElementById('resetBtn');
  const searchSuggestions = document.getElementById('searchSuggestions');
  const toast           = document.getElementById('toast');
  const multipleAccountsList = document.getElementById('multipleAccountsList');
  const multipleToggle  = document.getElementById('multipleToggle');

  let selectedSuggestionIndex = -1;
  let multipleAccountsMode = false;
  let selectedAccounts = [];
  let currentSelectedAccountIndex = -1;

  // ---------- Senior Citizen Discount state ----------
  // Populated by initDiscountModal when the user applies a discount.
  // { rate: 0.05, amount: 0, label: 'Senior Citizen (5%)' }
  let activeDiscount = null;

  const sDiscountLine   = document.getElementById('sDiscountLine');
  const sDiscount       = document.getElementById('sDiscount');
  const sDiscountRemoveBtn = document.getElementById('sDiscountRemoveBtn');

  function fmt(n) {
    return '\u20B1' + Number(n).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  function parseAmount(text) {
    return parseFloat(String(text).replace(/[\u20B1,]/g, '')) || 0;
  }

  // Enable / disable the Amount Tendered field
  function setTenderedEnabled(enabled) {
    amountTendered.disabled = !enabled;
    amountTendered.title    = enabled ? '' : 'Select an account first';
    amountTendered.closest('.amt-box').style.opacity = enabled ? '' : '0.45';
    amountTendered.closest('.amt-box').style.pointerEvents = enabled ? '' : 'none';
    if (!enabled) {
      amountTendered.value = '';
      recalcRemaining();
    }
  }

  // ---------- Discount helpers ----------
  function clearDiscount() {
    activeDiscount = null;
    sDiscountLine.style.display = 'none';
    sDiscount.textContent = '-\u20B10.00';
    // Re-run bill row display so sub-total reverts
    const selectedRow = billsBody.querySelector('.bill-row.selected');
    if (selectedRow) selectBillRow(selectedRow);
  }

  function applyDiscount(rate, label) {
    activeDiscount = { rate, label };
    // Re-run bill row display to show discount
    const selectedRow = billsBody.querySelector('.bill-row.selected');
    if (selectedRow) selectBillRow(selectedRow);
  }

  // Remove button on the discount row
  sDiscountRemoveBtn.addEventListener('click', function (e) {
    e.stopPropagation();
    clearDiscount();
  });

  // ---------- Invoice Input ----------
  invoiceInput.addEventListener('input', function() {
    // Allow only alphanumeric characters
    this.value = this.value.replace(/[^a-zA-Z0-9]/g, '').toUpperCase();
  });

  function getFullInvoiceNo() {
    const suffix = invoiceInput.value.trim();
    return suffix ? `CR00${suffix}` : '';
  }

  // ---------- Initial render from default account ----------
  function renderFromSample() {
    // Start with cleared state instead of showing default account
    forceClearAccountData();
  }

  // ---------- Bill row selection ----------
  function selectBillRow(row, opts) {
    document.querySelectorAll('.bill-row').forEach(r => {
      r.classList.remove('selected');
      r.querySelector('.radio').classList.remove('on');
    });
    row.classList.add('selected');
    row.querySelector('.radio').classList.add('on');

    const amount  = parseFloat(row.dataset.amount)  || 0;
    const penalty = parseFloat(row.dataset.penalty) || 0;
    const pca     = parseFloat(row.dataset.pca);
    const mmf     = parseFloat(row.dataset.mmf);
    const pcaVal  = isNaN(pca) ? 0 : pca;
    const mmfVal  = isNaN(mmf) ? 0 : mmf;
    const totalDue = amount + pcaVal + mmfVal;
    const subTotal = totalDue + penalty;

    sTotalBill.textContent = fmt(amount);
    sPCA.textContent       = fmt(pcaVal);
    sMMF.textContent       = fmt(mmfVal);
    sTotalDue.textContent  = fmt(totalDue);
    sPenalty.textContent   = fmt(penalty);

    // Apply senior citizen discount if active
    const discountAmt = activeDiscount ? parseFloat((subTotal * activeDiscount.rate).toFixed(2)) : 0;
    const finalSubTotal = subTotal - discountAmt;

    if (activeDiscount && discountAmt > 0) {
      sDiscount.textContent       = '-' + fmt(discountAmt);
      sDiscountLine.style.display = '';
    } else {
      sDiscountLine.style.display = 'none';
    }

    sSubTotal.textContent = fmt(finalSubTotal);

    // Use new total balance calculation
    updateTotalBalance();
  }

  billsBody.addEventListener('click', function (e) {
    const row = e.target.closest('.bill-row');
    if (row) selectBillRow(row);
  });

  // ---------- Payment method ----------
  document.querySelectorAll('.pay-opt').forEach(opt => {
    opt.addEventListener('click', function () {
      document.querySelectorAll('.pay-opt').forEach(o => o.classList.remove('active'));
      this.classList.add('active');
      const mode = this.dataset.mode;
      document.getElementById('checkPanel').style.display  = mode === 'check'  ? 'block' : 'none';
      document.getElementById('onlinePanel').style.display = mode === 'online' ? 'block' : 'none';
    });
  });

  function currentPayMode() {
    const active = document.querySelector('.pay-opt.active');
    return active ? active.dataset.mode : 'cash';
  }

  // ---------- Amount to pay / tendered ----------
  function recalcRemaining() {
    const toPay    = parseAmount(amountToPay.value);
    const tendered = parseAmount(amountTendered.value);
    const diff = tendered - toPay;

    if (!amountTendered.value || tendered === 0) {
      remainingRow.className = 'remaining-row';
      remainingLabel.textContent = 'Change';
      remainingVal.textContent = fmt(toPay);
    } else if (diff < 0) {
      remainingRow.className = 'remaining-row short';
      remainingLabel.textContent = 'Short By';
      remainingVal.textContent = fmt(Math.abs(diff));
    } else {
      remainingRow.className = 'remaining-row';
      remainingLabel.textContent = 'Change';
      remainingVal.textContent = fmt(diff);
    }
  }

  amountToPay.addEventListener('input', recalcRemaining);

  amountTendered.addEventListener('input', function () {
    // Strip anything that isn't a digit or a single decimal point
    let val = this.value.replace(/[^0-9.]/g, '');
    // Allow only one decimal point
    const parts = val.split('.');
    if (parts.length > 2) val = parts[0] + '.' + parts.slice(1).join('');
    this.value = val;
    recalcRemaining();
  });

  amountTendered.addEventListener('keydown', function (e) {
    // Block non-numeric keys (allow: digits, dot, backspace, delete, arrows, tab)
    const allowed = ['Backspace','Delete','ArrowLeft','ArrowRight','Tab','Home','End'];
    if (allowed.includes(e.key)) return;
    if (e.key === '.' && !this.value.includes('.')) return;
    if (!/^\d$/.test(e.key)) e.preventDefault();
  });
  recalcRemaining();

  // ---------- Search Functionality ----------
  
  // Store search results cache
  let searchResultsCache = [];
  let searchTimeout = null;
  
  // Debounce function to prevent excessive API calls
  function debounce(func, wait) {
    return function(...args) {
      clearTimeout(searchTimeout);
      searchTimeout = setTimeout(() => func.apply(this, args), wait);
    };
  }
  
  // Function to show suggestions based on search term
  async function showSuggestions(searchTerm) {
    if (searchTerm.length < 2) {
      hideSuggestions();
      return;
    }
    
    try {
      // Show loading state
      searchSuggestions.innerHTML = '<div class="no-suggestions">Searching...</div>';
      searchSuggestions.style.display = 'block';
      
      // Call the API to search accounts
      const response = await fetch(`/Billing/SearchAccountsForCollection?q=${encodeURIComponent(searchTerm)}`);
      
      if (!response.ok) {
        throw new Error('Search failed');
      }
      
      const matches = await response.json();
      
      // Store results in cache
      searchResultsCache = matches;
      
      if (matches.length === 0) {
        searchSuggestions.innerHTML = '<div class="no-suggestions">No accounts found</div>';
        searchSuggestions.style.display = 'block';
        return;
      }
      
      // Build suggestions HTML
      const suggestionsHTML = matches.map((account, index) => {
        return `
          <div class="suggestion-item" data-index="${index}">
            <div class="account-number">${account.accountNumber}</div>
            <div class="account-name">${account.name}</div>
            <div class="account-details">${account.meterNumber} \u2022 ${account.type} \u2022 ${account.address}</div>
          </div>
        `;
      }).join('');
      
      searchSuggestions.innerHTML = suggestionsHTML;
      searchSuggestions.style.display = 'block';
      selectedSuggestionIndex = -1;
      
      // Add click handlers to suggestions
      searchSuggestions.querySelectorAll('.suggestion-item').forEach(item => {
        item.addEventListener('click', function() {
          const accountIndex = parseInt(this.dataset.index);
          selectAccountFromAPI(searchResultsCache[accountIndex]);
        });
      });
    } catch (error) {
      console.error('Search error:', error);
      searchSuggestions.innerHTML = '<div class="no-suggestions">Error searching accounts</div>';
      searchSuggestions.style.display = 'block';
    }
  }
  
  // Debounced version of showSuggestions
  const debouncedShowSuggestions = debounce(showSuggestions, 300);
  
  // Function to convert API result to the format expected by loadAccountData
  function convertAPIToAccountData(apiAccount) {
    return {
      account: {
        accountNumber: apiAccount.accountNumber,
        meterNumber: apiAccount.meterNumber,
        name: apiAccount.name,
        address: apiAccount.address,
        type: apiAccount.type,
        status: apiAccount.status,
        otherChargesBalance: apiAccount.otherChargesBalance
      },
      bills: apiAccount.bills.map(bill => ({
        billMonth: bill.billMonth,
        dueDate: bill.dueDate,
        amount: bill.amount,
        pca: bill.pca,
        mmf: bill.mmf,
        penalty: bill.penalty,
        subTotal: bill.subTotal,
        selected: false
      })),
      invoiceNo: '',
      accountsInInvoice: 1
    };
  }
  
  // Function to select account from API result
  function selectAccountFromAPI(apiAccount) {
    const accountData = convertAPIToAccountData(apiAccount);
    selectAccount(accountData);
  }
  
  // Function to hide suggestions
  function hideSuggestions() {
    searchSuggestions.style.display = 'none';
    selectedSuggestionIndex = -1;
  }
  
  // Function to select an account and load its data
  function selectAccount(accountData) {
    if (multipleAccountsMode) {
      addAccountToList(accountData);
      searchInput.value = '';
    } else {
      searchInput.value = `${accountData.account.accountNumber} - ${accountData.account.name}`;
      loadAccountData(accountData);
    }
    hideSuggestions();
  }
  
  // Function to navigate suggestions with arrow keys
  function navigateSuggestions(direction) {
    const suggestions = searchSuggestions.querySelectorAll('.suggestion-item');
    if (suggestions.length === 0) return;
    
    // Remove previous highlight
    if (selectedSuggestionIndex >= 0) {
      suggestions[selectedSuggestionIndex].classList.remove('highlighted');
    }
    
    // Update index
    if (direction === 'down') {
      selectedSuggestionIndex = (selectedSuggestionIndex + 1) % suggestions.length;
    } else if (direction === 'up') {
      selectedSuggestionIndex = selectedSuggestionIndex <= 0 ? suggestions.length - 1 : selectedSuggestionIndex - 1;
    }
    
    // Highlight new suggestion
    suggestions[selectedSuggestionIndex].classList.add('highlighted');
    suggestions[selectedSuggestionIndex].scrollIntoView({ block: 'nearest' });
  }
  
  // Search input event handlers
  searchInput.addEventListener('input', function() {
    const searchTerm = this.value.trim().toLowerCase();
    
    if (searchTerm === '') {
      hideSuggestions();
      clearTimeout(searchTimeout);
      return;
    }
    
    // Show suggestions as user types (debounced)
    debouncedShowSuggestions(searchTerm);
  });
  
  // Handle Enter key and arrow key navigation
  searchInput.addEventListener('keydown', function(e) {
    const suggestions = searchSuggestions.querySelectorAll('.suggestion-item');
    
    switch(e.key) {
      case 'Enter':
        e.preventDefault();
        if (selectedSuggestionIndex >= 0 && suggestions[selectedSuggestionIndex]) {
          const accountIndex = parseInt(suggestions[selectedSuggestionIndex].dataset.index);
          selectAccountFromAPI(searchResultsCache[accountIndex]);
        } else if (searchResultsCache.length === 1) {
          // If only one result, select it automatically
          selectAccountFromAPI(searchResultsCache[0]);
        }
        break;
        
      case 'ArrowDown':
        e.preventDefault();
        navigateSuggestions('down');
        break;
        
      case 'ArrowUp':
        e.preventDefault();
        navigateSuggestions('up');
        break;
        
      case 'Escape':
        hideSuggestions();
        this.blur();
        break;
    }
  });
  
  // Hide suggestions when clicking outside
  document.addEventListener('click', function(e) {
    if (!e.target.closest('.search-box')) {
      hideSuggestions();
    }
  });

  // ---------- Multiple Accounts Functionality ----------
  
  // Function to add an account to the multiple accounts list
  function addAccountToList(accountData) {
    if (selectedAccounts.find(acc => acc.account.accountNumber === accountData.account.accountNumber)) {
      return; // already in list \u2014 silently ignore
    }
    
    selectedAccounts.push(accountData);
    
    // If this is the first account added, load its details automatically
    if (selectedAccounts.length === 1) {
      currentSelectedAccountIndex = 0;
      loadAccountData(accountData);
    }
    
    renderMultipleAccountsList();
    
    // Update total balance with new account included
    updateTotalBalance();
  }
  
  // Function to remove an account from the list
  function removeAccountFromList(accountNumber) {
    const removedIndex = selectedAccounts.findIndex(acc => acc.account.accountNumber === accountNumber);
    selectedAccounts = selectedAccounts.filter(acc => acc.account.accountNumber !== accountNumber);
    
    // If the removed account was currently selected, switch to another account
    if (removedIndex === currentSelectedAccountIndex) {
      // If there are remaining accounts, show the first one, otherwise clear details
      if (selectedAccounts.length > 0) {
        currentSelectedAccountIndex = 0;
        loadAccountData(selectedAccounts[0]);
      } else {
        clearAccountData();
        currentSelectedAccountIndex = -1;
      }
    } else if (removedIndex < currentSelectedAccountIndex) {
      // Adjust the current index if an account before it was removed
      currentSelectedAccountIndex--;
    }
    
    renderMultipleAccountsList();
    
    // Update total balance after account removal
    updateTotalBalance();
  }
  
  // Function to render the multiple accounts list
  function renderMultipleAccountsList() {
    if (!multipleAccountsMode || selectedAccounts.length === 0) {
      multipleAccountsList.style.display = 'none';
      return;
    }
    
    let totalAmount = 0;
    const rowsHTML = selectedAccounts.map((accountData, index) => {
      const acc = accountData.account;
      
      const selectedBill = accountData.bills.find(bill => bill.selected);
      let accountTotal = 0;
      
      if (selectedBill) {
        const amount  = selectedBill.amount;
        const pca     = selectedBill.pca != null ? selectedBill.pca : (selectedBill.pcaMmf || 0);
        const mmf     = selectedBill.mmf || 0;
        const penalty = selectedBill.penalty;
        accountTotal = amount + pca + mmf + penalty;
      }
      
      totalAmount += accountTotal;
      
      const isSelected = index === currentSelectedAccountIndex;
      return `
        <tr class="breakdown-row ${isSelected ? 'selected' : ''}" data-index="${index}">
          <td class="mono breakdown-accno">${acc.accountNumber}</td>
          <td class="breakdown-name">${acc.name}</td>
          <td class="mono breakdown-amount">${fmt(accountTotal)}</td>
          <td class="breakdown-action">
            <button class="btn-remove-account" data-account="${acc.accountNumber}">Remove</button>
          </td>
        </tr>
      `;
    }).join('');
    
    multipleAccountsList.innerHTML = `
      <div class="account-breakdown-card">
        <div class="account-breakdown-header">
          <span class="account-breakdown-title">Account Breakdown</span>
          <button class="btn-breakdown-clear" id="clearBreakdownBtn">Clear All</button>
        </div>
        <table class="account-breakdown-table">
          <thead>
            <tr>
              <th>Account Number</th>
              <th>Account Name</th>
              <th>Amount</th>
              <th style="width:70px;"></th>
            </tr>
          </thead>
          <tbody>
            ${rowsHTML}
          </tbody>
          <tfoot>
            <tr class="breakdown-total-row">
              <td colspan="2">Grand Total</td>
              <td class="mono">${fmt(totalAmount)}</td>
              <td></td>
            </tr>
          </tfoot>
        </table>
      </div>
    `;
    multipleAccountsList.style.display = 'block';
    
    // Row click \u2014 load that account's details
    multipleAccountsList.querySelectorAll('.breakdown-row').forEach((row, index) => {
      row.addEventListener('click', function(e) {
        if (e.target.closest('.btn-remove-account')) return;
        currentSelectedAccountIndex = index;
        loadAccountData(selectedAccounts[index]);
        renderMultipleAccountsList();
      });
    });
    
    // Remove buttons
    multipleAccountsList.querySelectorAll('.btn-remove-account').forEach(btn => {
      btn.addEventListener('click', function(e) {
        e.stopPropagation();
        removeAccountFromList(this.dataset.account);
      });
    });

    // Clear All button
    const clearBtn = multipleAccountsList.querySelector('#clearBreakdownBtn');
    if (clearBtn) {
      clearBtn.addEventListener('click', function() {
        selectedAccounts = [];
        currentSelectedAccountIndex = -1;
        forceClearAccountData();
        multipleAccountsList.style.display = 'none';
        updateTotalBalance();
      });
    }
    
    // Update invoice pill
    acctCountNum.textContent = selectedAccounts.length;
    acctCountLabel.textContent = selectedAccounts.length === 1 ? 'ACCOUNT' : 'ACCOUNTS';
    
    updateTotalBalance();
  }
  
  // Function to switch between single and multiple account modes
  function setMultipleAccountsMode(enabled) {
    multipleAccountsMode = enabled;
    
    if (enabled) {
      multipleToggle.classList.add('active');
      
      // If we have a current account loaded and no accounts in the list yet, add it
      if (fAccNo.textContent !== '\u2014' && fAccNo.textContent !== '' && selectedAccounts.length === 0) {
        const currentAccount = COLLECTION_ACCOUNTS.find(acc => 
          acc.account.accountNumber === fAccNo.textContent
        );
        if (currentAccount) {
          selectedAccounts.push(currentAccount);
          currentSelectedAccountIndex = 0;
          // Keep the current account details visible
        }
      }
      
      renderMultipleAccountsList();
      
      // If no accounts in list and no current details, start fresh
      if (selectedAccounts.length === 0 && fAccNo.textContent === '\u2014') {
        // nothing to show
      }
      
      // Update total balance for multiple mode
      updateTotalBalance();
    } else {
      multipleAccountsList.style.display = 'none';
      multipleToggle.classList.remove('active');
      selectedAccounts = [];
      currentSelectedAccountIndex = -1;
      
      // Reset label back to single mode
      totalBalanceLabel.textContent = 'Total Balance';
      
      // If we had accounts loaded, keep the last selected account visible
      // or clear if no account was selected
      updateTotalBalance();
    }
  }
  
  // Focusing the search input still adds more accounts in multiple mode

  // Function to load account data into the interface
  function loadAccountData(accountData) {
    const acc = accountData.account;

    // Enable amount tendered now that an account is loaded
    setTenderedEnabled(true);

    // Clear any previously applied discount when switching accounts
    activeDiscount = null;
    sDiscountLine.style.display = 'none';

    // Keep a reference so the Other Charges popover can read it
    window._ocCurrentAccount = acc;
    
    // Update account details
    document.getElementById('fAccNo').textContent = acc.accountNumber;
    document.getElementById('fMeter').textContent = acc.meterNumber;
    document.getElementById('fName').textContent = acc.name;
    document.getElementById('fAddress').textContent = acc.address;
    document.getElementById('fType').textContent = acc.type;
    document.getElementById('fOtherCharges').textContent = `\u20B1${acc.otherChargesBalance.toFixed(2)}`;
    
    // Update status badge (uses statusBadge declared in outer DOMContentLoaded scope)
    statusBadge.textContent = acc.status;
    statusBadge.className = acc.status === 'Connected' ? 'badge connected' : 'badge disconnected';
    
    // Update invoice info
    document.getElementById('invoiceInput').value = accountData.invoiceNo.replace(/^CR00/i, '');
    const acctCount = accountData.accountsInInvoice != null ? accountData.accountsInInvoice : 1;
    document.getElementById('acctCountNum').textContent = acctCount;
    document.getElementById('acctCountLabel').textContent = acctCount === 1 ? 'ACCOUNT' : 'ACCOUNTS';
    
    // Load bills (selectBillRow handles receipt PCA/MMF display)
    loadBills(accountData.bills);
  }
  
  // Function to clear account data (for no results)
  function clearAccountData() {
    // Don't clear if we're in multiple mode and have accounts selected
    if (multipleAccountsMode && selectedAccounts.length > 0) {
      return;
    }
    
    forceClearAccountData();
  }
  
  // Function to force clear account data regardless of mode
  function forceClearAccountData() {
    window._ocCurrentAccount = null;  // reset Other Charges popover reference
    document.getElementById('fAccNo').textContent = '\u2014';
    document.getElementById('fMeter').textContent = '\u2014';
    document.getElementById('fName').textContent = '\u2014';
    document.getElementById('fAddress').textContent = '\u2014';
    document.getElementById('fType').textContent = '\u2014';
    document.getElementById('fOtherCharges').textContent = '\u20B10.00';
    
    const statusBadge = document.getElementById('statusBadge');
    statusBadge.textContent = '\u2014';
    statusBadge.className = 'badge';
    
    document.getElementById('invoiceInput').value = '';
    
    if (!multipleAccountsMode) {
      document.getElementById('acctCountNum').textContent = '\u2014';
      document.getElementById('acctCountLabel').textContent = 'ACCOUNT';
    }
    
    // Clear bills table
    billsBody.innerHTML = '<tr><td colspan="7" style="text-align:center;color:var(--ink-muted);padding:20px;">No account selected</td></tr>';
    
    // Reset PCA/MMF to zero
    sPCA.textContent = fmt(0);
    sMMF.textContent = fmt(0);

    // Disable amount tendered until an account is loaded
    setTenderedEnabled(false);
  }
  
  // Function to load bills into the table
  function loadBills(bills) {
    billsBody.innerHTML = '';
    bills.forEach(bill => {
      const row = document.createElement('tr');
      row.className = 'bill-row';
      if (bill.selected) row.classList.add('selected');
      const pca = bill.pca != null ? bill.pca : 0;
      const mmf = bill.mmf != null ? bill.mmf : 0;
      row.setAttribute('data-amount', bill.amount.toFixed(2));
      row.setAttribute('data-penalty', bill.penalty.toFixed(2));
      row.setAttribute('data-pca', pca.toFixed(2));
      row.setAttribute('data-mmf', mmf.toFixed(2));
      row.setAttribute('data-month', bill.month);
      
      const penaltyCell = bill.penalty > 0
        ? `${fmt(bill.penalty)} <span class="tag">PENALTY</span>`
        : '\u2014';
      
      row.innerHTML = `
        <td><span class="radio ${bill.selected ? 'on' : ''}"></span></td>
        <td><b>${bill.month}</b></td>
        <td class="muted">${bill.dueDate}</td>
        <td class="mono">${fmt(bill.amount)}</td>
        <td class="mono muted">${fmt(pca + mmf)}</td>
        <td class="penalty-cell mono">${penaltyCell}</td>
        <td style="text-align:right;" class="subtotal-strong">${fmt(bill.amount + pca + mmf + bill.penalty)}</td>
      `;
      
      billsBody.appendChild(row);
      
      // Add click handler for row selection (using existing selectBillRow function)
      row.addEventListener('click', function() {
        selectBillRow(this);
      });
    });
    
    // Select the first selected bill or the first bill
    const selectedRow = billsBody.querySelector('.bill-row.selected') || billsBody.querySelector('.bill-row');
    if (selectedRow) selectBillRow(selectedRow);
  }
  
  // Function to recalculate totals
  function recalcTotals() {
    const selectedRow = billsBody.querySelector('.bill-row.selected');
    if (selectedRow) {
      selectBillRow(selectedRow, { skipRecalcTendered: true });
    }
  }

  // Function to calculate combined total for multiple accounts
  function calculateMultipleAccountsTotal() {
    if (!multipleAccountsMode || selectedAccounts.length === 0) {
      return 0;
    }
    
    let grandTotal = 0;
    
    selectedAccounts.forEach(accountData => {
      // Find the selected bill for this account, or use the first selected bill
      const selectedBill = accountData.bills.find(bill => bill.selected);
      
      if (selectedBill) {
        const amount  = selectedBill.amount;
        const pca     = selectedBill.pca != null ? selectedBill.pca : (selectedBill.pcaMmf || 0);
        const mmf     = selectedBill.mmf || 0;
        const penalty = selectedBill.penalty;
        const subTotal = amount + pca + mmf + penalty;
        grandTotal += subTotal;
      }
    });
    
    return grandTotal;
  }
  
  // Function to update total balance display
  function updateTotalBalance() {
    if (multipleAccountsMode && selectedAccounts.length > 0) {
      const multipleTotal = calculateMultipleAccountsTotal();
      totalBalance.textContent = fmt(multipleTotal);
      totalBalanceLabel.textContent = `Total Balance (${selectedAccounts.length} Account${selectedAccounts.length > 1 ? 's' : ''})`;
      amountToPay.value = multipleTotal.toFixed(2);
    } else {
      // Single account mode - use existing logic
      totalBalanceLabel.textContent = 'Total Balance';
      const selectedRow = billsBody.querySelector('.bill-row.selected');
      if (selectedRow) {
        const amount  = parseFloat(selectedRow.dataset.amount)  || 0;
        const penalty = parseFloat(selectedRow.dataset.penalty) || 0;
        const pca     = parseFloat(selectedRow.dataset.pca)     || 0;
        const mmf     = parseFloat(selectedRow.dataset.mmf)     || 0;
        const totalDue = amount + pca + mmf;
        const subTotal = totalDue + penalty;
        const discountAmt = activeDiscount ? parseFloat((subTotal * activeDiscount.rate).toFixed(2)) : 0;
        const finalTotal  = subTotal - discountAmt;
        totalBalance.textContent = fmt(finalTotal);
        amountToPay.value = finalTotal.toFixed(2);
      } else {
        // No row selected - reset totals to zero
        totalBalance.textContent = fmt(0);
        amountToPay.value = '0.00';
      }
    }
    recalcRemaining();
  }

  // ---------- Reset ----------
  resetBtn.addEventListener('click', function () {
    // Clear search input and hide suggestions
    searchInput.value = '';
    hideSuggestions();
    
    // Reset multiple accounts mode
    if (multipleAccountsMode) {
      selectedAccounts = [];
      currentSelectedAccountIndex = -1;
      setMultipleAccountsMode(false);
      // Turn off multiple toggle
      multipleToggle.classList.remove('active');
    }
    
    // Clear all account details
    forceClearAccountData();
    
    // Reset payment fields
    amountTendered.value = '';
    amountToPay.value = '0.00';
    
    // Reset payment method to cash (first option)
    document.querySelectorAll('.pay-opt').forEach(opt => opt.classList.remove('active'));
    document.querySelector('.pay-opt[data-mode="cash"]').classList.add('active');
    document.getElementById('checkPanel').style.display  = 'none';
    document.getElementById('onlinePanel').style.display = 'none';
    document.getElementById('checkBank').value    = '';
    document.getElementById('checkBranch').value  = '';
    document.getElementById('checkNumber').value  = '';
    document.getElementById('checkDate').value    = '';
    document.getElementById('onlineChannel').value = '';
    document.getElementById('onlineRef').value    = '';
    document.getElementById('onlineDate').value   = '';
    
    // Clear totals
    sTotalBill.textContent = '\u20B10.00';
    sTotalDue.textContent = '\u20B10.00';
    sPenalty.textContent = '\u20B10.00';
    sSubTotal.textContent = '\u20B10.00';
    totalBalance.textContent = '\u20B10.00';

    // Clear any active discount
    clearDiscount();
    totalBalanceLabel.textContent = 'Total Balance';
    
    // Recalculate everything
    recalcRemaining();
    
    // Show reset message \u2014 silent, no popup
  });

  // ---------- Multiple Accounts Toggle ----------
  multipleToggle.addEventListener('click', function () {
    const isCurrentlyActive = this.classList.contains('active');
    
    if (isCurrentlyActive) {
      this.classList.remove('active');
      setMultipleAccountsMode(false);
    } else {
      this.classList.add('active');
      setMultipleAccountsMode(true);
    }
  });

  // ---------- Action buttons ----------
  document.getElementById('payBtn').addEventListener('click', function () {
    const toPay    = parseAmount(amountToPay.value);
    const tendered = parseAmount(amountTendered.value);
    const invoiceNo = getFullInvoiceNo();

    if (!invoiceNo) {
      showToast('Please enter an invoice number before proceeding.', true);
      invoiceInput.focus();
      return;
    }
    if (tendered < toPay) {
      showToast('Amount tendered is less than amount to pay.', true);
      return;
    }
    showToast(`Payment of ${fmt(toPay)} recorded via ${currentPayMode().toUpperCase()} \u2014 Invoice ${invoiceNo}.`);
  });

  document.getElementById('reprintBtn').addEventListener('click', function () {
    const invoiceNo = getFullInvoiceNo();
    showToast(`Reprint Invoice${invoiceNo ? ' ' + invoiceNo : ''} \u2014 sent to printer.`);
  });

  document.getElementById('cancelBtn').addEventListener('click', function () {
    const invoiceNo = getFullInvoiceNo();
    showToast(`Invoice${invoiceNo ? ' ' + invoiceNo : ''} cancelled.`, true);
  });

  // ---------- Nav action bar \u2014 stub buttons ----------
  // Each button shows its own name as a toast until a real feature is wired.
  // NOTE: batchPaymentBtn  is wired separately in initBatchPaymentsModal() below.
  // NOTE: myCollectionBtn  is wired separately in initMyCollectionModal() below.
  // NOTE: invoiceSearchBtn is wired separately in initInvoiceSearchModal() below.
  // NOTE: reprintInvoiceBtn is wired separately in initReprintInvoiceModal() below.
  // NOTE: cancelInvoiceBtn  is wired separately in initCancelInvoiceModal() below.
  []/* no stubs remain */.forEach(function (item) {
    var el = document.getElementById(item.id);
    if (!el) return;
    el.addEventListener('click', function (e) {
      e.preventDefault();
      showToast(item.label);
    });
  });

  // ---------- Popover link chips ----------
  document.querySelectorAll('.link-chip').forEach(chip => {
    chip.addEventListener('click', function () {
      const modal = this.dataset.modal;
      if (modal === 'conversion') {
        if (typeof window.openConversionModal === 'function') window.openConversionModal();
      } else if (modal === 'discount') {
        if (typeof window.openDiscountModal === 'function') window.openDiscountModal();
      } else if (modal === 'others') {
        // Handled by initOtherChargesModal — its own listener on .link-chip[data-modal="others"]
        // No action needed here; avoids the Coming Soon fallback.
      } else {
        showComingSoonModal(this.textContent.trim());
      }
    });
  });

  // ---------- Toast ----------
  let toastTimer;
  function showToast(msg, danger) {
    const card     = document.getElementById('toast');
    const backdrop = document.getElementById('toastBackdrop');
    const title    = document.getElementById('toastTitle');
    const msgEl    = document.getElementById('toastMsg');
    const iconOk   = document.getElementById('toastIconSuccess');
    const iconErr  = document.getElementById('toastIconDanger');

    title.textContent = danger ? 'Error' : 'Success';
    msgEl.textContent = msg;
    iconOk.style.display  = danger ? 'none'  : 'block';
    iconErr.style.display = danger ? 'block' : 'none';

    card.className     = 'toast-card show' + (danger ? ' danger' : '');
    backdrop.className = 'toast-backdrop show';

    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => {
      card.className     = 'toast-card';
      backdrop.className = 'toast-backdrop';
    }, 2800);
  }

  // ---------- Coming Soon Modal ----------
  (function initComingSoonModal() {
    if (!document.getElementById('cs-modal-styles')) {
      const style = document.createElement('style');
      style.id = 'cs-modal-styles';
      style.textContent = `
        .cs-overlay {
          position: fixed; inset: 0;
          background: rgba(0,0,0,.55);
          display: flex; align-items: center; justify-content: center;
          z-index: 1200;
          opacity: 0; pointer-events: none;
          transition: opacity .2s;
        }
        .cs-overlay.show { opacity: 1; pointer-events: auto; }
        .cs-modal {
          background: var(--color-panel, #1e2537);
          border: 1px solid var(--color-border, #2e3a52);
          border-radius: 16px;
          box-shadow: 0 24px 60px rgba(0,0,0,.5);
          padding: 36px 40px 30px;
          width: 100%; max-width: 360px;
          text-align: center;
          transform: scale(.92); transition: transform .2s;
        }
        .cs-overlay.show .cs-modal { transform: scale(1); }
        .cs-modal-icon {
          width: 52px; height: 52px; border-radius: 50%;
          background: rgba(61,107,250,.15);
          color: var(--color-blue, #3d6bfa);
          display: flex; align-items: center; justify-content: center;
          margin: 0 auto 18px;
        }
        .cs-modal-icon svg { width: 26px; height: 26px; }
        .cs-modal-feature {
          font-size: 11px; font-weight: 700;
          text-transform: uppercase; letter-spacing: .8px;
          color: var(--color-blue, #3d6bfa); margin-bottom: 8px;
        }
        .cs-modal-title {
          font-size: 20px; font-weight: 700;
          color: var(--color-text, #e8ecf4); margin: 0 0 10px;
        }
        .cs-modal-body {
          font-size: 13.5px; color: var(--color-text-dim, #8a94a6);
          line-height: 1.55; margin-bottom: 26px;
        }
        .cs-modal-btn {
          display: inline-flex; align-items: center; justify-content: center;
          padding: 10px 32px; border-radius: 8px;
          background: var(--color-blue, #3d6bfa); color: #fff;
          font-size: 13px; font-weight: 600; font-family: inherit;
          border: none; cursor: pointer; transition: filter .12s;
        }
        .cs-modal-btn:hover { filter: brightness(1.1); }
      `;
      document.head.appendChild(style);
    }

    const overlay = document.createElement('div');
    overlay.className = 'cs-overlay';
    overlay.innerHTML = `
      <div class="cs-modal" role="dialog" aria-modal="true" aria-labelledby="csModalTitle">
        <div class="cs-modal-icon">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"/>
            <path d="M12 8v4M12 16h.01"/>
          </svg>
        </div>
        <div class="cs-modal-feature" id="csModalFeature"></div>
        <h3 class="cs-modal-title" id="csModalTitle">Coming Soon</h3>
        <p class="cs-modal-body">This feature is currently under development and will be available in a future update.</p>
        <button class="cs-modal-btn" id="csModalOkBtn">Got it</button>
      </div>
    `;
    document.body.appendChild(overlay);

    const featureEl = overlay.querySelector('#csModalFeature');
    const okBtn     = overlay.querySelector('#csModalOkBtn');

    function close() {
      overlay.classList.remove('show');
      document.body.style.overflow = '';
    }

    okBtn.addEventListener('click', close);
    overlay.addEventListener('click', e => { if (e.target === overlay) close(); });
    document.addEventListener('keydown', e => {
      if (e.key === 'Escape' && overlay.classList.contains('show')) close();
    });

    window.showComingSoonModal = function (featureName) {
      featureEl.textContent = featureName;
      overlay.classList.add('show');
      document.body.style.overflow = 'hidden';
      okBtn.focus();
    };
  }());

  // ---------- Senior Citizen Discount Modal ----------
  (function initDiscountModal() {

    if (!document.getElementById('disc-styles')) {
      const s = document.createElement('style');
      s.id = 'disc-styles';
      s.textContent = `
        .disc-overlay {
          position: fixed; inset: 0;
          background: rgba(0,0,0,.55);
          display: flex; align-items: center; justify-content: center;
          z-index: 1300;
          opacity: 0; pointer-events: none;
          transition: opacity .2s;
        }
        .disc-overlay.show { opacity: 1; pointer-events: auto; }
        .disc-modal {
          background: var(--color-panel, #1e2537);
          border: 1px solid var(--color-border, #2e3a52);
          border-radius: 16px;
          box-shadow: 0 24px 60px rgba(0,0,0,.5);
          width: 100%; max-width: 420px;
          transform: scale(.93); transition: transform .2s;
          overflow: hidden;
        }
        .disc-overlay.show .disc-modal { transform: scale(1); }

        .disc-header {
          display: flex; align-items: center; justify-content: space-between;
          padding: 18px 22px 14px;
          border-bottom: 1px solid var(--color-border, #2e3a52);
        }
        .disc-header-left { display: flex; align-items: center; gap: 10px; }
        .disc-header-icon {
          width: 34px; height: 34px; border-radius: 50%;
          background: rgba(48,209,88,.15);
          color: var(--color-green, #28c76f);
          display: flex; align-items: center; justify-content: center;
          flex-shrink: 0;
        }
        .disc-header-icon svg { width: 16px; height: 16px; }
        .disc-title {
          font-size: 13px; font-weight: 800;
          text-transform: uppercase; letter-spacing: .9px;
          color: var(--color-text, #e8ecf4); margin: 0;
        }
        .disc-subtitle {
          font-size: 11px; color: var(--color-text-dim, #8a94a6);
          margin-top: 1px;
        }
        .disc-close {
          background: none; border: none; cursor: pointer;
          color: var(--color-text-dim, #8a94a6);
          padding: 4px; border-radius: 6px;
          display: flex; align-items: center; justify-content: center;
          transition: color .15s, background .15s;
        }
        .disc-close:hover { color: var(--color-text, #e8ecf4); background: var(--color-border, #2e3a52); }

        .disc-body { padding: 22px; display: flex; flex-direction: column; gap: 16px; }

        .disc-label {
          font-size: 10.5px; font-weight: 700;
          text-transform: uppercase; letter-spacing: .5px;
          color: var(--color-text-dim, #8a94a6);
          margin-bottom: 6px; display: block;
        }

        /* Rate selector pills */
        .disc-rate-group { display: flex; gap: 8px; flex-wrap: wrap; }
        .disc-rate-pill {
          flex: 1; min-width: 70px;
          padding: 10px 6px; text-align: center;
          border: 1.5px solid var(--color-border, #2e3a52);
          border-radius: 8px; cursor: pointer;
          font-size: 13px; font-weight: 700;
          color: var(--color-text-dim, #8a94a6);
          background: var(--color-panel-alt, #252d3d);
          transition: all .15s;
          user-select: none;
        }
        .disc-rate-pill:hover { border-color: var(--color-green, #28c76f); color: var(--color-green, #28c76f); }
        .disc-rate-pill.active {
          border-color: var(--color-green, #28c76f);
          background: rgba(48,209,88,.12);
          color: var(--color-green, #28c76f);
        }

        /* SC ID fields */
        .disc-field { display: flex; flex-direction: column; gap: 5px; }
        .disc-input {
          width: 100%;
          background: var(--color-panel-alt, #252d3d);
          border: 1px solid var(--color-border, #2e3a52);
          border-radius: 8px;
          padding: 10px 13px;
          font-size: 13.5px; font-family: inherit;
          color: var(--color-text, #e8ecf4);
          outline: none;
          transition: border-color .15s, box-shadow .15s;
        }
        .disc-input::placeholder { color: var(--color-text-faint, #4a5568); }
        .disc-input:focus {
          border-color: var(--color-green, #28c76f);
          box-shadow: 0 0 0 3px rgba(48,209,88,.15);
        }
        .disc-input.disc-input--error {
          border-color: var(--color-red, #ff453a) !important;
          box-shadow: 0 0 0 3px rgba(255,69,58,.13) !important;
        }
        .disc-field-error {
          display: none; font-size: .73rem; color: #dc2626;
          font-weight: 500; padding-left: 2px;
        }

        /* Preview bar */
        .disc-preview {
          background: rgba(48,209,88,.1);
          border: 1px solid rgba(48,209,88,.25);
          border-radius: 8px;
          padding: 12px 14px;
          display: flex; justify-content: space-between; align-items: center;
        }
        .disc-preview-label { font-size: 12px; font-weight: 600; color: var(--color-green, #28c76f); }
        .disc-preview-amt {
          font-size: 16px; font-weight: 800;
          font-family: 'IBM Plex Mono', monospace;
          color: var(--color-green, #28c76f);
        }

        .disc-footer {
          padding: 14px 22px 20px;
          border-top: 1px solid var(--color-border, #2e3a52);
        }
        .disc-apply-btn {
          width: 100%; padding: 13px;
          background: var(--color-green, #28c76f);
          color: #fff; border: none; border-radius: 10px;
          font-size: 14px; font-weight: 700; font-family: inherit;
          letter-spacing: .5px; cursor: pointer; transition: filter .15s;
        }
        .disc-apply-btn:hover { filter: brightness(1.1); }
        .disc-apply-btn:disabled { opacity: .5; cursor: not-allowed; filter: none; }
      `;
      document.head.appendChild(s);
    }

    // Build markup
    const overlay = document.createElement('div');
    overlay.className = 'disc-overlay';
    overlay.id = 'discOverlay';
    overlay.setAttribute('aria-modal', 'true');
    overlay.setAttribute('role', 'dialog');
    overlay.setAttribute('aria-labelledby', 'discTitle');
    overlay.innerHTML = `
      <div class="disc-modal">
        <div class="disc-header">
          <div class="disc-header-left">
            <div class="disc-header-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                <circle cx="8" cy="8" r="2.2"/><circle cx="16" cy="16" r="2.2"/>
                <path d="M18 6L6 18"/>
              </svg>
            </div>
            <div>
              <div class="disc-title" id="discTitle">Senior Citizen Discount</div>
              <div class="disc-subtitle">PWD / Senior Citizen privilege card</div>
            </div>
          </div>
          <button class="disc-close" id="discCloseBtn" aria-label="Close">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4">
              <path d="M18 6L6 18M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <div class="disc-body">

          <!-- Discount rate -->
          <div class="disc-field">
            <span class="disc-label">Discount Rate</span>
            <div class="disc-rate-group" id="discRateGroup">
              <div class="disc-rate-pill active" data-rate="0.05">5%</div>
              <div class="disc-rate-pill" data-rate="0.10">10%</div>
              <div class="disc-rate-pill" data-rate="0.20">20%</div>
            </div>
          </div>

          <!-- SC ID Number -->
          <div class="disc-field">
            <span class="disc-label">SC / PWD ID Number</span>
            <input class="disc-input" id="discIdNumber" type="text" placeholder="e.g. SC-2024-001234" autocomplete="off" maxlength="30" />
            <span class="disc-field-error" id="discIdError">ID number is required.</span>
          </div>

          <!-- SC Name -->
          <div class="disc-field">
            <span class="disc-label">Senior Citizen / PWD Name</span>
            <input class="disc-input" id="discScName" type="text" placeholder="Full name on the card" autocomplete="off" maxlength="80" />
            <span class="disc-field-error" id="discNameError">Name is required.</span>
          </div>

          <!-- Discount preview -->
          <div class="disc-preview" id="discPreview">
            <span class="disc-preview-label">Discount Amount</span>
            <span class="disc-preview-amt" id="discPreviewAmt">- ₱0.00</span>
          </div>

        </div>

        <div class="disc-footer">
          <button class="disc-apply-btn" id="discApplyBtn">Apply Discount</button>
        </div>
      </div>
    `;
    document.body.appendChild(overlay);

    // DOM refs
    const closeBtn      = overlay.querySelector('#discCloseBtn');
    const rateGroup     = overlay.querySelector('#discRateGroup');
    const idInput       = overlay.querySelector('#discIdNumber');
    const nameInput     = overlay.querySelector('#discScName');
    const previewAmt    = overlay.querySelector('#discPreviewAmt');
    const applyBtn      = overlay.querySelector('#discApplyBtn');
    const idError       = overlay.querySelector('#discIdError');
    const nameError     = overlay.querySelector('#discNameError');

    let selectedRate = 0.05;

    // Helpers
    function fmtPHP(n) {
      return '\u20B1' + Number(n).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function getCurrentSubTotal() {
      // Read from the page receipt sub-total before discount
      const selectedRow = document.querySelector('#billsBody .bill-row.selected');
      if (!selectedRow) return 0;
      const amount  = parseFloat(selectedRow.dataset.amount)  || 0;
      const penalty = parseFloat(selectedRow.dataset.penalty) || 0;
      const pca     = parseFloat(selectedRow.dataset.pca)     || 0;
      const mmf     = parseFloat(selectedRow.dataset.mmf)     || 0;
      return amount + pca + mmf + penalty;
    }

    function updatePreview() {
      const base = getCurrentSubTotal();
      const discountAmt = parseFloat((base * selectedRate).toFixed(2));
      previewAmt.textContent = '- ' + fmtPHP(discountAmt);
    }

    // Rate pill selection
    rateGroup.addEventListener('click', function (e) {
      const pill = e.target.closest('.disc-rate-pill');
      if (!pill) return;
      rateGroup.querySelectorAll('.disc-rate-pill').forEach(p => p.classList.remove('active'));
      pill.classList.add('active');
      selectedRate = parseFloat(pill.dataset.rate);
      updatePreview();
    });

    // Live preview on input
    idInput.addEventListener('input', function () {
      idError.style.display = 'none';
      this.classList.remove('disc-input--error');
    });
    nameInput.addEventListener('input', function () {
      nameError.style.display = 'none';
      this.classList.remove('disc-input--error');
    });

    // Open / Close
    function openModal() {
      // Reset fields
      idInput.value   = '';
      nameInput.value = '';
      idError.style.display   = 'none';
      nameError.style.display = 'none';
      idInput.classList.remove('disc-input--error');
      nameInput.classList.remove('disc-input--error');
      // Default to 5%
      selectedRate = 0.05;
      rateGroup.querySelectorAll('.disc-rate-pill').forEach(p => {
        p.classList.toggle('active', parseFloat(p.dataset.rate) === 0.05);
      });
      updatePreview();
      overlay.classList.add('show');
      document.body.style.overflow = 'hidden';
      setTimeout(() => idInput.focus(), 180);
    }

    function closeModal() {
      overlay.classList.remove('show');
      document.body.style.overflow = '';
    }

    // Apply
    applyBtn.addEventListener('click', function () {
      let valid = true;

      if (!idInput.value.trim()) {
        idError.style.display = 'block';
        idInput.classList.add('disc-input--error');
        idInput.focus();
        valid = false;
      }
      if (!nameInput.value.trim()) {
        nameError.style.display = 'block';
        nameInput.classList.add('disc-input--error');
        if (valid) nameInput.focus();
        valid = false;
      }
      if (!valid) return;

      const rateLabel = Math.round(selectedRate * 100) + '%';
      // Call the page-level applyDiscount helper defined in the main DOMContentLoaded block
      if (typeof applyDiscount === 'function') {
        applyDiscount(selectedRate, `Senior Citizen (${rateLabel})`);
      }

      closeModal();
    });

    closeBtn.addEventListener('click', closeModal);
    overlay.addEventListener('click', e => { if (e.target === overlay) closeModal(); });
    document.addEventListener('keydown', e => {
      if (e.key === 'Escape' && overlay.classList.contains('show')) closeModal();
    });

    window.openDiscountModal = openModal;

  }());

  // ---------- Balance Conversion Modal ----------
  (function initConversionModal() {
    if (!document.getElementById('bconv-styles')) {
      const s = document.createElement('style');
      s.id = 'bconv-styles';
      s.textContent = `
        .bconv-overlay {
          position: fixed; inset: 0;
          background: rgba(0,0,0,.55);
          display: flex; align-items: center; justify-content: center;
          z-index: 1300;
          opacity: 0; pointer-events: none;
          transition: opacity .2s;
        }
        .bconv-overlay.show { opacity: 1; pointer-events: auto; }
        .bconv-modal {
          background: var(--color-panel, #1e2537);
          border: 1px solid var(--color-border, #2e3a52);
          border-radius: 16px;
          box-shadow: 0 24px 60px rgba(0,0,0,.5);
          width: 100%; max-width: 460px;
          transform: scale(.93); transition: transform .2s;
          overflow: hidden;
        }
        .bconv-overlay.show .bconv-modal { transform: scale(1); }
        .bconv-header {
          display: flex; align-items: center; justify-content: space-between;
          padding: 18px 22px 14px;
          border-bottom: 1px solid var(--color-border, #2e3a52);
        }
        .bconv-title {
          font-size: 13px; font-weight: 800;
          text-transform: uppercase; letter-spacing: .9px;
          color: var(--color-text, #e8ecf4); margin: 0;
        }
        .bconv-close {
          background: none; border: none; cursor: pointer;
          color: var(--color-text-dim, #8a94a6);
          padding: 4px; border-radius: 6px;
          display: flex; align-items: center; justify-content: center;
          transition: color .15s, background .15s;
        }
        .bconv-close:hover { color: var(--color-text, #e8ecf4); background: var(--color-border, #2e3a52); }
        .bconv-body { padding: 20px 22px; display: flex; flex-direction: column; gap: 14px; }
        .bconv-field { display: flex; flex-direction: column; gap: 5px; }
        .bconv-label {
          font-size: 10.5px; font-weight: 700;
          text-transform: uppercase; letter-spacing: .5px;
          color: var(--color-text-dim, #8a94a6);
        }
        .bconv-date-wrap { position: relative; }
        .bconv-date-wrap input[type="date"] {
          width: 100%;
          background: var(--color-panel-alt, #252d3d);
          border: 1px solid var(--color-border, #2e3a52);
          border-radius: 8px;
          padding: 10px 38px 10px 13px;
          font-size: 13.5px; font-family: inherit;
          color: var(--color-text, #e8ecf4);
          outline: none; transition: border-color .15s, box-shadow .15s;
          color-scheme: dark;
        }
        [data-theme="light"] .bconv-date-wrap input[type="date"] { color-scheme: light; }
        .bconv-date-wrap input[type="date"]:focus {
          border-color: var(--color-blue, #3d6bfa);
          box-shadow: 0 0 0 3px var(--color-blue-soft, rgba(61,107,250,.18));
        }
        .bconv-date-icon {
          position: absolute; right: 11px; top: 50%; transform: translateY(-50%);
          color: var(--color-text-dim, #8a94a6); pointer-events: none;
        }
        .bconv-select-wrap { position: relative; }
        .bconv-select-wrap select {
          width: 100%; appearance: none; -webkit-appearance: none;
          background: var(--color-panel-alt, #252d3d);
          border: 1px solid var(--color-border, #2e3a52);
          border-radius: 8px;
          padding: 11px 36px 11px 13px;
          font-size: 13.5px; font-family: inherit;
          color: var(--color-text, #e8ecf4);
          outline: none; cursor: pointer;
          transition: border-color .15s, box-shadow .15s;
        }
        .bconv-select-wrap select:focus {
          border-color: var(--color-blue, #3d6bfa);
          box-shadow: 0 0 0 3px var(--color-blue-soft, rgba(61,107,250,.18));
        }
        .bconv-select-arrow {
          position: absolute; right: 11px; top: 50%; transform: translateY(-50%);
          color: var(--color-text-dim, #8a94a6); pointer-events: none;
        }
        .bconv-amount-row { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
        .bconv-amount-box {
          background: var(--color-panel-alt, #252d3d);
          border: 1px solid var(--color-border, #2e3a52);
          border-radius: 8px; padding: 10px 13px;
        }
        .bconv-amount-box .bconv-label { margin-bottom: 4px; }
        .bconv-amount-val {
          font-size: 18px; font-weight: 700;
          font-family: 'IBM Plex Mono', monospace;
          color: var(--color-blue, #3d6bfa);
        }
        .bconv-amount-unit {
          font-size: 12px; font-weight: 500;
          color: var(--color-text-dim, #8a94a6); padding-top: 2px;
        }
        .bconv-textarea {
          width: 100%; resize: vertical; min-height: 68px;
          background: var(--color-panel-alt, #252d3d);
          border: 1px solid var(--color-border, #2e3a52);
          border-radius: 8px; padding: 10px 13px;
          font-size: 13.5px; font-family: inherit;
          color: var(--color-text, #e8ecf4);
          outline: none; transition: border-color .15s, box-shadow .15s;
        }
        .bconv-textarea::placeholder { color: var(--color-text-faint, #4a5568); }
        .bconv-textarea:focus {
          border-color: var(--color-blue, #3d6bfa);
          box-shadow: 0 0 0 3px var(--color-blue-soft, rgba(61,107,250,.18));
        }
        .bconv-bill-list {
          border: 1px solid var(--color-border, #2e3a52);
          border-radius: 8px; overflow: hidden;
        }
        .bconv-bill-row {
          display: flex; justify-content: space-between; align-items: center;
          padding: 11px 14px;
          border-bottom: 1px solid var(--color-border, #2e3a52);
          font-size: 13px;
        }
        .bconv-bill-row:last-child { border-bottom: none; }
        .bconv-bill-month { font-weight: 600; color: var(--color-text, #e8ecf4); }
        .bconv-bill-amount {
          font-family: 'IBM Plex Mono', monospace;
          font-weight: 700; font-size: 13px;
          color: var(--color-text, #e8ecf4);
        }
        .bconv-empty {
          padding: 14px; text-align: center; font-size: 13px;
          color: var(--color-text-dim, #8a94a6); font-style: italic;
        }
        .bconv-footer {
          padding: 14px 22px 20px;
          border-top: 1px solid var(--color-border, #2e3a52);
        }
        .bconv-save-btn {
          width: 100%; padding: 13px;
          background: var(--color-green, #28c76f);
          color: #fff; border: none; border-radius: 10px;
          font-size: 14px; font-weight: 700; font-family: inherit;
          letter-spacing: .5px; cursor: pointer; transition: filter .15s;
        }
        .bconv-save-btn:hover { filter: brightness(1.1); }
        .bconv-save-btn:disabled { opacity: .55; cursor: not-allowed; filter: none; }
      `;
      document.head.appendChild(s);
    }

    const overlay = document.createElement('div');
    overlay.className = 'bconv-overlay';
    overlay.id = 'bconvOverlay';
    overlay.setAttribute('aria-modal', 'true');
    overlay.setAttribute('role', 'dialog');
    overlay.setAttribute('aria-labelledby', 'bconvTitle');
    overlay.innerHTML = `
      <div class="bconv-modal">
        <div class="bconv-header">
          <h2 class="bconv-title" id="bconvTitle">Balance Conversion</h2>
          <button class="bconv-close" id="bconvCloseBtn" aria-label="Close">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4">
              <path d="M18 6L6 18M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="bconv-body">
          <div class="bconv-field">
            <label class="bconv-label">Date Processed</label>
            <div class="bconv-date-wrap">
              <input type="date" id="bconvDate" />
              <svg class="bconv-date-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="3" y="4" width="18" height="18" rx="2"/>
                <path d="M16 2v4M8 2v4M3 10h18"/>
              </svg>
            </div>
          </div>
          <div class="bconv-field">
            <label class="bconv-label">Payment Term</label>
            <div class="bconv-select-wrap">
              <select id="bconvPaymentTerm">
                <option value="">-- Select Payment Term --</option>
                <option value="3">Installment (3 months)</option>
                <option value="6">Installment (6 months)</option>
                <option value="12">Installment (12 months)</option>
                <option value="full">Full Payment</option>
              </select>
              <svg class="bconv-select-arrow" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                <path d="M6 9l6 6 6-6"/>
              </svg>
            </div>
          </div>
          <div class="bconv-amount-row">
            <div class="bconv-amount-box">
              <div class="bconv-label">Total Amount</div>
              <div class="bconv-amount-val" id="bconvTotalAmount">0.00</div>
            </div>
            <div class="bconv-amount-box">
              <div class="bconv-label">Basis</div>
              <div class="bconv-amount-unit">Monthly Due (Sub-Total)</div>
            </div>
          </div>
          <div class="bconv-field">
            <label class="bconv-label">Approved By</label>
            <div class="bconv-select-wrap">
              <select id="bconvApprovedBy">
                <option value="">-- Select Approver --</option>
                <option>General Manager</option>
                <option>Operations Manager</option>
                <option>Finance Officer</option>
                <option>Department Head</option>
              </select>
              <svg class="bconv-select-arrow" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                <path d="M6 9l6 6 6-6"/>
              </svg>
            </div>
          </div>
          <div class="bconv-field">
            <label class="bconv-label">Reason</label>
            <textarea class="bconv-textarea" id="bconvReason" placeholder="Enter reason for balance conversion..." rows="3"></textarea>
          </div>
          <div class="bconv-field">
            <label class="bconv-label">Total Bill</label>
            <div class="bconv-bill-list" id="bconvBillList">
              <div class="bconv-empty">No bill selected.</div>
            </div>
          </div>
        </div>
        <div class="bconv-footer">
          <button class="bconv-save-btn" id="bconvSaveBtn">SAVE</button>
        </div>
      </div>
    `;
    document.body.appendChild(overlay);

    const closeBtn   = overlay.querySelector('#bconvCloseBtn');
    const dateInput  = overlay.querySelector('#bconvDate');
    const totalAmtEl = overlay.querySelector('#bconvTotalAmount');
    const billListEl = overlay.querySelector('#bconvBillList');
    const saveBtn    = overlay.querySelector('#bconvSaveBtn');

    function fmtMono(n) {
      return Number(n).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
    function parseAmt(text) {
      return parseFloat(String(text).replace(/[\u20B1,]/g, '')) || 0;
    }

    function populate() {
      dateInput.value = new Date().toISOString().slice(0, 10);
      const subTotalEl = document.getElementById('sSubTotal');
      totalAmtEl.textContent = fmtMono(subTotalEl ? parseAmt(subTotalEl.textContent) : 0);
      const billRows = Array.from(document.querySelectorAll('#billsBody .bill-row.selected'));
      const rows = billRows.length ? billRows : Array.from(document.querySelectorAll('#billsBody .bill-row')).slice(0, 1);
      if (rows.length) {
        billListEl.innerHTML = rows.map(row => `
          <div class="bconv-bill-row">
            <span class="bconv-bill-month">${row.dataset.month || '--'}</span>
            <span class="bconv-bill-amount">${fmtMono(parseFloat(row.dataset.amount) || 0)}</span>
          </div>
        `).join('');
      } else {
        billListEl.innerHTML = '<div class="bconv-empty">No bill selected.</div>';
      }
    }

    function openModal() {
      populate();
      overlay.classList.add('show');
      document.body.style.overflow = 'hidden';
      setTimeout(() => dateInput.focus(), 180);
    }

    function closeModal() {
      overlay.classList.remove('show');
      document.body.style.overflow = '';
    }

    saveBtn.addEventListener('click', function () {
      const paymentTerm = overlay.querySelector('#bconvPaymentTerm').value;
      const approvedBy  = overlay.querySelector('#bconvApprovedBy').value;
      const reason      = overlay.querySelector('#bconvReason').value.trim();
      if (!paymentTerm) { overlay.querySelector('#bconvPaymentTerm').focus(); return; }
      if (!approvedBy)  { overlay.querySelector('#bconvApprovedBy').focus();  return; }
      if (!reason)      { overlay.querySelector('#bconvReason').focus();       return; }

      closeModal();

      // Show success toast
      const card     = document.getElementById('toast');
      const backdrop = document.getElementById('toastBackdrop');
      if (card) {
        document.getElementById('toastTitle').textContent    = 'Success';
        document.getElementById('toastMsg').textContent      = 'Balance conversion saved successfully.';
        document.getElementById('toastIconSuccess').style.display = 'block';
        document.getElementById('toastIconDanger').style.display  = 'none';
        card.className     = 'toast-card show';
        backdrop.className = 'toast-backdrop show';
        clearTimeout(window._bconvToastTimer);
        window._bconvToastTimer = setTimeout(() => {
          card.className     = 'toast-card';
          backdrop.className = 'toast-backdrop';
        }, 2800);
      }
    });

    closeBtn.addEventListener('click', closeModal);
    overlay.addEventListener('click', e => { if (e.target === overlay) closeModal(); });
    document.addEventListener('keydown', e => {
      if (e.key === 'Escape' && overlay.classList.contains('show')) closeModal();
    });

    window.openConversionModal = openModal;
  }());

  // ---------- Scanner Button ----------
  (function initScanner() {
    const scannerBtn = document.getElementById('scannerBtn');
    const scannerDot = document.getElementById('scannerDot');
    let hidDevice        = null;   // connected WebHID device
    let scannerBuffer    = '';
    let scannerTimer     = null;

    // ---- State helpers ----
    function setScannerState(connected, label) {
      scannerDot.className = 'scanner-status-dot' + (connected ? ' connected' : '');
      scannerBtn.classList.toggle('scanner-active', connected);
      scannerBtn.title = label;
    }

    function showScannerMsg(msg, isError) {
      // Reuse the toast card but with a scanner-specific title
      const card     = document.getElementById('toast');
      const backdrop = document.getElementById('toastBackdrop');
      const title    = document.getElementById('toastTitle');
      const msgEl    = document.getElementById('toastMsg');
      const iconOk   = document.getElementById('toastIconSuccess');
      const iconErr  = document.getElementById('toastIconDanger');

      title.textContent = isError ? 'Scanner' : 'Scanner';
      msgEl.textContent = msg;
      iconOk.style.display  = isError ? 'none'  : 'block';
      iconErr.style.display = isError ? 'block' : 'none';
      card.className     = 'toast-card show' + (isError ? ' danger' : '');
      backdrop.className = 'toast-backdrop show';
      clearTimeout(window._scannerToastTimer);
      window._scannerToastTimer = setTimeout(() => {
        card.className     = 'toast-card';
        backdrop.className = 'toast-backdrop';
      }, 3000);
    }

    // ---- Disconnect ----
    async function disconnectScanner() {
      if (hidDevice) {
        try { await hidDevice.close(); } catch (_) {}
        hidDevice = null;
      }
      setScannerState(false, 'Connect physical scanner');
      showScannerMsg('Scanner disconnected.', false);
    }

    // ---- Connect via WebHID ----
    async function connectScanner() {
      // Check API support
      if (!navigator.hid) {
        showScannerMsg(
          'WebHID is not supported in this browser. Try Chrome or Edge.',
          true
        );
        return;
      }

      try {
        // Prompt the user to pick a HID device (no filter = shows all)
        const [device] = await navigator.hid.requestDevice({ filters: [] });

        if (!device) {
          showScannerMsg('No device selected.', true);
          return;
        }

        await device.open();
        hidDevice = device;

        setScannerState(true, `Connected: ${device.productName} \u2014 click to disconnect`);
        showScannerMsg(`Scanner connected: ${device.productName}`, false);

        // HID input reports \u2014 most USB barcode scanners send keyboard-like HID data.
        // We read raw bytes but also keep the keyboard fallback (below) as the
        // primary decode path since it works without HID parsing tables.
        device.addEventListener('inputreport', () => {
          // The keyboard-event fallback handles character assembly; this listener
          // is here so the connection stays active and we can detect disconnect.
        });

        device.addEventListener('disconnect', () => {
          hidDevice = null;
          setScannerState(false, 'Connect physical scanner');
          showScannerMsg('Scanner was disconnected.', true);
        });

      } catch (err) {
        if (err.name === 'NotFoundError' || err.message.includes('No device')) {
          showScannerMsg('No HID device found or access was denied.', true);
        } else if (err.name === 'SecurityError') {
          showScannerMsg('Access to HID devices was blocked by the browser.', true);
        } else {
          showScannerMsg('Could not connect to scanner: ' + err.message, true);
        }
      }
    }

    // ---- Button click ----
    scannerBtn.addEventListener('click', function () {
      if (hidDevice) {
        disconnectScanner();
      } else {
        connectScanner();
      }
    });

    // ---- Keyboard fallback: USB scanners emulate a keyboard ----
    // This fires regardless of WebHID \u2014 most scanners work this way even
    // without WebHID permissions. It assembles chars and fires on Enter.
    document.addEventListener('keydown', function (e) {
      if (!hidDevice) return;   // only active while a scanner is connected
      const tag = document.activeElement.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;

      clearTimeout(scannerTimer);

      if (e.key === 'Enter') {
        if (scannerBuffer.length > 2) {
          searchInput.value = scannerBuffer;
          searchInput.dispatchEvent(new Event('input'));
        }
        scannerBuffer = '';
        return;
      }

      if (e.key.length === 1) scannerBuffer += e.key;
      scannerTimer = setTimeout(() => { scannerBuffer = ''; }, 80);
    });

  }());

  // ---------- Teller Widget ----------
  (function initTellerWidget() {
    const tellerDate   = document.getElementById('tellerDate');
    const tellerCash   = document.getElementById('tellerCash');
    const tellerName   = document.getElementById('tellerName');
    const tellerAvatar = document.getElementById('tellerAvatar');

    // Populate teller dropdown from SAMPLE_TELLERS (collection_sample.js)
    // TODO: replace with fetch('/api/Billing/Tellers').then(r => r.json())
    const tellers = (typeof SAMPLE_TELLERS !== 'undefined') ? SAMPLE_TELLERS : [];
    tellers.forEach(function (name) {
      const opt = document.createElement('option');
      opt.value = name;
      opt.textContent = name;
      tellerName.appendChild(opt);
    });

    // Seed today's date into the date picker
    const today = new Date();
    tellerDate.value = today.toISOString().slice(0, 10);

    // Avatar initials from selected teller name
    function updateAvatar() {
      const name  = tellerName.value.trim();
      const parts = name.split(' ').filter(Boolean);
      tellerAvatar.textContent = parts.length >= 2
        ? (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
        : parts[0]?.slice(0, 2).toUpperCase() || '?';
    }
    updateAvatar();

    // Update avatar whenever selection changes
    tellerName.addEventListener('change', updateAvatar);

    // Cash on-hand \u2014 numeric only, formats on blur
    tellerCash.addEventListener('keydown', function (e) {
      const allowed = ['Backspace','Delete','ArrowLeft','ArrowRight','Tab','Home','End'];
      if (allowed.includes(e.key)) return;
      if (e.key === '.' && !this.value.includes('.')) return;
      if (!/^\d$/.test(e.key)) e.preventDefault();
    });

    tellerCash.addEventListener('input', function () {
      let val = this.value.replace(/[^0-9.]/g, '');
      const parts = val.split('.');
      if (parts.length > 2) val = parts[0] + '.' + parts.slice(1).join('');
      this.value = val;
    });

    tellerCash.addEventListener('blur', function () {
      const num = parseFloat(this.value) || 0;
      this.value = num.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    });

    tellerCash.addEventListener('focus', function () {
      // Strip formatting so user can edit the raw number
      this.value = this.value.replace(/,/g, '');
    });
  }());

  // ---------- Kick things off ----------
  renderFromSample();

});


