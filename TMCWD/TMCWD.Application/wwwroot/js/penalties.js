let penaltyList = [];
let isLoaded = false;
let currentPage = 1;
let pageSize = 5;
let searchQuery = '';

document.addEventListener('DOMContentLoaded', function () {
    const loadBtn = document.getElementById('loadEntriesBtn');
    if (loadBtn) {
        loadBtn.addEventListener('click', function () {
            var dateInput = document.querySelector('input[type="date"], #billingDate');
            var billPeriod = dateInput ? dateInput.value : '';
            if (!billPeriod) {
                alertModal('Select a Billing Date', 'Please pick a billing date first.', 'error');
                return;
            }

            fetch('/Billing/GetBillByBillPeriod?billPeriod=' + encodeURIComponent(billPeriod))
                .then(function (res) { return res.ok ? res.json() : []; })
                .then(function (data) {
                    penaltyList = data || [];
                    isLoaded = true;
                    currentPage = 1;
                    renderTable();
                });
        });
    }

    const searchInput = document.getElementById('searchTextbox');
    if (searchInput) {
        searchInput.addEventListener('input', function () {
            searchQuery = this.value.trim().toLowerCase();
            currentPage = 1;
            renderTable();
        });
    }

    const waiveBtn = document.getElementById('waivePenaltyBtn');
    if (waiveBtn) {
        waiveBtn.addEventListener('click', function () {
            if (!isLoaded) {
                alertModal('Load Entries First', 'Please load the penalty list first before performing this action.', 'error');
                return;
            }
            const selected = document.querySelectorAll('.account-checkbox:checked');
            if (selected.length === 0) {
                alertModal('Load Entries First', 'Please load the penalty list first before performing this action.', 'error');
                return;
            }

            var refIds = Array.from(selected).map(function (cb) { return cb.dataset.ref; });

            fetch('/Billing/WaivePenalties', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ billingReferenceIds: refIds })
            })
                .then(function (res) {
                    if (!res.ok) throw new Error('Waive failed');
                    penaltyList = penaltyList.filter(function (item) {
                        return refIds.indexOf(item.billingReferenceId) === -1;
                    });
                    currentPage = 1;
                    renderTable();
                    alertModal('Penalties Waived', `Successfully waived penalties for ${selected.length} selected accounts.`);
                })
                .catch(function () {
                    alertModal('Error', 'Could not waive penalties. Try again.', 'error');
                });
        });
    }

    const selectAllCheck = document.getElementById('selectAllCheckbox');
    if (selectAllCheck) {
        selectAllCheck.addEventListener('change', function () {
            document.querySelectorAll('.account-checkbox').forEach(cb => {
                cb.checked = this.checked;
            });
        });
    }

    const rowsPerPageSelect = document.getElementById('rowsPerPage');
    if (rowsPerPageSelect) {
        rowsPerPageSelect.addEventListener('change', function () {
            pageSize = parseInt(this.value, 10);
            currentPage = 1;
            renderTable();
        });
    }

    document.getElementById('prevPageBtn')?.addEventListener('click', function () {
        if (currentPage > 1) { currentPage--; renderTable(); }
    });

    document.getElementById('nextPageBtn')?.addEventListener('click', function () {
        const totalRecords = getFilteredList().length;
        const totalPages = Math.ceil(totalRecords / pageSize) || 1;
        if (currentPage < totalPages) { currentPage++; renderTable(); }
    });
});

function getFilteredList() {
    if (!isLoaded) return [];
    if (!searchQuery) return penaltyList;
    return penaltyList.filter(item => item.accountNumber.toLowerCase().includes(searchQuery));
}

function renderTable() {
    const tbody = document.getElementById('penaltyTableBody');
    const infoEl = document.getElementById('paginationInfo');
    const prevBtn = document.getElementById('prevPageBtn');
    const nextBtn = document.getElementById('nextPageBtn');
    const selectAllCheck = document.getElementById('selectAllCheckbox');

    if (!tbody) return;

    if (!isLoaded) {
        tbody.innerHTML = `<tr><td colspan="6" class="pn-table-empty">Please select a billing date and click "Load Entries" to view accounts.</td></tr>`;
        if (infoEl) infoEl.textContent = '0–0 of 0';
        if (prevBtn) prevBtn.disabled = true;
        if (nextBtn) nextBtn.disabled = true;
        if (selectAllCheck) selectAllCheck.checked = false;
        return;
    }

    const filtered = getFilteredList();

    if (filtered.length === 0) {
        tbody.innerHTML = `<tr><td colspan="6" class="pn-table-empty">No accounts found matching search criteria.</td></tr>`;
        if (infoEl) infoEl.textContent = '0–0 of 0';
        if (prevBtn) prevBtn.disabled = true;
        if (nextBtn) nextBtn.disabled = true;
        if (selectAllCheck) selectAllCheck.checked = false;
        return;
    }

    const totalRecords = filtered.length;
    const totalPages = Math.ceil(totalRecords / pageSize) || 1;
    if (currentPage > totalPages) currentPage = totalPages;

    const startIndex = (currentPage - 1) * pageSize;
    const endIndex = Math.min(startIndex + pageSize, totalRecords);
    const pageData = filtered.slice(startIndex, endIndex);

    tbody.innerHTML = pageData.map(item => `
        <tr>
            <td style="font-weight: 600;">${item.accountNumber}</td>
            <td>${item.usage} m³</td>
            <td>₱${item.billAmount}</td>
            <td>₱${item.discount}</td>
            <td style="font-weight: 600; color: #ef4444;">₱${item.penalty}</td>
            <td class="pn-col-check">
                <input type="checkbox" class="pn-checkbox account-checkbox" data-ref="${item.billingReferenceId}" />
            </td>
        </tr>
    `).join('');

    if (selectAllCheck) selectAllCheck.checked = false;

    tbody.querySelectorAll('.account-checkbox').forEach(cb => {
        cb.addEventListener('change', function () {
            const allChecks = tbody.querySelectorAll('.account-checkbox');
            const checkedCount = tbody.querySelectorAll('.account-checkbox:checked').length;
            if (selectAllCheck) selectAllCheck.checked = checkedCount === allChecks.length;
        });
    });

    if (infoEl) infoEl.textContent = `${startIndex + 1}–${endIndex} of ${totalRecords}`;
    if (prevBtn) prevBtn.disabled = currentPage === 1;
    if (nextBtn) nextBtn.disabled = currentPage === totalPages;
}

function setModalIcon(type = 'success') {
    const icon = document.getElementById('modalIcon');
    const checkIcon = icon?.querySelector('.pn-modal-icon__check');
    const errorIcon = icon?.querySelector('.pn-modal-icon__error');
    const isError = type === 'error';
    if (icon) {
        icon.classList.remove('pn-modal-icon--success', 'pn-modal-icon--error');
        icon.classList.add(isError ? 'pn-modal-icon--error' : 'pn-modal-icon--success');
    }
    if (checkIcon) checkIcon.style.display = isError ? 'none' : 'block';
    if (errorIcon) errorIcon.style.display = isError ? 'block' : 'none';
}

function alertModal(title, msg, type = 'success') {
    setModalIcon(type);
    document.getElementById('modalTitle').textContent = title;
    document.getElementById('modalMsg').textContent = msg;
    document.getElementById('successModal').classList.add('active');
}

function closeSuccessModal() {
    document.getElementById('successModal').classList.remove('active');
    setTimeout(() => setModalIcon('success'), 300);
}