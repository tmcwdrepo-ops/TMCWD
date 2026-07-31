// Simple Debit/Credit Memo Form
// Fixed: DCM Type input readonly issue - multiple safeguards ensure input remains editable
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

    // DCM Type lookup with dropdown
    let dcmTypeTimeout;
    let dcmSelectedIndex = -1;
    let dcmTypeResults = [];
    const dcmTypeDropdown = document.getElementById('dcmTypeDropdown');

    // Ensure DCM Type input is not readonly and is fully functional
    dcmTypeInput.removeAttribute('readonly');
    dcmTypeInput.removeAttribute('disabled');
    dcmTypeInput.style.pointerEvents = 'auto';
    dcmTypeInput.style.cursor = 'text';

    // DCM Type search functionality
    dcmTypeInput.addEventListener('click', function() {
        // Ensure input is focusable and editable
        this.removeAttribute('readonly');
        this.focus();
        if (!this.value) {
            showDCMTypeResults('');
        }
    });

    dcmTypeInput.addEventListener('focus', function() {
        // Ensure input is not readonly when focused
        this.removeAttribute('readonly');
        if (!this.value) {
            showDCMTypeResults('');
        }
    });

    dcmTypeInput.addEventListener('input', function() {
        // Ensure input continues to work
        this.removeAttribute('readonly');
        clearTimeout(dcmTypeTimeout);
        const searchTerm = this.value.trim();
        dcmSelectedIndex = -1;
        
        dcmTypeTimeout = setTimeout(() => {
            showDCMTypeResults(searchTerm);
        }, 200);
    });

    // Add keydown event to catch any typing attempts
    dcmTypeInput.addEventListener('keydown', function(e) {
        // Ensure input is editable before processing keydown
        this.removeAttribute('readonly');
        this.removeAttribute('disabled');
        
        const items = dcmTypeDropdown.querySelectorAll('.dcm-search-item');
        
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            dcmSelectedIndex = Math.min(dcmSelectedIndex + 1, items.length - 1);
            updateDCMTypeHighlight(items);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            dcmSelectedIndex = Math.max(dcmSelectedIndex - 1, -1);
            updateDCMTypeHighlight(items);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            if (dcmSelectedIndex >= 0 && items[dcmSelectedIndex]) {
                selectDCMType(dcmTypeResults[dcmSelectedIndex]);
            }
        } else if (e.key === 'Escape') {
            hideDCMTypeDropdown();
        }
    });

    // Add keypress event to ensure typing is always allowed
    dcmTypeInput.addEventListener('keypress', function(e) {
        // Remove any readonly/disabled attributes immediately
        this.removeAttribute('readonly');
        this.removeAttribute('disabled');
    });

    function showDCMTypeResults(searchTerm) {
        dcmTypeResults = searchDCMTypes(searchTerm);
        
        if (dcmTypeResults.length > 0) {
            const html = dcmTypeResults.map((type, index) => `
                <div class="dcm-search-item" data-index="${index}">
                    <div class="dcm-search-item-main">${type}</div>
                </div>
            `).join('');
            
            dcmTypeDropdown.innerHTML = html;
            dcmTypeDropdown.classList.add('show');
            
            // Add click listeners
            dcmTypeDropdown.querySelectorAll('.dcm-search-item').forEach((item, index) => {
                item.addEventListener('click', () => selectDCMType(dcmTypeResults[index]));
            });
        } else {
            dcmTypeDropdown.innerHTML = '<div class="dcm-search-no-results">No DCM types found</div>';
            dcmTypeDropdown.classList.add('show');
        }
    }

    function updateDCMTypeHighlight(items) {
        items.forEach((item, index) => {
            item.classList.toggle('highlighted', index === dcmSelectedIndex);
        });
    }

    function selectDCMType(dcmType) {
        dcmTypeInput.value = dcmType;
        dcmTypeInput.dataset.selectedType = dcmType;
        // Ensure input remains editable after selection
        dcmTypeInput.removeAttribute('readonly');
        hideDCMTypeDropdown();
    }

    function hideDCMTypeDropdown() {
        dcmTypeDropdown.classList.remove('show');
        dcmSelectedIndex = -1;
    }

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
            hideDCMTypeDropdown();
        }
    });

    function showSearchResults(searchTerm) {
        searchResults = searchAccounts(searchTerm);
        
        if (searchResults.length > 0) {
            const html = searchResults.map((account, index) => `
                <div class="dcm-search-item" data-index="${index}">
                    <div class="dcm-search-item-main">${account.accountNumber} - ${account.accountName}</div>
                    <div class="dcm-search-item-sub">${account.address} | ${account.connectionType}</div>
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
        accountNameInput.value = account.accountName;
        
        // Store additional account info
        accountNumberInput.dataset.fullAccountNumber = account.accountNumber;
        accountNameInput.dataset.address = account.address;
        accountNameInput.dataset.meterNumber = account.meterNumber;
        accountNameInput.dataset.connectionType = account.connectionType;
        
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
        delete dcmTypeInput.dataset.selectedType;
        // Ensure input is editable
        dcmTypeInput.removeAttribute('readonly');
        dcmTypeInput.removeAttribute('disabled');
        hideDCMTypeDropdown();
    }

    // Function to ensure DCM Type input is always editable
    function ensureDCMTypeEditable() {
        dcmTypeInput.removeAttribute('readonly');
        dcmTypeInput.removeAttribute('disabled');
        dcmTypeInput.style.pointerEvents = 'auto';
        dcmTypeInput.style.cursor = 'text';
        dcmTypeInput.classList.remove('dcm-input-readonly');
    }

    // Call on page load to ensure DCM Type is editable
    ensureDCMTypeEditable();
    
    // Debug: Log DCM Type input state
    console.log('DCM Type input initialized:', {
        readonly: dcmTypeInput.hasAttribute('readonly'),
        disabled: dcmTypeInput.hasAttribute('disabled'),
        value: dcmTypeInput.value,
        classList: dcmTypeInput.classList.toString()
    });

    // Periodic check to ensure DCM Type remains editable (debugging)
    setInterval(() => {
        if (dcmTypeInput.hasAttribute('readonly') || dcmTypeInput.hasAttribute('disabled')) {
            console.warn('DCM Type input became readonly/disabled, fixing...');
            ensureDCMTypeEditable();
        }
    }, 1000);

    // Reset form
    resetBtn.addEventListener('click', function() {
        form.reset();
        document.getElementById('dcmDate').value = today;
        clearAccountName();
        clearDCMType();
        remarksInput.value = '';
        // Ensure DCM Type remains editable after reset
        ensureDCMTypeEditable();
    });

    // Form submission with sample data integration
    form.addEventListener('submit', function(e) {
        e.preventDefault();
        
        // Basic validation
        if (!accountNumberInput.value.trim()) {
            toastSystem.error('Please enter an account number');
            accountNumberInput.focus();
            return;
        }
        
        if (!dcmTypeInput.value.trim() || !dcmTypeInput.dataset.selectedType) {
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

        // Prepare DCM data
        const dcmData = {
            accountNumber: accountNumberInput.dataset.fullAccountNumber || accountNumberInput.value,
            accountName: accountNameInput.value,
            dcmType: dcmTypeInput.dataset.selectedType || dcmTypeInput.value,
            memoType: document.querySelector('input[name="dcmMemoType"]:checked').value,
            amount: parseFloat(document.getElementById('dcmAmount').value),
            dcmDate: document.getElementById('dcmDate').value,
            billMonthBasis: document.getElementById('dcmBillMonthBasis').value,
            explanation: explanationInput.value.trim(),
            remarks: remarksInput.value.trim()
        };

        // Disable submit button
        submitBtn.disabled = true;
        submitBtn.textContent = 'Creating...';

        // Simulate server submission
        setTimeout(() => {
            try {
                // Add to sample data
                const newRecord = addDCMRecord(dcmData);
                
                // Show success notification
                const successMessage = `DCM created successfully for ${newRecord.accountName}`;
                const details = `DCM ID: ${newRecord.id}\nAmount: ${formatCurrency(newRecord.amount)}\nType: ${newRecord.memoType.toUpperCase()} MEMO`;
                
                toastSystem.success(successMessage, details);
                
                // Reset form
                form.reset();
                document.getElementById('dcmDate').value = today;
                clearAccountName();
                clearDCMType();
                remarksInput.value = '';
                
                // Ensure DCM Type remains editable after form submission
                ensureDCMTypeEditable();
                
                console.log('DCM Records:', getSampleDCMRecords());
                
            } catch (error) {
                toastSystem.error('Error creating DCM', error.message);
            } finally {
                // Re-enable submit button
                submitBtn.disabled = false;
                submitBtn.textContent = 'Submit';
            }
        }, 1000);
    });

    // Display sample account hints
    function showAccountHints() {
        const hints = sampleAccounts.map(acc => `${acc.accountNumber} - ${acc.accountName}`).join('\n');
        console.log('Sample Accounts Available:\n' + hints);
    }

    // Show hints on page load
    showAccountHints();
});