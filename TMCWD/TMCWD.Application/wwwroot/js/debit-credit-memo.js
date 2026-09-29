// Debit/Credit Memo form backed by the BillingController API.
const dcmTypes = [
    'BOUNCED CHECK', 'CANCELLED OFFICIAL RECEIPT', 'DEFECTIVE SC AND WATER METER',
    'DEFECTIVE SERVICE CONNECTION', 'DEFECTIVE WATER METER', 'DEPOSIT APPLICATION',
    'DISCOUNT', 'DOUBLE ENTRY OF ACCOUNT', 'ERRONEOUS READING', 'ERROR ON PREVIOUS BALANCE',
    'INVALID ACCOUNT PAYMENT', 'INVALID CHARGES', 'LATE PENALTY', 'OTHERS', 'OVER PAYMENT REFUND',
    'RECLASSIFICATION OF CATEGORY', 'SPECIAL PRIVILEGE ACCOUNT', 'TRANSFER OF PAYMENT',
    'WAIVE PENALTY', 'WITHOLDING TAX'
];

function searchDCMTypes(searchTerm) {
    const term = searchTerm.trim().toLowerCase();
    return dcmTypes.filter(type => !term || type.toLowerCase().includes(term));
}

function escapeDCMHtml(value) {
    return String(value ?? '').replace(/[&<>"']/g, function(character) {
        return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character];
    });
}

function formatCurrency(amount) {
    return new Intl.NumberFormat('en-PH', {
        style: 'currency',
        currency: 'PHP',
        minimumFractionDigits: 2
    }).format(amount);
}

document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('dcmForm');
    const resetBtn = document.getElementById('dcmResetBtn');
    const submitBtn = document.getElementById('dcmSubmitBtn');
    const accountNumberInput = document.getElementById('dcmAccountNumber');
    const accountNameInput = document.getElementById('dcmAccountName');
    const dcmTypeInput = document.getElementById('dcmType');
    const explanationInput = document.getElementById('dcmExplanation');
    const remarksInput = document.getElementById('dcmRemarks');

    // Set today's date
    const today = new Date().toISOString().split('T')[0];
    document.getElementById('dcmDate').value = today;

    // Auto-populate remarks based on explanation
    explanationInput.addEventListener('input', function() {
        const explanationText = this.value.trim();
        if (explanationText) {
            remarksInput.value = `AMOUNTING TO DUE TO ${explanationText.toUpperCase()}`;
        } else {
            remarksInput.value = '';
        }
    });

    // Account number lookup with dropdown
    let searchTimeout;
    let selectedIndex = -1;
    let searchResults = [];
    const searchDropdown = document.getElementById('dcmSearchDropdown');

    // Account search functionality  
    accountNumberInput.addEventListener('input', function() {
        clearTimeout(searchTimeout);
        const searchTerm = this.value.trim();
        selectedIndex = -1;
        
        if (searchTerm.length >= 1) {
            searchTimeout = setTimeout(() => {
                showSearchResults(searchTerm);
            }, 300);
        } else {
            hideSearchDropdown();
            clearAccountName();
        }
    });

    // Handle keyboard navigation
    accountNumberInput.addEventListener('keydown', function(e) {
        const items = searchDropdown.querySelectorAll('.dcm-search-item');
        
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            selectedIndex = Math.min(selectedIndex + 1, items.length - 1);
            updateHighlight(items);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            selectedIndex = Math.max(selectedIndex - 1, -1);
            updateHighlight(items);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            if (selectedIndex >= 0 && items[selectedIndex]) {
                selectAccount(searchResults[selectedIndex]);
            }
        } else if (e.key === 'Escape') {
            hideSearchDropdown();
        }
    });

    // Hide dropdowns when clicking outside
    document.addEventListener('click', function(e) {
        if (!e.target.closest('.dcm-search-container')) {
            hideSearchDropdown();
        }
    });

    async function showSearchResults(searchTerm) {
        try {
            const response = await fetch(`/Billing/SearchAccounts?q=${encodeURIComponent(searchTerm)}`);
            if (!response.ok) throw new Error('Account lookup failed.');
            const results = await response.json();
            searchResults = Array.isArray(results) ? results : [];
        } catch (error) {
            searchResults = [];
            searchDropdown.innerHTML = '<div class="dcm-search-no-results">Unable to search accounts</div>';
            searchDropdown.classList.add('show');
            return;
        }
        
        if (searchResults.length > 0) {
            const html = searchResults.map((account, index) => `
                <div class="dcm-search-item" data-index="${index}">
                    <div class="dcm-search-item-main">${escapeDCMHtml(account.accountNumber)} - ${escapeDCMHtml(account.name)}</div>
                    <div class="dcm-search-item-sub">${escapeDCMHtml(account.address || '')}</div>
                </div>
            `).join('');
            
            searchDropdown.innerHTML = html;
            searchDropdown.classList.add('show');
            
            // Add click listeners
            searchDropdown.querySelectorAll('.dcm-search-item').forEach((item, index) => {
                item.addEventListener('click', () => selectAccount(searchResults[index]));
            });
        } else {
            searchDropdown.innerHTML = '<div class="dcm-search-no-results">No accounts found</div>';
            searchDropdown.classList.add('show');
        }
    }

    function updateHighlight(items) {
        items.forEach((item, index) => {
            item.classList.toggle('highlighted', index === selectedIndex);
        });
    }

    function selectAccount(account) {
        accountNumberInput.value = account.accountNumber;
        accountNameInput.value = account.name;
        
        // Store additional account info
        accountNumberInput.dataset.fullAccountNumber = account.accountNumber;
        accountNameInput.dataset.address = account.address;
        accountNameInput.dataset.meterNumber = account.meterNumber;
        
        hideSearchDropdown();
    }

    function hideSearchDropdown() {
        searchDropdown.classList.remove('show');
        selectedIndex = -1;
    }

    function clearAccountName() {
        accountNameInput.value = '';
        accountNameInput.disabled = false;
        // Clear stored data
        delete accountNumberInput.dataset.fullAccountNumber;
        delete accountNameInput.dataset.address;
        delete accountNameInput.dataset.meterNumber;
        delete accountNameInput.dataset.connectionType;
        hideSearchDropdown();
    }

    function clearDCMType() {
        dcmTypeInput.value = '';
    }

    // Reset form
    resetBtn.addEventListener('click', function() {
        form.reset();
        document.getElementById('dcmDate').value = today;
        clearAccountName();
        clearDCMType();
        remarksInput.value = '';
    });

    form.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        // Basic validation
        if (!accountNumberInput.value.trim()) {
            toastSystem.error('Please enter an account number');
            accountNumberInput.focus();
            return;
        }
        
        if (!dcmTypeInput.value) {
            toastSystem.error('Please select a DCM type');
            dcmTypeInput.focus();
            return;
        }
        
        if (!document.getElementById('dcmAmount').value || parseFloat(document.getElementById('dcmAmount').value) <= 0) {
            toastSystem.error('Please enter a valid amount');
            document.getElementById('dcmAmount').focus();
            return;
        }
        
        if (!explanationInput.value.trim()) {
            toastSystem.error('Please enter an explanation');
            explanationInput.focus();
            return;
        }

        if (accountNameInput.value === 'Account not found' || accountNameInput.value === 'Searching...' || !accountNameInput.value.trim()) {
            toastSystem.error('Please select a valid account from the dropdown');
            accountNumberInput.focus();
            return;
        }

        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        const memoDate = document.getElementById('dcmDate').value;
        const billMonthBasis = document.getElementById('dcmBillMonthBasis').value;
        const dcmData = {
            accountNumber: accountNumberInput.dataset.fullAccountNumber || accountNumberInput.value.trim(),
            dcmType: dcmTypeInput.value,
            memoType: document.querySelector('input[name="dcmMemoType"]:checked').value,
            amount: parseFloat(document.getElementById('dcmAmount').value),
            dcmDate: memoDate || today,
            billMonthBasis: billMonthBasis || null,
            explanation: explanationInput.value.trim(),
            remarks: remarksInput.value.trim()
        };

        submitBtn.disabled = true;
        submitBtn.textContent = 'Creating...';
        try {
            const response = await fetch('/Billing/SaveDebitCreditMemo', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token ? token.value : ''
                },
                body: JSON.stringify(dcmData)
            });
            if (!response.ok) throw new Error(await response.text() || 'The memo could not be saved.');
            const saved = await response.json();
            toastSystem.success(`DCM ${saved.id} created successfully`, `Amount: ${formatCurrency(saved.amount)}\nType: ${saved.memoType.toUpperCase()} MEMO`);
            form.reset();
            document.getElementById('dcmDate').value = today;
            clearAccountName();
            clearDCMType();
            remarksInput.value = '';
        } catch (error) {
            toastSystem.error('Error creating DCM', error.message);
        } finally {
            submitBtn.disabled = false;
            submitBtn.textContent = 'Submit';
        }
    });
});
