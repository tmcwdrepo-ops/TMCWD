document.addEventListener('DOMContentLoaded', () => {
    const get = id => document.getElementById(id);
    const form = get('srForm');
    if (!form) return;
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const value = id => get(id)?.value?.trim() || '';
    const set = (id, val) => { get(id).value = val ?? ''; };
    let accounts = [];
    let timer;
    let saved = false;

    function textFromError(raw) {
        try { const parsed = JSON.parse(raw); return parsed.detail || parsed.title || (typeof parsed === 'string' ? parsed : raw); }
        catch { return raw.replace(/^"|"$/g, ''); }
    }
    function showMessage(text, success = false) { const el = get('srMessage'); el.textContent = text; el.className = success ? 'is-success' : ''; }
    function escapeHtml(text) { const div = document.createElement('div'); div.textContent = text ?? ''; return div.innerHTML; }
    function hideSuggestions() { get('srSuggestions').hidden = true; }

    async function searchAccounts() {
        if (get('srNonCustomer').checked) return;
        const query = value('srAccountSearch');
        if (query.length < 2) return hideSuggestions();
        try {
            const response = await fetch('/CustomerSupport/SearchDisconnectionAccounts?q=' + encodeURIComponent(query));
            if (!response.ok) throw new Error(textFromError(await response.text()));
            accounts = await response.json() || [];
            const box = get('srSuggestions');
            box.innerHTML = accounts.length ? accounts.map((x, index) => `<button type="button" class="sr-suggestion" data-sr-index="${index}"><strong>${escapeHtml(x.accountNumber)}</strong><span>${escapeHtml(x.fullName)}</span><small>${escapeHtml(x.address)}</small></button>`).join('') : '<div class="sr-suggestion"><span>No matching accounts</span></div>';
            box.hidden = false;
            box.querySelectorAll('[data-sr-index]').forEach(button => button.addEventListener('click', () => selectAccount(accounts[Number(button.dataset.srIndex)])));
        } catch (error) { showMessage(error.message || 'Unable to search accounts.'); }
    }
    function selectAccount(account) {
        set('srAccountId', account.accountId); set('srAccountSearch', account.accountNumber); set('srAccountName', account.fullName);
        set('srAddress', account.address); set('srReportedBy', ''); set('srContact', account.contactNumber);
        hideSuggestions(); showMessage('');
    }
    get('srAccountSearch').addEventListener('input', () => { set('srAccountId', 0); clearTimeout(timer); timer = setTimeout(searchAccounts, 250); });
    get('srSearchButton').addEventListener('click', searchAccounts);
    document.addEventListener('click', event => { if (!event.target.closest('.sr-account-search')) hideSuggestions(); });

    function applyCustomerMode() {
        const nonCustomer = get('srNonCustomer').checked;
        get('srAccountSearch').disabled = nonCustomer;
        get('srSearchButton').disabled = nonCustomer;
        ['srAddress', 'srContact'].forEach(id => get(id).readOnly = !nonCustomer);
        get('srReportedBy').readOnly = false;
        set('srAccountId', 0); set('srAccountSearch', ''); set('srAccountName', ''); set('srAddress', ''); set('srReportedBy', ''); set('srContact', '');
        hideSuggestions(); showMessage(nonCustomer ? 'Enter the reporter details for this non-customer request.' : 'Select an existing account.');
    }
    get('srNonCustomer').addEventListener('change', applyCustomerMode);

    get('srReferredTo').addEventListener('change', event => get('srDateReferred').disabled = !event.target.value);

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (saved) return;
        const nonCustomer = get('srNonCustomer').checked;
        if (!nonCustomer && !Number(value('srAccountId'))) return showMessage('Select an existing account first.');
        if (!form.reportValidity()) return;
        const submit = get('srSubmit');
        submit.disabled = true; showMessage('Creating service request...');
        const payload = {
            isNonCustomer: nonCustomer, accountId: nonCustomer ? null : Number(value('srAccountId')),
            address: value('srAddress'), reportedBy: value('srReportedBy'), contactNumber: value('srContact'),
            jobOrderDate: value('srJobDate'), source: value('srSource'), jobOrderType: value('srType'),
            jobDescription: value('srDescription'), referredToDepartment: value('srReferredTo'),
            dateReferred: value('srReferredTo') ? value('srDateReferred') : null, name: value('srName'), particulars: value('srParticulars')
        };
        try {
            const response = await fetch('/CustomerSupport/SaveServiceRequest', { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify(payload) });
            if (!response.ok) throw new Error(textFromError(await response.text()));
            const result = await response.json();
            set('srJobNumber', result.jobOrderNumber);
            get('srResultNumber').textContent = result.jobOrderNumber;
            get('srResultStatus').textContent = `${result.status} · Received by ${result.receivedByName}`;
            get('srResult').hidden = false;
            saved = true; submit.textContent = 'Request Created';
            showMessage('The service request was saved successfully.', true);
        } catch (error) { showMessage(error.message || 'The service request could not be saved.'); }
        finally { submit.disabled = saved; }
    });

});
