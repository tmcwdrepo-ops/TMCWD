document.addEventListener('DOMContentLoaded', () => {
    const byId = id => document.getElementById(id);
    const form = byId('dcForm');
    if (!form) return;
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const message = byId('dcMessage');
    let accounts = [];
    let orders = [];
    let searchTimer;
    const value = id => byId(id)?.value?.trim() || '';
    const set = (id, val) => byId(id).value = val ?? '';
    const money = n => Number(n || 0).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const dateValue = date => date ? String(date).slice(0, 10) : '';

    async function searchAccounts() {
        const query = value('dcAccountSearch');
        if (query.length < 2) return hideSuggestions();
        try {
            const response = await fetch('/CustomerSupport/SearchDisconnectionAccounts?q=' + encodeURIComponent(query));
            if (!response.ok) throw new Error(await response.text());
            accounts = await response.json() || [];
            renderSuggestions();
        } catch (error) { showMessage(error.message || 'Could not search accounts.'); }
    }

    function renderSuggestions() {
        const box = byId('dcSuggestions');
        box.innerHTML = accounts.length ? accounts.map((x, index) => `<button type="button" class="dc-suggestion" data-account-index="${index}"><strong>${escapeHtml(x.accountNumber)}</strong><span>${escapeHtml(x.fullName)}</span><small>${escapeHtml(x.meterNumber || 'No meter')}</small></button>`).join('') : '<div class="dc-suggestion"><span>No matching accounts</span></div>';
        box.hidden = false;
        box.querySelectorAll('[data-account-index]').forEach(button => button.addEventListener('click', () => selectAccount(accounts[Number(button.dataset.accountIndex)])));
    }

    function selectAccount(account) {
        set('dcAccountId', account.accountId); set('dcAccountSearch', account.accountNumber);
        set('dcAccountNumber', account.accountNumber); set('dcFullName', account.fullName);
        set('dcAddress', account.address); set('dcMeterNumber', account.meterNumber);
        set('dcAccountStatus', account.accountStatus); set('dcLastReading', account.lastReading);
        hideSuggestions(); showMessage('');
    }

    function hideSuggestions() { byId('dcSuggestions').hidden = true; }
    byId('dcAccountSearch').addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(searchAccounts, 250); });
    byId('dcSearchButton').addEventListener('click', searchAccounts);
    document.addEventListener('click', event => { if (!event.target.closest('.dc-account-search')) hideSuggestions(); });

    document.querySelectorAll('[data-dc-action]').forEach(button => button.addEventListener('click', () => saveOrder(button.dataset.dcAction)));
    async function saveOrder(action) {
        if (!Number(value('dcAccountId'))) return showMessage('Select an account first.');
        if (!form.reportValidity()) return;
        if (action !== 'disconnect' && !Number(value('dcOrderId'))) return showMessage('Create or select a disconnection order first.');
        const buttons = document.querySelectorAll('[data-dc-action]');
        buttons.forEach(x => x.disabled = true);
        showMessage('Saving disconnection order...');
        const payload = {
            orderId: Number(value('dcOrderId')) || 0, accountId: Number(value('dcAccountId')),
            jobOrderDate: value('dcJobDate'), disconnectionDate: value('dcDate'), natureOfDisconnection: value('dcNature'),
            possession: value('dcPossession'), reason: value('dcReason'), lastReading: Number(value('dcLastReading')) || 0,
            plumber: value('dcPlumber'), reconnectionFee: Number(value('dcFee')) || 0, remarks: value('dcRemarks'),
            chargeFee: byId('dcChargeFee').checked, readingOverride: byId('dcOverride').checked, action
        };
        try {
            const response = await fetch('/CustomerSupport/SaveDisconnectionOrder', { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify(payload) });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved = await response.json();
            set('dcOrderId', saved.orderId); set('dcJobNumber', saved.jobOrderNumber);
            if (action === 'accomplish') set('dcAccountStatus', 'Suspended');
            showMessage(`${saved.jobOrderNumber} is now ${saved.status}.`, true);
        } catch (error) { showMessage(error.message || 'The disconnection order could not be saved.'); }
        finally { buttons.forEach(x => x.disabled = false); }
    }

    byId('dcShowOrders').addEventListener('click', showOrders);
    byId('dcCloseOrders').addEventListener('click', () => { byId('dcOrders').hidden = true; form.hidden = false; });
    byId('dcBatch').addEventListener('change', event => byId('dcOrders').classList.toggle('is-batch', event.target.checked));
    async function showOrders() {
        form.hidden = true; byId('dcOrders').hidden = false;
        byId('dcOrderRows').innerHTML = '<tr><td colspan="6">Loading orders...</td></tr>';
        try {
            const response = await fetch('/CustomerSupport/GetDisconnectionOrders');
            if (!response.ok) throw new Error(await response.text());
            orders = await response.json() || [];
            renderOrders();
        } catch (error) { byId('dcOrderRows').innerHTML = `<tr><td colspan="6">${escapeHtml(error.message || 'Unable to load orders.')}</td></tr>`; }
    }

    function renderOrders() {
        const body = byId('dcOrderRows');
        body.innerHTML = orders.length ? orders.map((x, index) => `<tr data-order-index="${index}"><td class="dc-batch-column"><input type="checkbox" aria-label="Select ${escapeHtml(x.jobOrderNumber)}" /></td><td><strong>${escapeHtml(x.jobOrderNumber)}</strong></td><td>${escapeHtml(x.accountNumber)}</td><td>${escapeHtml(x.fullName)}</td><td>${escapeHtml(dateValue(x.disconnectionDate))}</td><td>${escapeHtml(x.status)}</td></tr>`).join('') : '<tr><td colspan="6">No disconnection orders found.</td></tr>';
        body.querySelectorAll('[data-order-index]').forEach(row => row.addEventListener('click', event => { if (event.target.type === 'checkbox') return; loadOrder(orders[Number(row.dataset.orderIndex)]); }));
    }

    async function loadOrder(order) {
        set('dcOrderId', order.orderId); set('dcAccountId', order.accountId); set('dcJobNumber', order.jobOrderNumber);
        set('dcJobDate', dateValue(order.jobOrderDate)); set('dcAccountSearch', order.accountNumber); set('dcAccountNumber', order.accountNumber);
        set('dcFullName', order.fullName); set('dcAddress', order.address); set('dcMeterNumber', order.meterNumber);
        set('dcAccountStatus', order.status); set('dcDate', dateValue(order.disconnectionDate)); set('dcNature', order.natureOfDisconnection);
        set('dcPossession', order.possession); set('dcReason', order.reason); set('dcLastReading', order.lastReading);
        set('dcPlumber', order.plumber); set('dcFee', money(order.reconnectionFee).replaceAll(',', '')); set('dcRemarks', order.remarks);
        byId('dcChargeFee').checked = !!order.chargeFee; byId('dcOverride').checked = !!order.readingOverride;
        byId('dcOrders').hidden = true; form.hidden = false; showMessage('Order loaded.');
    }

    function showMessage(text, success) { message.textContent = text; message.className = success ? 'is-success' : ''; }
    function escapeHtml(text) { const div = document.createElement('div'); div.textContent = text ?? ''; return div.innerHTML; }
});
