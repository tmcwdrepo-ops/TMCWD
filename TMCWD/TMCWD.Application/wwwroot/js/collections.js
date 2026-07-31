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

  function fmt(n) {
    return '\u20B1' + Number(n).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  function parseAmount(text) {
    return parseFloat(String(text).replace(/[\u20B1,]/g, '')) || 0;
  }

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
    sSubTotal.textContent  = fmt(subTotal);
    
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
  
  // Function to show suggestions based on search term
  function showSuggestions(searchTerm) {
    if (searchTerm.length < 1) {
      hideSuggestions();
      return;
    }
    
    // Find matching accounts
    const matches = COLLECTION_ACCOUNTS.filter(account => {
      const acc = account.account;
      return acc.accountNumber.toLowerCase().includes(searchTerm) ||
             acc.name.toLowerCase().includes(searchTerm) ||
             acc.meterNumber.toLowerCase().includes(searchTerm);
    });
    
    if (matches.length === 0) {
      searchSuggestions.innerHTML = '<div class="no-suggestions">No accounts found</div>';
      searchSuggestions.style.display = 'block';
      return;
    }
    
    // Build suggestions HTML
    const suggestionsHTML = matches.map((account, index) => {
      const acc = account.account;
      return `
        <div class="suggestion-item" data-index="${index}" data-account-index="${COLLECTION_ACCOUNTS.indexOf(account)}">
          <div class="account-number">${acc.accountNumber}</div>
          <div class="account-name">${acc.name}</div>
          <div class="account-details">${acc.meterNumber} \u2022 ${acc.type} \u2022 ${acc.address}</div>
        </div>
      `;
    }).join('');
    
    searchSuggestions.innerHTML = suggestionsHTML;
    searchSuggestions.style.display = 'block';
    selectedSuggestionIndex = -1;
    
    // Add click handlers to suggestions
    searchSuggestions.querySelectorAll('.suggestion-item').forEach(item => {
      item.addEventListener('click', function() {
        const accountIndex = parseInt(this.dataset.accountIndex);
        selectAccount(COLLECTION_ACCOUNTS[accountIndex]);
      });
    });
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
      return;
    }
    
    // Show suggestions as user types
    showSuggestions(searchTerm);
  });
  
  // Handle Enter key and arrow key navigation
  searchInput.addEventListener('keydown', function(e) {
    const suggestions = searchSuggestions.querySelectorAll('.suggestion-item');
    
    switch(e.key) {
      case 'Enter':
        e.preventDefault();
        if (selectedSuggestionIndex >= 0 && suggestions[selectedSuggestionIndex]) {
          const accountIndex = parseInt(suggestions[selectedSuggestionIndex].dataset.accountIndex);
          selectAccount(COLLECTION_ACCOUNTS[accountIndex]);
        } else {
          const searchTerm = this.value.trim().toLowerCase();
          if (searchTerm === '') return;
          
          const foundAccount = COLLECTION_ACCOUNTS.find(account => {
            const acc = account.account;
            return acc.accountNumber.toLowerCase().includes(searchTerm) ||
                   acc.name.toLowerCase().includes(searchTerm) ||
                   acc.meterNumber.toLowerCase().includes(searchTerm);
          });
          
          if (foundAccount) {
            selectAccount(foundAccount);
          } else {
            hideSuggestions();
          }
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
        totalBalance.textContent = fmt(subTotal);
        amountToPay.value = subTotal.toFixed(2);
      } else {
        // No row selected \u2014 reset totals to zero
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
  [
    { id: 'cancelInvoiceBtn', label: 'Cancel Invoice' }
  ].forEach(function (item) {
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
      // Modal not yet connected \u2014 no popup
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
    const tellerDate   = document.getElementById('tellerDate');   // now a date input
    const tellerCash   = document.getElementById('tellerCash');
    const tellerName   = document.getElementById('tellerName');   // now a <select>
    const tellerAvatar = document.getElementById('tellerAvatar');

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


// ============================================================
//  Collection Remittance Modal \u2014 open / close 
// ============================================================
(function initRemittanceModal() {
  const overlay     = document.getElementById('crOverlay');
  const closeBtn    = document.getElementById('crCloseBtn');
  const dcrBtn      = document.getElementById('dcrBtn');
  const crDateInput = document.getElementById('crDate');

  if (!overlay || !dcrBtn) return;

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
  // TODO: replace with a real API call to load active collectors.
  // ----------------------------------------------------------
  const COLLECTORS = [
    'Elmer Pangilinan',
    'Maria Santos',
    'Jose Reyes',
    'Ana Garcia',
    'Pedro Cruz'
  ];

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

// ============================================================
//  Collection Report Modal \u2014 final wiring & cleanup
//
//  Naming conventions match the Collection Remittance modal
//  (initRemittanceModal / initRemittanceFields / etc.) that
//  precedes this block in the file.
// ============================================================
(function initCollectionReportModal() {
  'use strict';

  // â”€â”€ DOM refs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Central state object â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Single source of truth \u2014 all field listeners write here;
  // both export functions read from here at click time.
  window.reportState = {
    fromDate:  '',   // ISO 'YYYY-MM-DD'
    toDate:    '',   // ISO 'YYYY-MM-DD'
    collector: '',   // '' = All Collectors
    sortBy:    'OR'  // 'OR' | 'accountNo'
  };

  // â”€â”€ In-flight guard â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Set to true while an export Promise is pending.
  // closeModal() checks this to abort cleanly mid-export.
  var _exportInFlight = false;

  // â”€â”€ Helpers: today â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  function todayISO() {
    return new Date().toISOString().slice(0, 10);
  }

  // â”€â”€ To-date inline error â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Export-area inline error â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Collector dropdown â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Date seeding â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  function seedDates() {
    var today = todayISO();
    fromInput.value             = today;
    toInput.value               = today;
    window.reportState.fromDate = today;
    window.reportState.toDate   = today;
    clearToDateError();
  }

  // â”€â”€ Radio label sync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  function syncRadioLabels() {
    [radioOR, radioAcct].forEach(function (r) {
      if (!r) return;
      r.closest('.colrep-radio-label').classList.toggle(
        'colrep-radio-label--selected', r.checked
      );
    });
  }

  // â”€â”€ Full state reset â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Button loading / restore â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Open â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  function openModal() {
    resetState();
    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';
    closeBtn.focus();
  }

  // â”€â”€ Close â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Validation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Field event wiring â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Open / close event listeners â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ PDF export â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Excel export â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Success toast â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ One-time style injection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

  // â”€â”€ Initial setup â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  populateCollectors();
  seedDates();
  syncRadioLabels();

}());


// ============================================================
//  Collection Monitoring Modal
//  Phases 2 \u2013 4: data loading, rendering, pagination, cleanup.
//  Naming follows the cr-* / colrep-* pattern used by the
//  Collection Remittance and Collection Report modals above.
// ============================================================
(function initCollectionMonitoringModal() {
  'use strict';

  // â”€â”€ DOM refs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  var overlay       = document.getElementById('cmOverlay');
  var openBtn       = document.getElementById('collectionMonitoringBtn');
  var closeBtn      = document.getElementById('cmCloseBtn');
  var tableBody     = document.getElementById('cmTableBody');
  var totalAmountEl = document.getElementById('totalCollectionAmount');
  var rowsSelect    = document.getElementById('rowsPerPageSelect');
  var rangeLabel    = document.getElementById('paginationRangeLabel');
  var prevBtn       = document.getElementById('prevPageBtn');
  var nextBtn       = document.getElementById('nextPageBtn');

  // Guard \u2014 nothing to wire if any element is absent.
  if (!overlay || !openBtn || !closeBtn || !tableBody || !totalAmountEl ||
      !rowsSelect || !rangeLabel || !prevBtn || !nextBtn) return;

  // â”€â”€ Full dataset â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Populated once per open; every render function reads from here
  // so all views (table, total, pagination) share a single source.
  var monitoringData = [];

  // â”€â”€ Pagination state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Defaults match the <select> default value (5) and always
  // reset to page 1 on open so the modal reopens fresh.
  var monitoringPagination = {
    currentPage: 1,
    rowsPerPage: 5
  };

  // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  function fmtCurrency(amount) {
    return '\u20B1' + Number(amount).toLocaleString('en-PH', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  // â”€â”€ Data loading â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // TODO: replace the mock below with a real API call, e.g.:
  //
  //   return fetch('/api/Billing/CollectionMonitoring')
  //     .then(function (r) {
  //       if (!r.ok) throw new Error(r.statusText);
  //       return r.json();
  //     });
  //
  // The returned array must contain objects shaped as:
  //   { collector, orFrom, orTo, cashOnHand }
  function loadCollectionMonitoringData() {
    return Promise.resolve(
      (typeof SAMPLE_COLLECTION_MONITORING !== 'undefined') ? SAMPLE_COLLECTION_MONITORING : []
    );
  }

  // â”€â”€ Total collection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Sums the FULL dataset \u2014 never the current page slice.
  // Called once after load; pagination never calls this again.
  function calculateTotalCollection(data) {
    var total = data.reduce(function (sum, item) {
      return sum + (item.cashOnHand || 0);
    }, 0);
    totalAmountEl.textContent = fmtCurrency(total);
  }

  // â”€â”€ Table rendering â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Accepts whatever slice (or full array) is passed to it.
  // Empty-state message shown when the slice has zero rows.
  function renderCollectionMonitoringTable(data) {
    tableBody.innerHTML = '';

    if (!data || data.length === 0) {
      var emptyRow = document.createElement('tr');
      emptyRow.innerHTML = '<td colspan="4" class="cm-empty-row">No collections found.</td>';
      tableBody.appendChild(emptyRow);
      return;
    }

    data.forEach(function (item) {
      var row = document.createElement('tr');
      row.innerHTML =
        '<td>' + item.collector              + '</td>' +
        '<td>' + item.orFrom                 + '</td>' +
        '<td>' + item.orTo                   + '</td>' +
        '<td>' + fmtCurrency(item.cashOnHand)+ '</td>';
      tableBody.appendChild(row);
    });
  }

  // â”€â”€ Page slice â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Pure helper \u2014 no side effects. Returns the correct sub-array
  // for the given pagination state.
  function getPageSlice(data, pagination) {
    var start = (pagination.currentPage - 1) * pagination.rowsPerPage;
    var end   = start + pagination.rowsPerPage;
    return data.slice(start, end);
  }

  // â”€â”€ Pagination controls â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Updates range label and enables / disables arrow buttons.
  // Also clamps currentPage when rowsPerPage shrinks the range.
  function updatePaginationControls(data, pagination) {
    var total      = data.length;
    var totalPages = Math.max(1, Math.ceil(total / pagination.rowsPerPage));

    // Clamp \u2014 prevents currentPage from sitting beyond the last page.
    if (pagination.currentPage > totalPages) {
      pagination.currentPage = totalPages;
    }

    var start = total === 0 ? 0 : (pagination.currentPage - 1) * pagination.rowsPerPage + 1;
    var end   = Math.min(pagination.currentPage * pagination.rowsPerPage, total);

    rangeLabel.textContent = total === 0
      ? '0\u20130 of 0'
      : start + '\u2013' + end + ' of ' + total;

    // Disable arrows at the boundaries; both disabled when no data.
    prevBtn.disabled = total === 0 || pagination.currentPage <= 1;
    nextBtn.disabled = total === 0 || pagination.currentPage >= totalPages;
  }

  // â”€â”€ Central view refresh â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Single call-site for anything that changes what is visible.
  // Reads monitoringData directly so callers cannot pass a stale copy.
  function refreshView() {
    var slice = getPageSlice(monitoringData, monitoringPagination);
    renderCollectionMonitoringTable(slice);
    updatePaginationControls(monitoringData, monitoringPagination);
  }

  // â”€â”€ Pagination resets â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Restores state to defaults \u2014 called on close so the modal
  // always reopens at page 1 with the default row count.
  function resetPagination() {
    monitoringPagination.currentPage = 1;
    monitoringPagination.rowsPerPage = 5;
    rowsSelect.value = '5';
  }

  // â”€â”€ Rows-per-page select â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  rowsSelect.addEventListener('change', function () {
    monitoringPagination.rowsPerPage = parseInt(this.value, 10);
    monitoringPagination.currentPage = 1;   // always back to page 1
    refreshView();
  });

  // â”€â”€ Prev / Next buttons â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  prevBtn.addEventListener('click', function () {
    if (monitoringPagination.currentPage > 1) {
      monitoringPagination.currentPage--;
      refreshView();
    }
  });

  nextBtn.addEventListener('click', function () {
    var totalPages = Math.ceil(monitoringData.length / monitoringPagination.rowsPerPage);
    if (monitoringPagination.currentPage < totalPages) {
      monitoringPagination.currentPage++;
      refreshView();
    }
  });

  // â”€â”€ Open â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  function openModal() {
    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';
    closeBtn.focus();

    loadCollectionMonitoringData()
      .then(function (data) {
        // Store the full dataset \u2014 all functions read from here.
        monitoringData = data;

        // Reset to page 1 with the default row count before rendering.
        resetPagination();

        // Total is set once from the full dataset and never changes
        // as the user pages through \u2014 satisfies Phase 2 & 4 requirements.
        calculateTotalCollection(monitoringData);

        // Render the first page slice and sync pagination controls.
        refreshView();
      })
      .catch(function (err) {
        console.error('[CollectionMonitoring] Data load error:', err);
        // Show error state; disable controls so nothing is clickable.
        tableBody.innerHTML = '<tr><td colspan="4" class="cm-empty-row">Failed to load data. Please try again.</td></tr>';
        totalAmountEl.textContent = '0.00';
        rangeLabel.textContent    = '0\u20130 of 0';
        prevBtn.disabled = true;
        nextBtn.disabled = true;
      });
  }

  // â”€â”€ Close â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  // Resets pagination on every close so the modal always reopens
  // at page 1 \u2014 it never remembers the last page viewed.
  function closeModal() {
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
    resetPagination();
    openBtn.focus();
  }

  // â”€â”€ Event listeners â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  openBtn.addEventListener('click', function (e) {
    e.preventDefault();
    openModal();
  });

  closeBtn.addEventListener('click', closeModal);

  // Dim overlay click (outside the card) â†’ close
  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });

  // Escape key â†’ close (only when this modal is the visible one)
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

}());



// ============================================================
//  Batch Payments Modal
//  Phase 1: UI shell.
//  Phase 2: data loading & table rendering.
//  Phase 3: date / channel / search filters (combined).
//  Phase 4: CSV upload stub + full pagination.
// ============================================================
(function initBatchPaymentsModal() {
  'use strict';

  // ── DOM refs ──────────────────────────────────────────────
  var overlay       = document.getElementById('bpOverlay');
  var openBtn       = document.getElementById('batchPaymentBtn');
  var closeBtn      = document.getElementById('bpCloseBtn');
  var dateInput     = document.getElementById('batchDate');
  var channelSelect = document.getElementById('batchPaymentChannel');
  var searchEl      = document.getElementById('batchSearch');
  var tableBody     = document.getElementById('bpTableBody');
  var uploadBtn     = document.getElementById('uploadCsvBtn');
  var csvFileInput  = document.getElementById('batchCsvFileInput');
  var rowsSelect    = document.getElementById('batchRowsPerPageSelect');
  var rangeLabel    = document.getElementById('batchPaginationRangeLabel');
  var prevBtn       = document.getElementById('batchPrevPageBtn');
  var nextBtn       = document.getElementById('batchNextPageBtn');

  // Guard — nothing to wire if modal markup is absent.
  if (!overlay || !openBtn || !closeBtn) return;

  // ── Module-level state ────────────────────────────────────

  // Full dataset — populated once per open; rows are prepended on upload.
  var batchPaymentsData = [];

  // The last filtered result — recalculated by applyBatchFilters() and
  // held here so pagination can re-slice it without re-filtering.
  var batchFilteredData = [];

  // Pagination state — mirrors the cm-* / colrep-* pattern in this file.
  var batchPagination = {
    currentPage: 1,
    rowsPerPage: 10    // matches batchRowsPerPageSelect default option
  };

  // Debounce timer for the search input.
  var batchSearchTimer = null;

  // ── Helpers ───────────────────────────────────────────────
  var MONTH_NAMES = ['Jan','Feb','Mar','Apr','May','Jun',
                     'Jul','Aug','Sep','Oct','Nov','Dec'];

  function formatDate(iso) {
    var p = iso.split('-');
    if (p.length !== 3) return iso;
    return MONTH_NAMES[parseInt(p[1], 10) - 1] + ' ' +
           parseInt(p[2], 10) + ', ' + p[0];
  }

  function daysAgo(n) {
    var d = new Date();
    d.setDate(d.getDate() - n);
    return d.toISOString().slice(0, 10);
  }

  function todayISO() {
    return new Date().toISOString().slice(0, 10);
  }

  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  // ── 1. Data loading ───────────────────────────────────────
  // TODO: replace with fetch('/api/Billing/BatchPayments').then(r => r.json())
  // Expected item shape: { id, filename, channel, status, dateUploaded }
  function loadBatchPaymentsData() {
    return Promise.resolve(
      (typeof SAMPLE_BATCH_PAYMENTS !== 'undefined') ? SAMPLE_BATCH_PAYMENTS : []
    );
  }

  // ── 2. CSV upload stub ────────────────────────────────────
  // TODO: replace with a real multipart/form-data POST, e.g.:
  //   var form = new FormData(); form.append('file', file);
  //   return fetch('/api/Billing/BatchPayments/Upload', { method:'POST', body:form })
  //     .then(function (r) { if (!r.ok) throw new Error(r.statusText); return r.json(); });
  function uploadBatchCsv(file) {
    console.log('[uploadBatchCsv] uploading:', file.name);
    return new Promise(function (resolve) { setTimeout(resolve, 900); });
  }

  // Wire the invisible file <input> to the visible upload button.
  // Shows a brief loading state while "uploading", then prepends a new
  // mock row so the freshly uploaded file appears at the top of the list.
  if (uploadBtn && csvFileInput) {

    // Button click → trigger the native file picker
    uploadBtn.addEventListener('click', function () {
      csvFileInput.value = '';   // reset so re-selecting the same file fires 'change'
      csvFileInput.click();
    });

    // File selected → run the upload stub
    csvFileInput.addEventListener('change', function () {
      var file = this.files && this.files[0];
      if (!file) return;

      // Loading state on the button
      var originalHTML = uploadBtn.innerHTML;
      uploadBtn.disabled = true;
      uploadBtn.innerHTML =
        '<svg class="bp-upload-spinner" viewBox="0 0 24 24" fill="none" ' +
        'stroke="currentColor" stroke-width="2.5" width="16" height="16" aria-hidden="true">' +
        '<path d="M12 2a10 10 0 0 1 10 10" stroke-linecap="round"/></svg>';

      uploadBatchCsv(file)
        .then(function () {
          // Prepend a new mock row representing the uploaded file
          var newRow = {
            id:           'TMCWD-RAWLOGS',
            filename:     file.name,
            channel:      channelSelect && channelSelect.value ? channelSelect.value : 'ECPay',
            status:       'Processed',
            dateUploaded: todayISO()
          };
          batchPaymentsData.unshift(newRow);

          // Refresh channel dropdown in case the new row adds a novel channel
          populateChannelDropdown(batchPaymentsData);

          // Re-apply all filters so the new row is visible if it matches
          applyBatchFilters();
        })
        .catch(function (err) {
          console.error('[uploadBatchCsv] error:', err);
        })
        .finally(function () {
          uploadBtn.disabled = false;
          uploadBtn.innerHTML = originalHTML;
        });
    });
  }

  // ── 3. Channel dropdown population ───────────────────────
  function populateChannelDropdown(data) {
    if (!channelSelect) return;
    var seen = {}, channels = [];
    data.forEach(function (item) {
      if (!seen[item.channel]) { seen[item.channel] = true; channels.push(item.channel); }
    });

    var current = channelSelect.value;
    channelSelect.innerHTML = '';
    var allOpt = document.createElement('option');
    allOpt.value = ''; allOpt.textContent = 'All Channels';
    channelSelect.appendChild(allOpt);
    channels.forEach(function (ch) {
      var opt = document.createElement('option');
      opt.value = ch; opt.textContent = ch;
      channelSelect.appendChild(opt);
    });
    channelSelect.value = (current && seen[current]) ? current : '';
  }

  // ── 4. Detail view stub ───────────────────────────────────
  // TODO: wire to real batch detail view, e.g.:
  //   window.location.href = '/Billing/BatchDetail?id=' + encodeURIComponent(id);
  function viewBatchDetails(id, filename) {
    console.log('[viewBatchDetails] id:', id, '| file:', filename);
  }

  // ── 5. Table rendering (accepts a page-slice) ─────────────
  function renderBatchPaymentsTable(data) {
    if (!tableBody) return;
    tableBody.innerHTML = '';

    if (!data || data.length === 0) {
      var emptyRow = document.createElement('tr');
      emptyRow.innerHTML =
        '<td colspan="4" class="bp-empty-row">No batch payments found.</td>';
      tableBody.appendChild(emptyRow);
      return;
    }

    data.forEach(function (item) {
      var row = document.createElement('tr');

      var idCell = document.createElement('td');
      var link   = document.createElement('a');
      link.href      = '#';
      link.className = 'bp-id-link';
      link.setAttribute('aria-label',
        'View details for ' + item.id + ' / ' + item.filename);
      link.innerHTML =
        '<span class="bp-id-code">' + escapeHtml(item.id)       + '</span>' +
        '<span class="bp-id-file">' + escapeHtml(item.filename) + '</span>';
      (function (cid, cfile) {
        link.addEventListener('click', function (e) {
          e.preventDefault();
          viewBatchDetails(cid, cfile);
        });
      }(item.id, item.filename));
      idCell.appendChild(link);

      var channelCell = document.createElement('td');
      channelCell.textContent = item.channel;

      var statusCell = document.createElement('td');
      statusCell.textContent = item.status;

      var dateCell = document.createElement('td');
      dateCell.textContent = formatDate(item.dateUploaded);

      row.appendChild(idCell);
      row.appendChild(channelCell);
      row.appendChild(statusCell);
      row.appendChild(dateCell);
      tableBody.appendChild(row);
    });
  }

  // ── 6. Pagination controls ────────────────────────────────
  // Updates range label and enables / disables arrow buttons.
  // Clamps currentPage when rowsPerPage shrinks the range.
  function updatePaginationControls(total) {
    if (!rangeLabel) return;

    var rpp        = batchPagination.rowsPerPage;
    var totalPages = Math.max(1, Math.ceil(total / rpp));

    // Clamp — prevents currentPage sitting beyond the last page
    if (batchPagination.currentPage > totalPages) {
      batchPagination.currentPage = totalPages;
    }

    var start = total === 0 ? 0 : (batchPagination.currentPage - 1) * rpp + 1;
    var end   = Math.min(batchPagination.currentPage * rpp, total);

    rangeLabel.textContent = total === 0
      ? '0\u20130 of 0'
      : start + '\u2013' + end + ' of ' + total;

    if (prevBtn) prevBtn.disabled = total === 0 || batchPagination.currentPage <= 1;
    if (nextBtn) nextBtn.disabled = total === 0 || batchPagination.currentPage >= totalPages;
  }

  // Returns the correct page-slice from batchFilteredData.
  function getPageSlice() {
    var rpp   = batchPagination.rowsPerPage;
    var start = (batchPagination.currentPage - 1) * rpp;
    return batchFilteredData.slice(start, start + rpp);
  }

  // Central view refresh — single call-site for anything that changes
  // what is visible. Reads batchFilteredData directly.
  function refreshView() {
    renderBatchPaymentsTable(getPageSlice());
    updatePaginationControls(batchFilteredData.length);
  }

  // ── 7. Combined filter function ───────────────────────────
  // Reads current values of all three controls, filters batchPaymentsData,
  // stores result in batchFilteredData, resets to page 1, refreshes view.
  function applyBatchFilters() {
    var dateVal    = dateInput     ? dateInput.value.trim()              : '';
    var channelVal = channelSelect ? channelSelect.value.trim()          : '';
    var searchVal  = searchEl      ? searchEl.value.trim().toLowerCase() : '';

    batchFilteredData = batchPaymentsData.filter(function (item) {
      if (dateVal    && item.dateUploaded !== dateVal)                       return false;
      if (channelVal && item.channel      !== channelVal)                    return false;
      if (searchVal) {
        var hay = (item.id + ' ' + item.filename).toLowerCase();
        if (hay.indexOf(searchVal) === -1)                                   return false;
      }
      return true;
    });

    batchPagination.currentPage = 1;   // reset to page 1 on every filter change
    refreshView();
  }

  // ── 8. Filter event listeners ─────────────────────────────

  if (dateInput)     dateInput.addEventListener('change', applyBatchFilters);
  if (channelSelect) channelSelect.addEventListener('change', applyBatchFilters);

  if (searchEl) {
    searchEl.addEventListener('input', function () {
      clearTimeout(batchSearchTimer);
      batchSearchTimer = setTimeout(applyBatchFilters, 300);
    });
  }

  // ── 9. Pagination event listeners ────────────────────────

  if (rowsSelect) {
    rowsSelect.addEventListener('change', function () {
      batchPagination.rowsPerPage = parseInt(this.value, 10);
      batchPagination.currentPage = 1;
      refreshView();
    });
  }

  if (prevBtn) {
    prevBtn.addEventListener('click', function () {
      if (batchPagination.currentPage > 1) {
        batchPagination.currentPage--;
        refreshView();
      }
    });
  }

  if (nextBtn) {
    nextBtn.addEventListener('click', function () {
      var totalPages = Math.ceil(batchFilteredData.length / batchPagination.rowsPerPage);
      if (batchPagination.currentPage < totalPages) {
        batchPagination.currentPage++;
        refreshView();
      }
    });
  }

  // ── 10. Reset — restores all controls + pagination to defaults ───
  // Called on both open and close so the modal always reopens fresh.
  function resetModal() {
    var today = todayISO();
    if (dateInput)     dateInput.value     = '';   // no default filter — show all rows
    if (channelSelect) channelSelect.value = '';
    if (searchEl)      searchEl.value      = '';
    clearTimeout(batchSearchTimer);

    batchPagination.currentPage = 1;
    batchPagination.rowsPerPage = 10;
    if (rowsSelect) rowsSelect.value = '10';

    // Clear filtered data and reset pagination display
    batchFilteredData = [];
    updatePaginationControls(0);
  }

  // ── Open ─────────────────────────────────────────────────
  function openModal() {
    resetModal();
    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';

    loadBatchPaymentsData()
      .then(function (data) {
        batchPaymentsData = data;
        populateChannelDropdown(batchPaymentsData);
        // applyBatchFilters reads the just-reset controls so the table
        // opens pre-filtered by today's date at page 1.
        applyBatchFilters();
      })
      .catch(function (err) {
        console.error('[BatchPayments] Data load error:', err);
        if (tableBody) {
          tableBody.innerHTML =
            '<tr><td colspan="4" class="bp-empty-row">' +
            'Failed to load data. Please try again.</td></tr>';
        }
      });

    setTimeout(function () { if (searchEl) searchEl.focus(); }, 220);
  }

  // ── Close ────────────────────────────────────────────────
  // Resets everything so the modal reopens fresh next time.
  function closeModal() {
    resetModal();
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
    openBtn.focus();
  }

  // ── Open / close event listeners ─────────────────────────

  openBtn.addEventListener('click', function (e) { e.preventDefault(); openModal(); });
  closeBtn.addEventListener('click', closeModal);
  overlay.addEventListener('click', function (e) { if (e.target === overlay) closeModal(); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

  // ── One-time style injection ──────────────────────────────
  // Injects the upload-button spinner animation without a separate CSS file.
  // Mirrors the colrep-* pattern used by initCollectionReportModal above.
  (function injectBpStyles() {
    if (document.getElementById('bp-dynamic-styles')) return;
    var style = document.createElement('style');
    style.id = 'bp-dynamic-styles';
    style.textContent =
      '@keyframes bpSpin{from{transform:rotate(0deg)}to{transform:rotate(360deg)}}' +
      '.bp-upload-spinner{animation:bpSpin .7s linear infinite;display:block;}';
    document.head.appendChild(style);
  }());

}());


// ============================================================
//  My Collection Modal
//  Phase 1 : open / close wiring.
//  Phase 2 : data loading, table rendering, empty state,
//            pagination range label.
//  Phase 3 : date filter (re-fetch) + debounced search filter.
//  Phase 4 : full pagination, reset-on-close, final style pass.
// ============================================================
(function initMyCollectionModal() {
  'use strict';

  // ── DOM refs ──────────────────────────────────────────────
  var overlay      = document.getElementById('mclOverlay');
  var openBtn      = document.getElementById('myCollectionBtn');
  var closeBtn     = document.getElementById('mclCloseBtn');
  var dateInput    = document.getElementById('collectionListDate');
  var searchInput  = document.getElementById('collectionListSearch');
  var tableBody    = document.getElementById('mclTableBody');
  var rangeLabel   = document.getElementById('collectionListPaginationRangeLabel');
  var rowsSelect   = document.getElementById('collectionListRowsPerPageSelect');
  var prevBtn      = document.getElementById('collectionListPrevPageBtn');
  var nextBtn      = document.getElementById('collectionListNextPageBtn');

  if (!overlay || !openBtn || !closeBtn) return;

  // ── Module-level state ────────────────────────────────────
  // collectionListData     — full dataset for the selected date;
  //                          never mutated by filtering.
  // collectionListFiltered — result of the last applyCollectionListFilters()
  //                          call; pagination always slices this.
  // collectionListPagination — single source of truth for page state.
  var collectionListData     = [];
  var collectionListFiltered = [];
  var collectionListPagination = { currentPage: 1, rowsPerPage: 5 };

  // Debounce timer for the search input.
  var mclSearchTimer = null;

  // ── Helpers ───────────────────────────────────────────────
  function todayISO() {
    return new Date().toISOString().slice(0, 10);
  }

  function fmtCurrency(amount) {
    return '\u20B1' + Number(amount).toLocaleString('en-PH', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  // ── 1. Data loading ───────────────────────────────────────
  // TODO: replace with a real API call, e.g.:
  //   return fetch('/api/Billing/MyCollection?date=' + encodeURIComponent(date))
  //     .then(function (r) {
  //       if (!r.ok) throw new Error(r.statusText);
  //       return r.json();
  //     });
  // Expected item shape: { invoiceNo, account, totalAmount, transactionType }
  function loadCollectionListData(date) {
    var sample = (typeof SAMPLE_COLLECTION_LIST !== 'undefined')
      ? SAMPLE_COLLECTION_LIST
      : [];
    // When a real API is wired the date parameter will be sent as a query filter.
    // For now every date returns the full sample set.
    return Promise.resolve(sample);
  }

  // ── 2. Table rendering ────────────────────────────────────
  // Accepts the page-slice only — caller is responsible for slicing.
  // Empty array → "No records found." spanning all 4 columns.
  function renderCollectionListTable(data) {
    if (!tableBody) return;
    tableBody.innerHTML = '';

    if (!data || data.length === 0) {
      var emptyRow = document.createElement('tr');
      emptyRow.innerHTML =
        '<td colspan="4" class="mcl-empty-row">No records found.</td>';
      tableBody.appendChild(emptyRow);
      return;
    }

    data.forEach(function (item) {
      var row = document.createElement('tr');

      var tdInvoice = document.createElement('td');
      tdInvoice.textContent = item.invoiceNo;

      var tdAccount = document.createElement('td');
      tdAccount.textContent = item.account;

      var tdAmount = document.createElement('td');
      tdAmount.textContent = fmtCurrency(item.totalAmount);

      var tdType = document.createElement('td');
      tdType.textContent = item.transactionType;

      row.appendChild(tdInvoice);
      row.appendChild(tdAccount);
      row.appendChild(tdAmount);
      row.appendChild(tdType);
      tableBody.appendChild(row);
    });
  }

  // ── 3. Page slice helper ──────────────────────────────────
  // Pure function — no side-effects. Returns the sub-array for
  // the current pagination state applied to collectionListFiltered.
  function getMclPageSlice() {
    var rpp   = collectionListPagination.rowsPerPage;
    var start = (collectionListPagination.currentPage - 1) * rpp;
    return collectionListFiltered.slice(start, start + rpp);
  }

  // ── 4. Pagination controls ────────────────────────────────
  // Updates range label and enables / disables arrow buttons.
  // Clamps currentPage when rowsPerPage shrinks the valid range.
  function updateMclPaginationControls() {
    var total      = collectionListFiltered.length;
    var rpp        = collectionListPagination.rowsPerPage;
    var totalPages = Math.max(1, Math.ceil(total / rpp));

    // Clamp — prevents currentPage sitting beyond the last page.
    if (collectionListPagination.currentPage > totalPages) {
      collectionListPagination.currentPage = totalPages;
    }

    var start = total === 0
      ? 0
      : (collectionListPagination.currentPage - 1) * rpp + 1;
    var end = Math.min(collectionListPagination.currentPage * rpp, total);

    if (rangeLabel) {
      rangeLabel.textContent = total === 0
        ? '1\u20130 of 0'
        : start + '\u2013' + end + ' of ' + total;
    }

    if (prevBtn) prevBtn.disabled =
      total === 0 || collectionListPagination.currentPage <= 1;
    if (nextBtn) nextBtn.disabled =
      total === 0 || collectionListPagination.currentPage >= totalPages;
  }

  // ── 5. Central view refresh ───────────────────────────────
  // Single call-site — slices collectionListFiltered, renders the
  // page, and syncs all pagination controls.
  function refreshMclView() {
    renderCollectionListTable(getMclPageSlice());
    updateMclPaginationControls();
  }

  // ── 6. Combined filter function ───────────────────────────
  // Reads the current search term, filters collectionListData into
  // collectionListFiltered, resets to page 1, then refreshes the view.
  // Never re-fetches — only operates on the in-memory date dataset.
  function applyCollectionListFilters() {
    var term = searchInput ? searchInput.value.trim().toLowerCase() : '';

    collectionListFiltered = term
      ? collectionListData.filter(function (item) {
          return item.invoiceNo.toLowerCase().indexOf(term) !== -1 ||
                 item.account.toLowerCase().indexOf(term)   !== -1;
        })
      : collectionListData.slice();

    collectionListPagination.currentPage = 1;
    refreshMclView();
  }

  // ── 7. Load for a given date, then apply any active filter ─
  // Owns the full "fetch → store → clear search → filter → render"
  // pipeline. Both openModal() and the date-change handler use this.
  function loadAndRender(date) {
    loadCollectionListData(date)
      .then(function (data) {
        collectionListData = data;
        collectionListPagination.currentPage = 1;
        if (searchInput) searchInput.value = '';
        applyCollectionListFilters();
      })
      .catch(function (err) {
        console.error('[MyCollection] Data load error:', err);
        collectionListData     = [];
        collectionListFiltered = [];
        collectionListPagination.currentPage = 1;
        renderCollectionListTable([]);
        updateMclPaginationControls();
      });
  }

  // ── 8. Reset — restores all controls + state to defaults ─
  // Called on close so the modal always reopens fresh next time.
  function resetMclModal() {
    if (dateInput)   dateInput.value   = todayISO();
    if (searchInput) searchInput.value = '';
    clearTimeout(mclSearchTimer);

    collectionListData     = [];
    collectionListFiltered = [];
    collectionListPagination.currentPage = 1;
    collectionListPagination.rowsPerPage = 5;
    if (rowsSelect) rowsSelect.value = '5';

    // Reset pagination display to blank/disabled state
    if (rangeLabel) rangeLabel.textContent = '1\u20130 of 0';
    if (prevBtn) prevBtn.disabled = true;
    if (nextBtn) nextBtn.disabled = true;
  }

  // ── Open ─────────────────────────────────────────────────
  function openModal() {
    // Always seed today before loadAndRender reads dateInput.value
    if (dateInput) dateInput.value = todayISO();

    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';
    if (closeBtn) closeBtn.focus();

    loadAndRender(dateInput ? dateInput.value : todayISO());
  }

  // ── Close ────────────────────────────────────────────────
  function closeModal() {
    resetMclModal();
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
    openBtn.focus();
  }

  // ── Event listeners ──────────────────────────────────────

  openBtn.addEventListener('click', function (e) {
    e.preventDefault();
    openModal();
  });

  closeBtn.addEventListener('click', closeModal);

  // Dim overlay click → close
  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });

  // Escape key → close (only when this modal is the visible one)
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

  // Date change → re-fetch for the new date, clear search, re-render
  if (dateInput) {
    dateInput.addEventListener('change', function () {
      loadAndRender(this.value);
    });
  }

  // Search input → debounced filter (no re-fetch)
  if (searchInput) {
    searchInput.addEventListener('input', function () {
      clearTimeout(mclSearchTimer);
      mclSearchTimer = setTimeout(applyCollectionListFilters, 300);
    });
  }

  // Rows-per-page select → update rowsPerPage, reset to page 1, refresh
  if (rowsSelect) {
    rowsSelect.addEventListener('change', function () {
      collectionListPagination.rowsPerPage = parseInt(this.value, 10);
      collectionListPagination.currentPage = 1;
      refreshMclView();
    });
  }

  // Previous page button
  if (prevBtn) {
    prevBtn.addEventListener('click', function () {
      if (collectionListPagination.currentPage > 1) {
        collectionListPagination.currentPage--;
        refreshMclView();
      }
    });
  }

  // Next page button
  if (nextBtn) {
    nextBtn.addEventListener('click', function () {
      var totalPages = Math.ceil(
        collectionListFiltered.length / collectionListPagination.rowsPerPage
      );
      if (collectionListPagination.currentPage < totalPages) {
        collectionListPagination.currentPage++;
        refreshMclView();
      }
    });
  }

}());


// ============================================================
//  Invoice Search Modal
//  Phase 1: open / close wiring.
//  Phase 2: mock data in collection_sample.js.
//  Phase 3: search logic and results rendering.
//  Phase 4: final cleanup, guards, consistent naming.
// ============================================================
(function initInvoiceSearchModal() {
  'use strict';

  // ── DOM refs ──────────────────────────────────────────────
  var overlay       = document.getElementById('isOverlay');
  var openBtn       = document.getElementById('invoiceSearchBtn');
  var closeBtn      = document.getElementById('isCloseBtn');
  var searchInput   = document.getElementById('invoiceSearchInput');
  var searchBtn     = document.getElementById('invoiceSearchBtn2');
  var resultsArea   = document.getElementById('invoiceSearchResults');
  var floatingLabel = document.getElementById('isFloatingLabel');

  // Guard — nothing to wire if modal markup is absent.
  if (!overlay || !openBtn || !closeBtn) return;

  // ── Data source ──────────────────────────────────────────
  // Reference sample data from collection_sample.js
  // TODO: replace with fetch('/api/Billing/InvoiceSearch?query=<term>')
  //       .then(function (r) { if (!r.ok) throw new Error(r.statusText); return r.json(); })
  //       when backend endpoint is ready.
  var invoiceData = (typeof SAMPLE_INVOICE_SEARCH !== 'undefined') ? SAMPLE_INVOICE_SEARCH : [];

  // ── In-flight guard ──────────────────────────────────────
  // Prevents double-triggering while a search is active.
  var searchInFlight = false;

  // ── Helpers ──────────────────────────────────────────────
  function fmtCurrency(amount) {
    return '\u20B1' + Number(amount).toLocaleString('en-PH', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  function formatDate(iso) {
    var parts = iso.split('-');
    if (parts.length !== 3) return iso;
    var months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    return months[parseInt(parts[1], 10) - 1] + ' ' + parseInt(parts[2], 10) + ', ' + parts[0];
  }

  // ── Search function ──────────────────────────────────────
  // Filters invoiceData by partial case-insensitive match on invoiceNo.
  // TODO: replace the filter logic below with:
  //   return fetch('/api/Billing/InvoiceSearch?query=' + encodeURIComponent(query))
  //     .then(function (r) { if (!r.ok) throw new Error(r.statusText); return r.json(); });
  function searchInvoices(query) {
    var term = query.trim().toLowerCase();
    
    // Empty query → return empty array (don't show full dataset)
    if (!term) return [];
    
    // Filter by partial case-insensitive match on invoiceNo
    return invoiceData.filter(function (invoice) {
      return invoice.invoiceNo.toLowerCase().indexOf(term) !== -1;
    });
  }

  // ── Results rendering ────────────────────────────────────
  function renderInvoiceSearchResults(results) {
    if (!resultsArea) return;
    
    resultsArea.innerHTML = '';
    
    // Empty state
    if (!results || results.length === 0) {
      var emptyMsg = document.createElement('div');
      emptyMsg.className = 'is-empty-state';
      emptyMsg.textContent = 'No invoices found';
      resultsArea.appendChild(emptyMsg);
      return;
    }
    
    // Build table
    var table = document.createElement('table');
    table.className = 'is-results-table';
    
    // Header
    var thead = document.createElement('thead');
    thead.innerHTML = '<tr>' +
      '<th>Invoice No.</th>' +
      '<th>Account</th>' +
      '<th style="text-align:right;">Total Amount</th>' +
      '<th>Transaction Type</th>' +
      '<th>Date Issued</th>' +
      '<th>Status</th>' +
    '</tr>';
    table.appendChild(thead);
    
    // Body
    var tbody = document.createElement('tbody');
    results.forEach(function (invoice) {
      var row = document.createElement('tr');
      
      var tdInvoice = document.createElement('td');
      tdInvoice.className = 'is-cell-invoice';
      tdInvoice.textContent = invoice.invoiceNo;
      
      var tdAccount = document.createElement('td');
      tdAccount.textContent = invoice.account;
      
      var tdAmount = document.createElement('td');
      tdAmount.className = 'is-cell-amount';
      tdAmount.textContent = fmtCurrency(invoice.totalAmount);
      
      var tdType = document.createElement('td');
      tdType.textContent = invoice.transactionType;
      
      var tdDate = document.createElement('td');
      tdDate.textContent = formatDate(invoice.dateIssued);
      
      var tdStatus = document.createElement('td');
      tdStatus.className = 'is-cell-status';
      var statusSpan = document.createElement('span');
      statusSpan.className = invoice.status === 'Paid' ? 'is-status-paid' : 'is-status-unpaid';
      statusSpan.textContent = invoice.status;
      tdStatus.appendChild(statusSpan);
      
      row.appendChild(tdInvoice);
      row.appendChild(tdAccount);
      row.appendChild(tdAmount);
      row.appendChild(tdType);
      row.appendChild(tdDate);
      row.appendChild(tdStatus);
      tbody.appendChild(row);
    });
    table.appendChild(tbody);
    
    resultsArea.appendChild(table);
  }

  // ── Search trigger ───────────────────────────────────────
  // Structured as async so it's ready for a future API call.
  // The in-flight guard prevents double-triggering.
  function performSearch() {
    if (!searchInput || !searchBtn) return;
    if (searchInFlight) return;   // guard against double-trigger
    
    var query = searchInput.value;
    
    // Mark search as in-flight
    searchInFlight = true;
    
    // Loading state
    var originalHTML = searchBtn.innerHTML;
    searchBtn.disabled = true;
    searchBtn.innerHTML = '<svg class="is-spinner" viewBox="0 0 24 24" fill="none" ' +
      'stroke="currentColor" stroke-width="2.5" width="15" height="15" aria-hidden="true">' +
      '<path d="M12 2a10 10 0 0 1 10 10" stroke-linecap="round"/></svg> Searching...';
    
    // Wrap in a short timeout so the spinner is visible even for instant results
    setTimeout(function () {
      try {
        // TODO: replace with await fetch(...).then(r => r.json()) when backend is ready.
        var results = searchInvoices(query);
        renderInvoiceSearchResults(results);
      } catch (err) {
        console.error('[Invoice Search] error:', err);
        renderInvoiceSearchResults([]);
      } finally {
        searchBtn.disabled = false;
        searchBtn.innerHTML = originalHTML;
        searchInFlight = false;
      }
    }, 0);
  }

  // ── Open ─────────────────────────────────────────────────
  function openModal() {
    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';

    // Clear previous search input and results for a fresh start
    if (searchInput) searchInput.value = '';
    if (resultsArea) resultsArea.innerHTML = '';
    // Restore label in case it was hidden from a previous session
    if (floatingLabel) floatingLabel.classList.remove('is-label-hidden');

    // Focus the search input after the animation settles
    setTimeout(function () {
      if (searchInput) searchInput.focus();
    }, 200);
  }

  // ── Close ────────────────────────────────────────────────
  function closeModal() {
    overlay.style.display        = 'none';
    document.body.style.overflow = '';

    // Clear search input and results so the modal reopens fresh next time
    if (searchInput) searchInput.value = '';
    if (resultsArea) resultsArea.innerHTML = '';
    // Restore label visibility
    if (floatingLabel) floatingLabel.classList.remove('is-label-hidden');

    openBtn.focus();
  }

  // ── Event listeners ──────────────────────────────────────

  // Label hide/show — disappears on focus, reappears when blurred and empty
  if (searchInput && floatingLabel) {
    searchInput.addEventListener('focus', function () {
      floatingLabel.classList.add('is-label-hidden');
    });
    searchInput.addEventListener('blur', function () {
      if (!this.value) floatingLabel.classList.remove('is-label-hidden');
    });
  }

  // Nav bar button → open
  openBtn.addEventListener('click', function (e) {
    e.preventDefault();
    openModal();
  });

  // X button → close
  closeBtn.addEventListener('click', closeModal);

  // Dim overlay click (outside the card) → close
  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });

  // Escape key → close (only when this modal is visible)
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

  // Search button → perform search
  if (searchBtn) {
    searchBtn.addEventListener('click', performSearch);
  }

  // Enter key in search input → perform search
  // Live search: also fires on every keystroke (input event)
  if (searchInput) {
    searchInput.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') {
        e.preventDefault();
        performSearch();
      }
    });

    // Live / as-you-type search — debounced so it doesn't fire on every keystroke
    var liveSearchTimer = null;
    searchInput.addEventListener('input', function () {
      clearTimeout(liveSearchTimer);
      var query = this.value;

      // If input is cleared, clear the results area instantly
      if (!query.trim()) {
        if (resultsArea) resultsArea.innerHTML = '';
        return;
      }

      // Short debounce keeps the UI snappy without hammering future API calls
      liveSearchTimer = setTimeout(function () {
        performSearch();
      }, 250);
    });
  }

}());


// ============================================================
//  Re-Print Invoice Modal  (ri-*)
//  Phase 1 : open / close wiring.
//  Phase 3 : input sanitisation, lookup, inline validation,
//            async-ready handlePrintInvoice().
//  Phase 4 : printInvoiceDocument() stub, loading state on
//            printInvoiceBtn, success toast, full reset on close.
//
//  Data source : reprintInvoiceSampleData (collection_sample.js)
//  — this file never redefines that array; it only reads it.
// ============================================================
(function initReprintInvoiceModal() {
  'use strict';

  // ── DOM refs ──────────────────────────────────────────────
  var overlay      = document.getElementById('riOverlay');
  var openBtnNav   = document.getElementById('reprintInvoiceBtn');
  var closeBtn     = document.getElementById('riCloseBtn');
  var invoiceInput = document.getElementById('reprintInvoiceNumber');
  var printBtn     = document.getElementById('printInvoiceBtn');

  // Guard \u2014 nothing to wire if modal markup is absent.
  if (!overlay || !openBtnNav || !closeBtn) return;

  // ── In-flight guard ───────────────────────────────────────
  // Mirrors the _exportInFlight pattern used by initCollectionReportModal.
  // Prevents double-triggering while a print Promise is pending.
  var _printInFlight = false;

  // ── Inline feedback helpers ───────────────────────────────
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

  // ── Live search suggestions ───────────────────────────────
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

  // ── Input sanitisation + live search ──────────────────────
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

  // ── findInvoiceByNumber ───────────────────────────────────
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

  // ── printInvoiceDocument ──────────────────────────────────
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

  // ── Button loading / restore ──────────────────────────────
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

  // ── Success toast ─────────────────────────────────────────
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

  // ── handlePrintInvoice ────────────────────────────────────
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

    // ── Loading state ──
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

  // ── Open ─────────────────────────────────────────────────
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

  // ── Close ────────────────────────────────────────────────
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

  // ── Event listeners ──────────────────────────────────────

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

  // ── One-time style injection ──────────────────────────────
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


// ============================================================
//  Other Charges Info Popover
//  Clicking the ⓘ icon next to "Other Charges Balance" opens a
//  small card listing individual charges for the loaded account.
// ============================================================
(function initOtherChargesPopover() {
  'use strict';

  var infoBtn    = document.getElementById('ocInfoBtn');
  var popover    = document.getElementById('ocPopover');
  var closeBtn   = document.getElementById('ocPopoverClose');
  var bodyEl     = document.getElementById('ocPopoverBody');

  if (!infoBtn || !popover || !bodyEl) return;

  // ── Current account reference ─────────────────────────────
  // loadAccountData() in the DOMContentLoaded block sets this so
  // the popover always reflects whatever account is currently loaded.
  // Exposed on window so the inner DOMContentLoaded closure can write it.
  window._ocCurrentAccount = null;

  // ── Formatting helper ─────────────────────────────────────
  function fmtPHP(n) {
    return '\u20B1' + Number(n).toLocaleString('en-PH', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  // ── Populate popover body ─────────────────────────────────
  // If the account object has an `otherCharges` array, render each
  // item as a row.  Otherwise fall back to the single balance figure
  // shown in the field.
  function populatePopover() {
    var acc = window._ocCurrentAccount;
    bodyEl.innerHTML = '';

    // No account loaded yet
    if (!acc) {
      bodyEl.innerHTML = '<p class="oc-empty">No account loaded.</p>';
      popover.querySelector('.oc-total-row') && popover.querySelector('.oc-total-row').remove();
      return;
    }

    var charges = Array.isArray(acc.otherCharges) ? acc.otherCharges : [];
    var balance = acc.otherChargesBalance || 0;

    if (charges.length === 0 && balance === 0) {
      bodyEl.innerHTML = '<p class="oc-empty">No other charges on record.</p>';
      return;
    }

    // If a detailed breakdown array exists, render it
    if (charges.length > 0) {
      var total = 0;
      var html  = charges.map(function (c) {
        total += c.amount || 0;
        return '<div class="oc-charge-row">' +
          '<span class="oc-charge-desc">'   + c.description + '</span>' +
          '<span class="oc-charge-amount">' + fmtPHP(c.amount) + '</span>' +
        '</div>';
      }).join('');
      bodyEl.innerHTML = html;

      // Total footer row — appended outside bodyEl so it gets the
      // distinct background treatment from .oc-total-row
      var existing = popover.querySelector('.oc-total-row');
      if (!existing) {
        var totalRow = document.createElement('div');
        totalRow.className = 'oc-total-row';
        popover.appendChild(totalRow);
      }
      var tr = popover.querySelector('.oc-total-row');
      tr.innerHTML =
        '<span>Total</span>' +
        '<span class="oc-charge-amount">' + fmtPHP(total) + '</span>';

    } else {
      // No breakdown array — show the single balance as one line
      bodyEl.innerHTML =
        '<div class="oc-charge-row">' +
          '<span class="oc-charge-desc">Other Charges</span>' +
          '<span class="oc-charge-amount">' + fmtPHP(balance) + '</span>' +
        '</div>';

      // Remove total row if it was left from a previous account
      var existingTr = popover.querySelector('.oc-total-row');
      if (existingTr) existingTr.remove();
    }
  }

  // ── Open / close ──────────────────────────────────────────
  function openPopover() {
    populatePopover();
    popover.style.display = 'block';
  }

  function closePopover() {
    popover.style.display = 'none';
  }

  // ── Event listeners ───────────────────────────────────────

  // ⓘ button toggles the popover
  infoBtn.addEventListener('click', function (e) {
    e.stopPropagation();
    if (popover.style.display === 'none') {
      openPopover();
    } else {
      closePopover();
    }
  });

  // × button inside popover
  closeBtn.addEventListener('click', function (e) {
    e.stopPropagation();
    closePopover();
  });

  // Click anywhere outside → close
  document.addEventListener('click', function (e) {
    if (!e.target.closest('#ocInfoBtn') &&
        !e.target.closest('#ocPopover')) {
      closePopover();
    }
  });

  // Escape key → close
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') closePopover();
  });

}());
