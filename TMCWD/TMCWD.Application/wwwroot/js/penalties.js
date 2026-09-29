let penaltyList = [], isLoaded = false, currentPage = 1, pageSize = 5, searchQuery = '';

document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('loadEntriesBtn')?.addEventListener('click', loadEntries);
    document.getElementById('searchTextbox')?.addEventListener('input', function () { searchQuery = this.value.trim().toLowerCase(); currentPage = 1; renderTable(); });
    document.getElementById('waivePenaltyBtn')?.addEventListener('click', submitWaiverRequests);
    document.getElementById('selectAllCheckbox')?.addEventListener('change', function () { document.querySelectorAll('.account-checkbox').forEach(x => x.checked = this.checked); });
    document.getElementById('rowsPerPage')?.addEventListener('change', function () { pageSize = parseInt(this.value, 10); currentPage = 1; renderTable(); });
    document.getElementById('prevPageBtn')?.addEventListener('click', () => { if (currentPage > 1) { currentPage--; renderTable(); } });
    document.getElementById('nextPageBtn')?.addEventListener('click', () => { if (currentPage < Math.ceil(filtered().length / pageSize)) { currentPage++; renderTable(); } });
    document.getElementById('refreshWaiversBtn')?.addEventListener('click', loadPendingWaivers);
    if (document.getElementById('waiverApprovalBody')) loadPendingWaivers();
});

async function loadEntries() {
    const date = document.getElementById('billingDate')?.value;
    if (!date) return alertModal('Select a Billing Date', 'Please pick a billing date first.', 'error');
    try {
        const response = await fetch('/Billing/GetPenaltiesByBillPeriod?billPeriod=' + encodeURIComponent(date));
        if (!response.ok) throw new Error();
        penaltyList = await response.json() || []; isLoaded = true; currentPage = 1; renderTable();
    } catch { alertModal('Error', 'Could not load penalties. Please try again.', 'error'); }
}

async function submitWaiverRequests() {
    if (!isLoaded) return alertModal('Load Entries First', 'Please load the penalty list first.', 'error');
    const selected = [...document.querySelectorAll('.account-checkbox:checked')];
    if (!selected.length) return alertModal('Select Penalties', 'Select at least one penalty.', 'error');
    const reason = document.getElementById('waiverReason')?.value.trim() || '';
    if (reason.length < 3 || reason.length > 500) return alertModal('Reason Required', 'Enter a reason between 3 and 500 characters.', 'error');
    const button = document.getElementById('waivePenaltyBtn'); if (button.disabled) return; button.disabled = true;
    try {
        const response = await fetch('/Billing/WaivePenalties', { method: 'POST', headers: jsonHeaders(), body: JSON.stringify({ billingReferenceIds: selected.map(x => x.dataset.ref), reason }) });
        const result = await responseData(response);
        if (!response.ok || !Number.isInteger(result.submittedCount) || result.submittedCount <= 0) throw new Error(result.message);
        selected.forEach(x => x.checked = false); document.getElementById('waiverReason').value = '';
        alertModal('Request Submitted', `${result.submittedCount} waiver request(s) sent to the Customer Service OIC.`);
    } catch (error) { alertModal('Error', error.message || 'The waiver request was not confirmed.', 'error'); }
    finally { button.disabled = false; }
}

async function loadPendingWaivers() {
    const body = document.getElementById('waiverApprovalBody'); if (!body) return;
    body.innerHTML = '<tr><td colspan="6" class="pn-table-empty">Loading pending requests...</td></tr>';
    try {
        const response = await fetch('/Billing/PendingPenaltyWaivers'); if (!response.ok) throw new Error();
        const requests = await response.json() || [];
        if (!requests.length) return void (body.innerHTML = '<tr><td colspan="6" class="pn-table-empty">No pending waiver requests.</td></tr>');
        body.innerHTML = requests.map(x => `<tr><td>${html(x.accountNumber)}</td><td>${html(x.billingReferenceId)}</td><td>₱${money(x.penaltyAmount)}</td><td>${html(x.requestedByName)}</td><td>${html(x.reason)}</td><td><button type="button" class="pn-btn-load" data-review-id="${x.id}" data-approve="true">Approve</button> <button type="button" class="pn-btn-waive" data-review-id="${x.id}" data-approve="false">Reject</button></td></tr>`).join('');
        body.querySelectorAll('[data-review-id]').forEach(x => x.addEventListener('click', reviewWaiver));
    } catch { body.innerHTML = '<tr><td colspan="6" class="pn-table-empty">Could not load pending requests.</td></tr>'; }
}

async function reviewWaiver(event) {
    const button = event.currentTarget, approve = button.dataset.approve === 'true';
    const remarks = prompt(`${approve ? 'Approval' : 'Rejection'} remarks (optional, maximum 500 characters):`, '');
    if (remarks === null) return; if (remarks.length > 500) return alertModal('Remarks Too Long', 'Remarks cannot exceed 500 characters.', 'error');
    button.disabled = true;
    try {
        const response = await fetch('/Billing/ReviewPenaltyWaiver', { method: 'POST', headers: jsonHeaders(), body: JSON.stringify({ requestId: Number(button.dataset.reviewId), approve, remarks }) });
        const result = await responseData(response); if (!response.ok) throw new Error(result.message);
        alertModal(approve ? 'Waiver Approved' : 'Waiver Rejected', approve ? `${result.waivedCount} penalty record(s) were waived.` : 'The request was rejected; the penalty remains active.');
        await loadPendingWaivers();
    } catch (error) { alertModal('Error', error.message || 'The review was not confirmed.', 'error'); }
    finally { button.disabled = false; }
}

function filtered() { return !isLoaded ? [] : !searchQuery ? penaltyList : penaltyList.filter(x => (x.accountNumber || '').toLowerCase().includes(searchQuery)); }
function renderTable() {
    const body = document.getElementById('penaltyTableBody'); if (!body) return;
    const list = filtered(), info = document.getElementById('paginationInfo'), previous = document.getElementById('prevPageBtn'), next = document.getElementById('nextPageBtn'), selectAll = document.getElementById('selectAllCheckbox');
    if (!isLoaded || !list.length) {
        body.innerHTML = `<tr><td colspan="6" class="pn-table-empty">${isLoaded ? 'No accounts found.' : 'Please select a billing date and click "Load Entries".'}</td></tr>`;
        if (info) info.textContent = '0–0 of 0'; if (previous) previous.disabled = true; if (next) next.disabled = true; if (selectAll) selectAll.checked = false; return;
    }
    const pages = Math.ceil(list.length / pageSize); currentPage = Math.min(currentPage, pages); const start = (currentPage - 1) * pageSize, end = Math.min(start + pageSize, list.length);
    body.innerHTML = list.slice(start, end).map(x => `<tr><td style="font-weight:600">${html(x.accountNumber)}</td><td>${html(x.usage)} m³</td><td>₱${money(x.billAmount)}</td><td>₱${money(x.discount)}</td><td style="font-weight:600;color:#ef4444">₱${money(x.penalty)}</td><td class="pn-col-check"><input type="checkbox" class="pn-checkbox account-checkbox" data-ref="${html(x.billingReferenceId)}"></td></tr>`).join('');
    if (info) info.textContent = `${start + 1}–${end} of ${list.length}`; if (previous) previous.disabled = currentPage === 1; if (next) next.disabled = currentPage === pages; if (selectAll) selectAll.checked = false;
}

function jsonHeaders() { return { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }; }
async function responseData(response) { const text = await response.text(); if (!text) return {}; try { return JSON.parse(text); } catch { return { message: text.replace(/^"|"$/g, '') }; } }
function html(value) { const element = document.createElement('div'); element.textContent = value == null ? '' : String(value); return element.innerHTML; }
function money(value) { return Number(value || 0).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
function setModalIcon(type = 'success') { const icon = document.getElementById('modalIcon'), error = type === 'error'; icon?.classList.toggle('pn-modal-icon--error', error); icon?.classList.toggle('pn-modal-icon--success', !error); const check = icon?.querySelector('.pn-modal-icon__check'), cross = icon?.querySelector('.pn-modal-icon__error'); if (check) check.style.display = error ? 'none' : 'block'; if (cross) cross.style.display = error ? 'block' : 'none'; }
function alertModal(title, message, type = 'success') { setModalIcon(type); document.getElementById('modalTitle').textContent = title; document.getElementById('modalMsg').textContent = message; document.getElementById('successModal').classList.add('active'); }
function closeSuccessModal() { document.getElementById('successModal').classList.remove('active'); setTimeout(() => setModalIcon('success'), 300); }
