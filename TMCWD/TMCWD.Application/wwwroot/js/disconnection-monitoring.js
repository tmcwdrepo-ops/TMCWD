(() => {
    'use strict';
    const get = id => document.getElementById(id);
    let rows = [], version = 0;
    let confirmationAccount = '', confirming = false, confirmationTrigger, reconnecting = false, confirmationEvent = '';
    const canDisconnect = document.querySelector('[data-can-disconnect]').dataset.canDisconnect === 'true';
    const canReconnect = document.querySelector('[data-can-reconnect]').dataset.canReconnect === 'true';
    const disconnectionWarning = get('dmConfirmationWarning').cloneNode(true);
    const confirmationLabel = () => reconnecting ? 'Confirm Physical Reconnection' : 'Confirm Physical Disconnection';
    const money = value => new Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP' }).format(value);
    const date = value => value ? new Intl.DateTimeFormat('en-PH', { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' }).format(new Date(String(value).slice(0, 10) + 'T00:00:00Z')) : '—';
    function node(tag, text, className) {
        const element = document.createElement(tag);
        if (text != null) element.textContent = text;
        if (className) element.className = className;
        return element;
    }
    function cell(row, label) { const td = node('td'); td.dataset.label = label; row.appendChild(td); return td; }
    function statusClass(status) {
        if (status === 'FOR DISCONNECTION' || status === 'DISCONNECTED') return 'danger';
        if (status === 'FIRST BILL UNPAID' || status === 'BALANCE + RECONNECTION FEE PENDING') return 'warning';
        if (status === 'SUBJECT FOR DISCONNECTION') return 'subject';
        if (status === 'ELIGIBLE FOR RECONNECTION' || status === 'RECONNECTED') return 'success';
        if (status === 'FINANCIAL / SERVICE STATE REVIEW REQUIRED') return 'review';
        return 'current';
    }
    const badge = status => node('span', status, 'dm-status dm-status--' + statusClass(status));
    function detail(list, label, value, className) {
        const group = node('div', null, className);
        group.append(node('dt', label), node('dd', value)); list.appendChild(group);
    }
    function bill(row, label, value, reference) {
        const td = cell(row, label);
        td.append(node('div', date(value)), node('small', 'Ref: ' + (reference || '—'), 'dm-reference'));
    }
    function render() {
        const filtered = rows.filter(r => !get('dmStatus').value || r.status === get('dmStatus').value ||
            (get('dmStatus').value === 'DISCONNECTED' && r.physicalServiceStatus === 'DISCONNECTED'));
        get('dmRows').replaceChildren();
        get('dmSummary').hidden = false;
        document.querySelectorAll('[data-dm-count]').forEach(counter => {
            const status = counter.dataset.dmCount;
            counter.textContent = status === 'total' ? filtered.length : filtered.filter(r =>
                status === 'DISCONNECTED' ? r.physicalServiceStatus === status : r.status === status).length;
        });
        get('dmEmpty').hidden = !!filtered.length;
        get('dmResults').hidden = !filtered.length;
        get('dmError').hidden = true;
        filtered.forEach(r => {
            // A matching server event also acknowledges a confirmation whose original response was lost.
            try {
                const key = 'tmcwd-disconnection:' + r.accountNumber;
                if (r.actualDisconnectionDateUtc && r.eventId && sessionStorage.getItem(key) === r.eventId) sessionStorage.removeItem(key);
                if (r.actualReconnectionDateUtc && r.eventId) sessionStorage.removeItem('tmcwd-reconnection:' + r.accountNumber + ':' + r.eventId);
            } catch { /* Keep the pending identity when storage is unavailable. */ }
            const tr = node('tr', null, 'dm-row--' + statusClass(r.status));
            cell(tr, 'Account').appendChild(node('strong', r.accountNumber, 'dm-account'));
            cell(tr, 'Customer / address').append(node('strong', r.accountName), node('div', r.serviceAddress, 'dm-muted'));
            bill(tr, 'First unpaid bill', r.firstBillingDate, r.firstBillingReference);
            bill(tr, 'Second bill', r.secondBillingDate, r.secondBillingReference);
            const schedule = node('dl', null, 'dm-schedule');
            detail(schedule, 'Subject', date(r.subjectDate));
            detail(schedule, 'Scheduled', date(r.scheduledDisconnectionDate));
            detail(schedule, 'Days remaining', r.daysRemaining == null ? '—' : String(Math.max(0, r.daysRemaining)));
            if (r.status === 'FOR DISCONNECTION') schedule.appendChild(node('span', 'Due now', 'dm-due'));
            cell(tr, 'Disconnection schedule').appendChild(schedule);
            const balance = node('dl', null, 'dm-balance');
            detail(balance, 'Water', money(r.outstandingWaterBalance));
            detail(balance, 'Penalty', money(r.activePenaltyBalance));
            detail(balance, 'Other Charges', money(r.collectibleOtherCharges));
            detail(balance, 'Reconnection', money(r.reconnectionFeeBalance));
            detail(balance, 'Total', money(r.totalCollectibleBalance), 'dm-balance-total');
            cell(tr, 'Balances').appendChild(balance);
            const status = cell(tr, 'Status'); status.appendChild(badge(r.status));
            if (r.actualDisconnectionDateUtc) {
                const physical = node('div', null, 'dm-physical');
                physical.append(badge(r.physicalServiceStatus), node('small', `Disconnection: ${date(r.actualDisconnectionDateUtc)}; Reconnection: ${date(r.actualReconnectionDateUtc)}`, 'dm-reference'));
                status.appendChild(physical);
            }
            const actions = node('div', null, 'dm-actions'); cell(tr, 'Actions').appendChild(actions);
            const ledger = node('button', 'View Ledger', 'dm-button'); ledger.type = 'button';
            ledger.addEventListener('click', () => window.openAccountLedger(r.accountNumber, { initialTab: 'bills' })); actions.appendChild(ledger);
            const account = node('a', 'View Account', 'dm-button');
            account.href = '/CustomerSupport/CustomerInformation?customerId=' + encodeURIComponent(r.customerId) + '&accountNumber=' + encodeURIComponent(r.accountNumber);
            actions.appendChild(account);
            if ((canDisconnect && r.status === 'FOR DISCONNECTION') || (canReconnect && r.status === 'ELIGIBLE FOR RECONNECTION')) {
                const reconnect = r.status === 'ELIGIBLE FOR RECONNECTION';
                const confirm = node('button', reconnect ? 'Confirm Physical Reconnection' : 'Confirm Physical Disconnection', 'dm-button ' + (reconnect ? 'dm-button--primary' : 'dm-button--danger')); confirm.type = 'button';
                if (reconnect) confirm.dataset.reconnectionConfirm = ''; else confirm.dataset.disconnectionConfirm = '';
                confirm.addEventListener('click', () => {
                    reconnecting = reconnect; confirmationEvent = r.eventId || '';
                    confirmationAccount = r.accountNumber;
                    confirmationTrigger = confirm;
                    get('dmConfirmationAccount').textContent = r.accountNumber;
                    get('dmConfirmationCustomer').textContent = r.accountName;
                    get('dmConfirmationSchedule').textContent = date(r.scheduledDisconnectionDate);
                    get('dmConfirmationBalance').textContent = money(r.totalCollectibleBalance);
                    get('dmConfirmationTitle').textContent = get('dmConfirm').textContent = confirmationLabel();
                    get('dmConfirm').className = 'dm-button ' + (reconnect ? 'dm-button--primary' : 'dm-button--danger');
                    get('dmReconnectionFee').hidden = !reconnect;
                    get('dmConfirmationWarning').replaceChildren(...(reconnect ? get('dmReconnectionWarning').content.cloneNode(true).childNodes : disconnectionWarning.cloneNode(true).childNodes));
                    get('dmConfirmationMessage').textContent = '';
                    get('dmConfirmation').showModal();
                    get('dmCancel').focus();
                });
                actions.appendChild(confirm);
            }
            get('dmRows').appendChild(tr);
        });
        get('dmMessage').textContent = filtered.length ? `${filtered.length} account(s).` : 'No accounts match these filters.';
    }
    async function load(event) {
        event?.preventDefault();
        const current = ++version;
        rows = []; get('dmResults').setAttribute('aria-busy', 'true'); get('dmRows').replaceChildren();
        get('dmLoading').hidden = false; get('dmResults').hidden = true;
        get('dmSummary').hidden = true; get('dmEmpty').hidden = true; get('dmError').hidden = true; get('dmNotice').hidden = true;
        get('dmMessage').textContent = 'Loading service monitoring…';
        try {
            const params = new URLSearchParams({ q: get('dmQuery').value.trim() });
            if (get('dmZone').value) params.set('zone', get('dmZone').value);
            if (get('dmBook').value) params.set('book', get('dmBook').value);
            const response = await fetch('/CustomerSupport/DisconnectionMonitoringData?' + params, { headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error(response.status === 503 ? 'Monitoring requires its reviewed database migration.' :
                [401, 403].includes(response.status) ? 'You are not authorized to view monitoring.' : 'Unable to load monitoring. Narrow the search or retry.');
            const data = await response.json();
            if (current !== version) return;
            rows = data; render();
        } catch (e) { if (current === version) { rows = []; get('dmMessage').textContent = e.message; get('dmError').hidden = false; } }
        finally { if (current === version) { get('dmResults').setAttribute('aria-busy', 'false'); get('dmLoading').hidden = true; } }
    }
    get('dmFilters').addEventListener('submit', load);
    get('dmStatus').addEventListener('change', render);
    get('dmRetry').addEventListener('click', load);
    document.querySelectorAll('[data-dm-clear]').forEach(button => button.addEventListener('click', () => { get('dmFilters').reset(); load(); }));
    get('dmCancel').addEventListener('click', () => { if (!confirming) get('dmConfirmation').close(); });
    get('dmConfirmation').addEventListener('cancel', e => { if (confirming) e.preventDefault(); });
    get('dmConfirmation').addEventListener('keydown', e => {
        if (e.key === 'Escape' && confirming) { e.preventDefault(); return; }
        if (e.key !== 'Tab') return;
        const buttons = [...get('dmConfirmation').querySelectorAll('button:not([disabled])')];
        if (!buttons.length) { e.preventDefault(); return; }
        const target = e.shiftKey ? buttons.at(-1) : buttons[0];
        if (document.activeElement === (e.shiftKey ? buttons[0] : buttons.at(-1))) {
            e.preventDefault(); target.focus();
        }
    });
    get('dmConfirmation').addEventListener('close', () => {
        if (confirmationTrigger?.isConnected) confirmationTrigger.focus();
        else if (!get('dmResults').hidden) get('dmResults').focus();
    });
    get('dmConfirm').addEventListener('click', async () => {
        if (confirming || !confirmationAccount) return;
        confirming = true; get('dmConfirm').disabled = true; get('dmCancel').disabled = true;
        get('dmConfirm').textContent = 'Confirming…'; get('dmConfirmation').setAttribute('aria-busy', 'true');
        const storageKey = reconnecting ? 'tmcwd-reconnection:' + confirmationAccount + ':' + confirmationEvent : 'tmcwd-disconnection:' + confirmationAccount;
        let failureMessage = 'Retry storage or crypto is unavailable. Confirmation was not sent.';
        try {
            if (reconnecting && !confirmationEvent) throw new Error('Missing event identity.');
            let operationId = sessionStorage.getItem(storageKey);
            if (!operationId) {
                operationId = crypto.randomUUID();
                sessionStorage.setItem(storageKey, operationId);
            }
            get('dmConfirmationMessage').textContent = reconnecting ? 'Confirming physical reconnection…' : 'Confirming physical disconnection…';
            failureMessage = 'The confirmation outcome is uncertain and could not be verified. Reload the account before another action; retry uses the retained identity.';
            const response = await fetch('/CustomerSupport/' + (reconnecting ? 'ConfirmReconnection' : 'ConfirmDisconnection') + '?accountNumber=' + encodeURIComponent(confirmationAccount), {
                method: 'POST', headers: { 'Content-Type': 'application/json',
                    RequestVerificationToken: document.querySelector('input[name="__RequestVerificationToken"]').value },
                body: JSON.stringify({ operationId })
            });
            if (!response.ok) {
                failureMessage = response.status === 409 ? 'This account is no longer eligible for ' + (reconnecting ? 'reconnection' : 'disconnection') + ', was already confirmed, or needs reconciliation. Reload and review.' :
                    [401, 403].includes(response.status) ? 'Your session does not have permission to perform this action.' : failureMessage;
                throw new Error(failureMessage);
            }
            // Only a successful server acknowledgement releases the retry identity.
            await response.json(); sessionStorage.removeItem(storageKey);
            get('dmConfirmation').close(); await load();
            get('dmNotice').textContent = reconnecting ? 'Physical reconnection confirmed. The account is now Active. No new fee was created.' : 'Physical disconnection confirmed. Reconnection Fee: ' + money(350) + '. The account is now marked Disconnected.';
            get('dmNotice').hidden = false;
            if (!get('dmResults').hidden) get('dmResults').focus();
        } catch { get('dmConfirmationMessage').textContent = failureMessage; }
        finally {
            confirming = false; get('dmConfirm').disabled = false; get('dmCancel').disabled = false;
            get('dmConfirm').textContent = confirmationLabel(); get('dmConfirmation').setAttribute('aria-busy', 'false');
        }
    });
    load();
})();
