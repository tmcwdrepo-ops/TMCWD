document.addEventListener('DOMContentLoaded', () => {
    const byId = id => document.getElementById(id);
    const form = byId('atForm');
    if (!form) return;
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const message = byId('atMessage');
    const value = id => byId(id)?.value?.trim() || '';
    const set = (id, val) => byId(id).value = val ?? '';
    let matches = [];
    let timer;
    let completed = false;

    async function loadZones(selectedZone, selectedBook) {
        try {
            const response = await fetch('/ZoneBook/GetZone');
            if (!response.ok) return;
            const rows = await response.json() || [];
            const zones = [...new Set(rows.map(x => Number(x.zone)).filter(Number.isFinite))];
            byId('atZone').innerHTML = '<option value="">Select zone</option>' + zones.map(x => `<option value="${x}">Zone ${x}</option>`).join('');
            if (selectedZone) {
                set('atZone', selectedZone);
                await loadBooks(selectedZone, selectedBook);
            }
        } catch { }
    }

    async function loadBooks(zone, selectedBook) {
        const book = byId('atBook');
        book.innerHTML = '<option value="">Select book</option>';
        book.disabled = true;
        if (!zone) return;
        try {
            const response = await fetch('/ZoneBook/GetBooksByZone?zone=' + encodeURIComponent(zone));
            if (!response.ok) return;
            const rows = await response.json() || [];
            book.innerHTML += rows.map(x => `<option value="${Number(x.book)}">Book ${Number(x.book)}</option>`).join('');
            book.disabled = false;
            if (selectedBook) set('atBook', selectedBook);
        } catch { }
    }
    byId('atZone').addEventListener('change', event => loadBooks(event.target.value));

    async function search() {
        const query = value('atAccountSearch');
        if (query.length < 2) return hideSuggestions();
        try {
            const response = await fetch('/CustomerSupport/SearchAccountTransferAccounts?q=' + encodeURIComponent(query));
            if (!response.ok) throw new Error(await response.text());
            matches = await response.json() || [];
            renderSuggestions();
        } catch (error) { showMessage(error.message || 'Unable to search accounts.'); }
    }
    byId('atAccountSearch').addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(search, 250); });
    byId('atSearchButton').addEventListener('click', search);
    document.addEventListener('click', event => { if (!event.target.closest('.at-account-search')) hideSuggestions(); });

    function renderSuggestions() {
        const box = byId('atSuggestions');
        box.innerHTML = matches.length ? matches.map((x, index) => `<button type="button" class="at-suggestion" data-transfer-index="${index}"><strong>${escapeHtml(x.accountNumber)}</strong><span>${escapeHtml(x.accountName)}</span><small>Zone ${Number(x.zone)} / Book ${Number(x.book)}</small></button>`).join('') : '<div class="at-suggestion"><span>No matching accounts</span></div>';
        box.hidden = false;
        box.querySelectorAll('[data-transfer-index]').forEach(button => button.addEventListener('click', () => selectAccount(matches[Number(button.dataset.transferIndex)])));
    }

    async function selectAccount(account) {
        set('atAccountId', account.accountId); set('atAccountSearch', account.accountNumber);
        set('atAccountNumber', account.accountNumber); set('atAccountName', account.accountName); set('atAddress', account.address);
        set('atNewAccount', '');
        await loadZones(account.zone, account.book);
        hideSuggestions(); showMessage('');
    }
    function hideSuggestions() { byId('atSuggestions').hidden = true; }

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (completed) return;
        if (!Number(value('atAccountId'))) return showMessage('Select an existing account first.');
        if (!form.reportValidity()) return;
        if (value('atAccountNumber').toLowerCase() === value('atNewAccount').toLowerCase()) return showMessage('The new account number must be different.');
        const submit = byId('atSubmit');
        submit.disabled = true; showMessage('Transferring account...');
        const payload = {
            accountId: Number(value('atAccountId')), jobOrderDate: value('atJobDate'), accountNumber: value('atAccountNumber'),
            accountName: value('atAccountName'), address: value('atAddress'), zone: Number(value('atZone')), book: Number(value('atBook')),
            newAccountNumber: value('atNewAccount'), transferDate: value('atTransferDate')
        };
        try {
            const response = await fetch('/CustomerSupport/SaveAccountTransfer', { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify(payload) });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved = await response.json();
            set('atJobNumber', saved.jobOrderNumber);
            byId('atSummaryNumber').textContent = saved.jobOrderNumber;
            byId('atSummaryDetail').textContent = `${saved.accountNumber} → ${saved.newAccountNumber}`;
            byId('atSummary').hidden = false;
            completed = true; submit.textContent = 'Transfer Completed';
            showMessage('The account transfer was completed successfully.', true);
        } catch (error) { showMessage(error.message || 'The account transfer could not be completed.'); }
        finally { submit.disabled = completed; }
    });

    function showMessage(text, success) { message.textContent = text; message.className = success ? 'is-success' : ''; }
    function escapeHtml(text) { const div = document.createElement('div'); div.textContent = text ?? ''; return div.innerHTML; }
    loadZones();
});
