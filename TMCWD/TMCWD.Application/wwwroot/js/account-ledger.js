(() => {
    'use strict';
    const dialog = document.getElementById('accountLedger');
    if (!dialog) return;
    const find = selector => dialog.querySelector(selector);
    const money = value => value == null ? '—' : new Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP' }).format(value);
    const date = (value, month = false, time = false) => {
        if (!value || String(value).startsWith('0001-')) return '—';
        const parsed = new Date(value);
        if (Number.isNaN(parsed.getTime())) return '—';
        return parsed.toLocaleString('en-PH', month ? { month: 'short', year: 'numeric' } :
            { year: 'numeric', month: 'short', day: 'numeric', ...(time ? { hour: '2-digit', minute: '2-digit' } : {}) });
    };
    const statusLabels = ['Draft', 'Unpaid', 'Paid', 'Overdue', 'Waived'];
    const status = (value, child = false) => {
        if (value == null || value === '') return '—';
        if (child && value === 0) return 'Future';
        const key = Number(value);
        if (Number.isNaN(key)) return String(value);
        return statusLabels[key] || 'Unknown';
    };
    const statusClass = label => {
        const normalized = String(label || '').trim().toLowerCase();
        if (normalized === 'paid') return 'account-ledger__status--paid';
        if (normalized === 'unpaid' || normalized === 'disconnected') return 'account-ledger__status--danger';
        if (normalized === 'overdue') return 'account-ledger__status--overdue';
        if (normalized === 'future') return 'account-ledger__status--future';
        if (normalized === 'active') return 'account-ledger__status--active';
        if (normalized === 'inactive') return 'account-ledger__status--inactive';
        return 'account-ledger__status--default';
    };
    function element(tag, text, className) {
        const node = document.createElement(tag);
        if (text != null) node.textContent = String(text);
        if (className) node.className = className;
        return node;
    }
    function badge(label) {
        const text = label == null || label === '' ? '—' : String(label);
        const span = element('span', text, 'account-ledger__badge ' + statusClass(text));
        return span;
    }
    function row(values, numeric = [], statusColumns = []) {
        const tr = element('tr');
        values.forEach((value, index) => {
            const isStatusCell = statusColumns.includes(index);
            const td = element('td', isStatusCell ? null : (value ?? '—'), numeric.includes(index) ? 'account-ledger__money' : '');
            if (isStatusCell) td.append(badge(value));
            tr.append(td);
        });
        return tr;
    }
    function empty(body, count, message) {
        if (body.children.length) return;
        const td = element('td', message); td.colSpan = count;
        const tr = element('tr'); tr.append(td); body.append(tr);
    }
    let accountNumber = '', currentTab = 'bills', requestNumber = 0, controller;
    function selectTab(tab, focus = false) {
        currentTab = ['bills', 'otherCharges', 'history'].includes(tab) ? tab : 'bills';
        dialog.querySelectorAll('[data-ledger-tab]').forEach(button => {
            const selected = button.dataset.ledgerTab === currentTab;
            button.setAttribute('aria-selected', String(selected));
            button.tabIndex = selected ? 0 : -1;
            const panel = document.getElementById(button.getAttribute('aria-controls'));
            if (panel) panel.hidden = !selected;
            button.classList.toggle('is-active', selected);
            if (selected && focus) button.focus();
        });
    }
    function render(data) {
        if (!data?.account || !Array.isArray(data.bills) || !Array.isArray(data.otherCharges) || !Array.isArray(data.history)
            || !Number.isFinite(data.totalBalance) || !Number.isFinite(data.collectibleOtherCharges)) throw new Error('Invalid ledger response.');

        dialog.querySelectorAll('[data-header]').forEach(node => {
            const value = data.account[node.dataset.header] ?? '—';
            node.textContent = value === '' ? '—' : value;
            if (node.dataset.header === 'status') {
                const label = String(value || '—');
                node.className = 'account-ledger__status ' + statusClass(label);
                node.textContent = label === '—' ? '—' : label;
            }
        });

        find('[data-ledger-balance]').textContent = money(data.totalBalance);
        find('[data-ledger-collectible]').textContent = money(data.collectibleOtherCharges);

        const bills = find('[data-ledger-bills]');
        bills.replaceChildren();
        data.bills.forEach(bill => {
            const c = bill.collection || {};
            bills.append(row([
                date(bill.billingPeriod, true),
                c.serviceInvoiceNumber || '—',
                c.billingReferenceId || '—',
                date(bill.readingDate),
                date(bill.billingDate),
                date(c.dueDate),
                money(c.billAmount),
                money(c.meterMaintenanceFee),
                money(c.penalty),
                money(c.remainingAmount),
                status(bill.paymentStatus),
                c.paymentReference || '—'
            ], [6, 7, 8, 9], [10]));
        });
        empty(bills, 12, 'No bills found for this account.');

        const charges = find('[data-ledger-charges]');
        charges.replaceChildren();
        data.otherCharges.forEach(charge => {
            const section = element('article', null, 'account-ledger__charge');
            const header = element('div', null, 'account-ledger__charge-header');
            const title = element('h3', charge.description || 'Other Charge');
            const parentStatus = element('span', charge.isActive ? status(charge.paymentStatus) : 'Inactive', 'account-ledger__badge ' + statusClass(charge.isActive ? status(charge.paymentStatus) : 'Inactive'));
            header.append(title, parentStatus);
            const details = element('div', null, 'account-ledger__charge-meta');
            details.append(element('span', charge.particulars || 'No particulars available.'));
            const totals = element('div', null, 'account-ledger__charge-totals');
            const contractTotal = element('div', null, 'account-ledger__charge-total');
            const contractLabel = element('span', 'Contract Total');
            const contractValue = element('strong', money(charge.contractTotal));
            contractTotal.append(contractLabel, contractValue);
            const collectibleTotal = element('div', null, 'account-ledger__charge-total');
            const collectibleLabel = element('span', 'Collectible Now');
            const collectibleValue = element('strong', money(charge.collectibleNow));
            collectibleTotal.append(collectibleLabel, collectibleValue);
            totals.append(contractTotal, collectibleTotal);
            const meta = element('div', null, 'account-ledger__charge-foot');
            meta.append(element('span', 'Installments: ' + (charge.installmentCount ?? '—')));
            meta.append(element('span', 'Parent Status: ' + (charge.isActive ? status(charge.paymentStatus) : 'Inactive')));

            section.append(header, details, totals, meta);

            const wrap = element('div', null, 'account-ledger__table-wrap');
            const table = element('table');
            const head = element('thead');
            const headRow = element('tr');
            ['#', 'Due Period', 'Amount', 'Status', 'Receipt', 'Activated Billing Reference'].forEach(label => headRow.append(element('th', label)));
            head.append(headRow);
            const body = element('tbody');
            if (Array.isArray(charge.installments) && charge.installments.length) {
                charge.installments.forEach(child => body.append(row([
                    child.installmentNumber ?? '—',
                    date(child.dueBillingPeriod, true),
                    money(child.amount),
                    status(child.paymentStatus, true),
                    child.paymentReference || '—',
                    child.activatedBillingReferenceId || '—'
                ], [2], [3])));
            } else {
                const tr = element('tr');
                const td = element('td', 'Draft plan: no posted installments.');
                td.colSpan = 6;
                tr.append(td);
                body.append(tr);
            }
            table.append(head, body);
            wrap.append(table);
            section.append(wrap);
            charges.append(section);
        });
        if (!charges.children.length) {
            const emptyState = element('div', 'No Other Charges found for this account.', 'account-ledger__empty');
            charges.append(emptyState);
        }

        const history = find('[data-ledger-history]');
        history.replaceChildren();
        data.history.forEach(event => history.append(row([
            date(event.date, false, true),
            event.type || '—',
            event.reference || '—',
            event.description || '—',
            money(event.debit),
            money(event.credit),
            event.status || '—'
        ], [4, 5], [6])));
        empty(history, 7, 'No transaction history found for this account.');
        find('[data-ledger-content]').hidden = false;
    }
    async function load() {
        controller?.abort(); controller = new AbortController();
        const request = ++requestNumber;
        find('[data-ledger-content]').hidden = true;
        find('[data-ledger-message]').textContent = 'Loading account ledger…';
        dialog.setAttribute('aria-busy', 'true');
        try {
            if (!accountNumber) throw new Error('Select an account first.');
            const response = await fetch('/Billing/AccountLedgerData?accountNumber=' + encodeURIComponent(accountNumber),
                { headers: { Accept: 'application/json' }, signal: controller.signal });
            if (!response.ok) throw new Error(response.status === 404 ? 'Account not found.' :
                [401, 403].includes(response.status) ? 'You are not authorized to view this account ledger.' : 'Unable to load the account ledger. Use Refresh to try again.');
            const data = await response.json();
            if (request !== requestNumber) return;
            render(data); find('[data-ledger-message]').textContent = '';
        } catch (error) {
            if (request === requestNumber && error.name !== 'AbortError') find('[data-ledger-message]').textContent = error.message;
        } finally { if (request === requestNumber) dialog.setAttribute('aria-busy', 'false'); }
    }
    window.openAccountLedger = (number, options = {}) => {
        accountNumber = String(number || '').trim(); selectTab(options.initialTab || 'bills');
        if (!dialog.open) dialog.showModal();
        return load();
    };
    find('[data-ledger-refresh]').addEventListener('click', load);
    dialog.addEventListener('close', () => {
        if (dialog.open) return; // A queued close event must not abort a ledger that has already reopened.
        controller?.abort(); ++requestNumber;
    });
    dialog.addEventListener('click', event => {
        const tab = event.target.closest('[data-ledger-tab]'); if (tab) selectTab(tab.dataset.ledgerTab);
    });
    dialog.querySelector('[role="tablist"]').addEventListener('keydown', event => {
        const tabs = [...dialog.querySelectorAll('[data-ledger-tab]')], index = tabs.indexOf(document.activeElement);
        if (index < 0 || !['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
        event.preventDefault();
        const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 :
            (index + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
        selectTab(tabs[next].dataset.ledgerTab, true);
    });
    document.addEventListener('click', event => {
        if (event.target.closest('.link-chip[data-modal="ledger"]')) window.openAccountLedger(window._ocCurrentAccount?.accountNumber);
    });
    selectTab('bills');
})();
