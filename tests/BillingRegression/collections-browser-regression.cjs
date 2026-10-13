// Actual Collections Razor body and scripts in headless Edge; synthetic HTTP only, no application database.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { spawn } = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const web = path.join(root, 'TMCWD/TMCWD.Application');
const ledgerPartial = fs.readFileSync(path.join(web, 'Views/Shared/_AccountLedger.cshtml'), 'utf8').replaceAll('="~/', '="/');
const view = fs.readFileSync(path.join(web, 'Views/Collections/Collections.cshtml'), 'utf8').replace('<partial name="_AccountLedger" />', ledgerPartial.replace(/<script[^>]*>[\s\S]*?<\/script>/g, ''));
const scripts = [...view.matchAll(/<script src="~([^"?]+)[^"]*"/g)].map(m => m[1]).concat('/js/account-ledger.js');
const body = view.slice(view.indexOf('@Html.AntiForgeryToken()')).replace('@Html.AntiForgeryToken()', '<input name="__RequestVerificationToken" value="synthetic-csrf" type="hidden">');
const adminView = fs.readFileSync(path.join(web, 'Views/Admin/Index.cshtml'), 'utf8');
const chargeWorkspace = adminView.slice(adminView.indexOf('    <div class="dashboard-modal" id="applicationsModal"'), adminView.indexOf('    <div class="dashboard-modal" id="inspectionModal"'));
const profile = fs.mkdtempSync(path.join(root, '.tmp/collection-browser-'));
const browserPath = process.env.TMCWD_BROWSER || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
let browser, socket, nextId = 0, state;
const pending = new Map(), errors = [];
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const money = value => Number(value).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://localhost');
  const json = (value, status = 200) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(value)); };
  if (url.pathname === '/') return res.end(`<!doctype html><html><head><meta charset="utf-8"><link rel="stylesheet" href="/css/collections.css"></head><body>${body}${scripts.map(src => `<script src="${src}"></script>`).join('')}</body></html>`);
  if (url.pathname === '/dashboard') return res.end(`<!doctype html><html><head><meta charset="utf-8"></head><body><input name="__RequestVerificationToken" value="synthetic-csrf" type="hidden"><button data-service-category="charging" data-service-title="Charging">Charging</button>${chargeWorkspace}${ledgerPartial}<script src="/js/dashboard.js"></script></body></html>`);
  if (scripts.includes(url.pathname) || url.pathname === '/js/dashboard.js' || url.pathname === '/css/collections.css' || url.pathname === '/css/account-ledger.css') {
    res.setHeader('Content-Type', url.pathname.endsWith('.js') ? 'text/javascript' : 'text/css');
    return res.end(fs.readFileSync(path.join(web, 'wwwroot', url.pathname)));
  }
  if (url.pathname === '/Collections/CurrentCollector') return json({ id: 7, name: 'Synthetic Cashier', date: '2026-10-07' });
  if (url.pathname === '/Billing/AccountLedgerData') {
    assert.equal(req.method, 'GET'); state.ledgerReads = (state.ledgerReads || 0) + 1;
    if (url.searchParams.get('accountNumber') === 'MISSING') return json({ message: 'Account not found.' }, 404);
    if (url.searchParams.get('accountNumber') === 'FAILED') return json({ message: 'Unexpected server failure.' }, 500);
    if (url.searchParams.get('accountNumber') === 'EMPTY') return json({ account: { accountNumber: 'EMPTY', accountName: 'Empty Synthetic Account' },
      totalBalance: 0, collectibleOtherCharges: 0, bills: [], otherCharges: [], history: [] });
    const collectible = (state.otherCharges || []).reduce((sum, c) => sum + c.amount, 0);
    return json({ account: { accountNumber: '01-010015', accountName: 'Authoritative Customer', serviceAddress: 'Authoritative Street', meterNumber: 'METER-1', classification: 'Residential', status: state.ledgerAccountStatus ?? 'Active', rateCode: null },
      totalBalance: state.bill.remainingAmount + state.bill.penalty + collectible, collectibleOtherCharges: collectible,
      bills: [{ billingPeriod: '2026-10-01T00:00:00', readingDate: '2026-10-01T08:00:00', billingDate: '2026-10-02T00:00:00', paymentStatus: state.ledgerPaymentStatus ?? 1, collection: { ...state.bill, billingDate: '2026-10-02T00:00:00' } }],
      otherCharges: [{ id: 501, description: 'Synthetic Fee', particulars: '<img src=x onerror="window.injected=true">', contractTotal: 1000, collectibleNow: collectible, installmentCount: 4, paymentStatus: 1, isActive: true,
        installments: [1,2,3,4].map(n => ({ id: 9000+n, installmentNumber: n, dueBillingPeriod: `2026-${String(9+n).padStart(2,'0')}-01`, amount: 250, paymentStatus: n > 1 ? 0 : collectible ? 1 : 2, paymentReference: n === 1 && !collectible ? 'CR009001' : null, activatedBillingReferenceId: n === 1 ? 'SYN-OC' : null })) }],
      history: [{ date: '2026-10-01T09:00:00', type: 'Water Bill Posted', reference: 'SYN-OC', description: 'Persisted invoice', debit: 360, credit: null, status: 'Unpaid' },
        ...(state.settledCharges ? [{ date: '2026-10-02T10:00:00', type: 'Payment', reference: 'CR009001', description: state.cancelled ? 'Persisted account payment' : 'charge 501 installment #1', debit: null, credit: 250, status: state.cancelled ? 'Cancelled' : 'Completed' }] : []),
        ...(state.cancelled ? [{ date: '2026-10-03T11:00:00', type: 'Payment Cancellation', reference: 'CR009001', description: 'Wrong amount', debit: 250, credit: null, status: 'Cancelled' }] : [])].sort((a,b) => b.date.localeCompare(a.date)) });
  }
  if (url.pathname === '/Collections/RemittanceExpected') return json({ cashOnHand: state.cashOnHand || 0 });
  if (url.pathname === '/Collections/SearchAccounts') return json([{
    accountId: 1, accountNumber: '01-010015', accountName: 'Synthetic Customer', address: 'Synthetic Street',
    unpaidCount: state.paid ? 0 : 1, otherCharges: state.otherCharges || [],
    otherChargesBalance: (state.otherCharges || []).reduce((sum, c) => sum + c.amount, 0), bills: state.paid ? [] : [state.bill]
  }]);
  if (url.pathname === '/Billing/GetChargeTypes') return json([{ value: 1, label: 'Synthetic Fee' }]);
  if (url.pathname === '/Admin/GetServiceApplications') return json([{ requestId: 1, accountNumber: '01-010015', accountName: 'Synthetic Customer', serviceName: 'Connection', stage: 'charging' }]);
  if (url.pathname === '/Billing/GetOtherChargesByAccount') return json([
    { id: 501, description: 'Installment plan', amount: 1000, payableIn: '4', collectibleAmount: 250, isPosted: true, status: 'Unpaid' },
    { id: 502, description: 'Draft plan', amount: 700, payableIn: '1', collectibleAmount: 0, isPosted: false, status: 'Draft' },
    { id: 503, description: 'Future installments', amount: 900, payableIn: '3', collectibleAmount: 0, isPosted: true, status: 'Unpaid' }
  ]);
  if (url.pathname === '/Billing/SaveOtherCharge') {
    let raw = ''; for await (const chunk of req) raw += chunk;
    state.drafts.push(JSON.parse(raw));
    assert.equal(req.headers.requestverificationtoken, 'synthetic-csrf');
    return json({ id: 501, status: 'Draft', isPosted: false });
  }
  if (url.pathname === '/Collections/PayOtherCharges') {
    let raw = ''; for await (const chunk of req) raw += chunk;
    const request = JSON.parse(raw); state.chargePayments.push(request);
    assert.equal(req.headers.requestverificationtoken, 'synthetic-csrf');
    assert.equal(Object.hasOwn(request, 'otherChargeIds'), false, 'Browser submitted the parent of an installment.');
    assert.deepEqual(request.installmentIds, [9001], 'Browser lost the authoritative installment ID.');
    assert.equal(request.amountTendered, 250);
    state.settledCharges = state.otherCharges;
    state.otherCharges = []; state.bill.otherCharges = 0; state.bill.amountDue = 397;
    return json({ success: true, invoiceNumber: 'CR009001' });
  }
  if (url.pathname === '/Collections/CancelInvoice') {
    let raw = ''; for await (const chunk of req) raw += chunk;
    const request = JSON.parse(raw);
    assert.equal(req.headers.requestverificationtoken, 'synthetic-csrf');
    assert.equal(request.invoiceNo, 'CR009001');
    if (state.cancelled) return json({ message: 'This invoice has already been cancelled.' }, 409);
    state.cancelled = true; state.otherCharges = state.settledCharges;
    state.bill.otherCharges = 250; state.bill.amountDue = 647;
    return json({ success: true });
  }
  if (url.pathname === '/Collections/Pay') {
    let raw = ''; for await (const chunk of req) raw += chunk;
    const request = JSON.parse(raw); state.requests.push(request);
    assert.equal(req.headers.requestverificationtoken, 'synthetic-csrf');
    await delay(100);
    if (state.stale) { state.paid = true; return json({ success: false, message: 'The bill balance changed or has already been paid. Refresh and try again.' }, 409); }
    if (state.paid) return json({ success: false, message: 'The bill balance changed or has already been paid. Refresh and try again.' }, 409);
    if (request.bills[0].paidAmount !== state.bill.amountDue) return json({ success: false, message: 'The bill amount changed. Reload the account.' }, 400);
    assert.equal(request.invoiceNumber, undefined, 'Browser supplied its own receipt reference.');
    assert.deepEqual(request.bills[0].installmentIds, state.bill.installmentIds || [], 'Combined payment lost its installment identities.');
    state.paid = true; state.paymentReference = 'CR000' + (500 + Number(state.bill.billingReferenceId.slice(4)));
    state.cashOnHand = request.method === 1 ? state.bill.amountDue : 0;
    return json({ success: true, paymentReference: state.paymentReference });
  }
  return json([]);
});
function command(method, params = {}) {
  const id = ++nextId;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject }); socket.send(JSON.stringify({ id, method, params }));
  });
}
async function evaluate(expression) {
  const result = await command('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
  return result.result.value;
}
async function waitFor(expression) {
  for (let i = 0; i < 100; i++) { if (await evaluate(expression)) return; await delay(50); }
  throw new Error(`Browser timed out: ${expression}`);
}
async function search() {
  await evaluate("document.getElementById('searchInput').value='01-010015'; document.getElementById('searchInput').dispatchEvent(new Event('input',{bubbles:true}));");
  await waitFor("document.querySelector('.suggestion-item') !== null");
  await evaluate("document.querySelector('.suggestion-item').click()");
}
let ledgerChecks = 0, collectionsLedgerStructure;
async function ledgerCheck(name, expression, expected) {
  assert.deepEqual(await evaluate(expression), expected, name); ++ledgerChecks;
  console.log('PASS: Account Ledger browser: ' + name);
}
async function openLedger() {
  await evaluate("document.querySelector('.link-chip[data-modal=\"ledger\"]').click()");
  await waitFor("document.getElementById('accountLedger').open && !document.querySelector('[data-ledger-content]').hidden");
}
async function closeLedger() { await evaluate("document.querySelector('#accountLedger form button').click()"); }
(async () => {
  try {
    if (!fs.existsSync(browserPath)) throw new Error('Configure TMCWD_BROWSER with an installed Chromium/Edge executable.');
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    browser = spawn(browserPath, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
      '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'], { windowsHide: true, stdio: 'ignore' });
    let port;
    for (let i = 0; i < 200; i++) {
      if (fs.existsSync(path.join(profile, 'DevToolsActivePort'))) { port = fs.readFileSync(path.join(profile, 'DevToolsActivePort'), 'utf8').split('\n')[0]; break; }
      await delay(50);
    }
    if (!port) throw new Error('Headless Edge did not start.');
    const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
    socket = new WebSocket(targets.find(t => t.type === 'page').webSocketDebuggerUrl);
    await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
    socket.onmessage = event => {
      const message = JSON.parse(event.data);
      if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails.text);
      const item = pending.get(message.id);
      if (item) { pending.delete(message.id); message.error ? item.reject(new Error(message.error.message)) : item.resolve(message.result); }
    };
    await command('Runtime.enable'); await command('Page.enable');
    const url = `http://127.0.0.1:${server.address().port}/`;
    const cases = [
      { name: 'before due', water: 170, maintenance: 20, remaining: 190, penalty: 0 },
      { name: 'after due', water: 170, maintenance: 20, remaining: 190, penalty: 17 },
      { name: 'combined current installment', water: 170, maintenance: 20, remaining: 190, penalty: 17, installment: 250 },
      { name: 'adjusted balance', water: 170, maintenance: 20, remaining: 160, penalty: 0, adjustment: 30 },
      { name: 'historical balance', water: null, maintenance: null, remaining: 80, penalty: 0 },
      { name: 'check', water: 170, maintenance: 20, remaining: 190, penalty: 0, method: 'check' },
      { name: 'online', water: 170, maintenance: 20, remaining: 190, penalty: 0, method: 'online' },
      { name: 'stale second cashier', water: 170, maintenance: 20, remaining: 190, penalty: 0, stale: true }
    ];
    for (const [index, test] of cases.entries()) {
      state = { paid: false, stale: test.stale, requests: [], bill: {
        billingReferenceId: `SYN-${index}`, serviceInvoiceNumber: test.water === null ? null : '0000001',
        billingPeriod: '2026-10', dueDate: test.penalty > 0 ? '2026-10-06' : '2026-10-21', billAmount: test.water, meterMaintenanceFee: test.maintenance,
        otherCharges: test.installment || 0, installmentIds: test.installment ? [9001] : [], billingAdjustment: test.adjustment || 0, remainingAmount: test.remaining,
        penalty: test.penalty, amountDue: test.remaining + test.penalty + (test.installment || 0)
      }};
      await command('Page.navigate', { url });
      await waitFor("document.readyState === 'complete' && document.getElementById('payBtn') !== null");
      await search(); await waitFor("document.querySelector('.bill-row') !== null");
      assert.equal(await evaluate("document.querySelector('.bill-row').children[1].textContent.trim()"), 'October 2026');
      assert.equal(await evaluate("getComputedStyle(document.querySelector('.bill-row').children[1]).whiteSpace"), 'nowrap');
      assert.doesNotMatch(await evaluate("document.querySelector('.bill-row').textContent"), /Service Invoice:|0000001|Historical/);
      assert.equal(await evaluate("document.querySelector('.bill-row').dataset.month"), '2026-10', 'Display formatting changed the underlying month.');
      assert.equal(await evaluate("document.querySelector('.bill-row').children[6].textContent.trim()"), '₱' + money(state.bill.amountDue));
      assert.equal(await evaluate("document.querySelector('.bill-row').children[3].textContent.trim()"),
        test.water === null ? '—' : '₱' + money(test.water));
      assert.equal(await evaluate("document.querySelector('.bill-row').children[4].textContent.trim()"),
        test.maintenance === null ? '—' : '₱' + money(test.maintenance));
      await evaluate(`if (!document.querySelector('.bill-row').classList.contains('selected')) document.querySelector('.bill-checkbox').click(); document.querySelector('.pay-opt[data-mode="${test.method || 'cash'}"]').click(); document.getElementById('checkNumber').value='EXTERNAL-CHECK'; document.getElementById('onlineRef').value='EXTERNAL-ONLINE';`);
      assert.equal(await evaluate("Number(document.getElementById('amountToPay').value)"), state.bill.amountDue);
      assert.ok((await evaluate("document.getElementById('sTotalDue').textContent")).endsWith(money(test.remaining + (test.installment || 0))));
      assert.ok((await evaluate("document.getElementById('sPenalty').textContent")).endsWith(money(test.penalty)));
      assert.equal(await evaluate("document.getElementById('invoiceInput').readOnly"), true);
      await evaluate(`document.getElementById('amountTendered').value='${state.bill.amountDue}'; document.getElementById('payBtn').click();`);
      await waitFor("!document.getElementById('paymentSummaryOverlay').hidden");
      await evaluate("document.getElementById('psProceedBtn').click(); document.getElementById('psProceedBtn').click();");
      await waitFor("document.getElementById('psProceedBtn').disabled");
      await waitFor("document.getElementById('paymentSummaryOverlay').hidden && !document.getElementById('psProceedBtn').disabled");
      assert.equal(state.requests.length, 1, 'Duplicate button press submitted twice.');
      assert.equal(state.requests[0].bills[0].paidAmount, state.bill.amountDue);
      if (test.stale) {
        await waitFor("document.querySelectorAll('.bill-row').length === 0");
        assert.match(await evaluate("document.getElementById('toastMsg').textContent"), /balance changed|already been paid/);
        assert.equal(state.paymentReference, undefined);
        assert.equal(await evaluate("document.getElementById('invoiceInput').value"), '');
        console.log('PASS: actual Collections browser stale second cashier: controlled conflict, refreshed balance, no receipt.');
        continue;
      }
      assert.equal(state.paymentReference, `CR000${500 + index}`);
      assert.equal(await evaluate("document.getElementById('invoiceInput').value"), state.paymentReference);
      await waitFor(`document.getElementById('tellerCash').value === '${money(test.method ? 0 : state.bill.amountDue)}'`);
      assert.equal(state.requests[0].externalReference, test.method === 'check' ? 'EXTERNAL-CHECK' : test.method === 'online' ? 'EXTERNAL-ONLINE' : null);
      assert.equal(await evaluate("document.getElementById('tellerCash').readOnly"), true);
      await evaluate("document.getElementById('resetBtn').click()");
      assert.equal(await evaluate("document.getElementById('invoiceInput').value"), '');
      await command('Page.navigate', { url });
      await waitFor("document.readyState === 'complete' && document.getElementById('payBtn') !== null");
      await search();
      assert.equal(await evaluate("document.querySelectorAll('.bill-row').length"), 0, 'Reload lost settled state.');
      console.log(`PASS: actual Collections browser ${test.name}: search, identity, breakdown, pay, duplicate press and reload.`);
    }
    state = { paid: false, drafts: [], chargePayments: [], bill: {
      billingReferenceId: 'SYN-100', billingPeriod: '2026-11', billAmount: 357, meterMaintenanceFee: 40,
      remainingAmount: 397, penalty: 0, otherCharges: 250, amountDue: 647
    }, otherCharges: [{ id: 501, installmentId: 9001, installmentNumber: 1, amount: 250,
      chargeType: 'Synthetic Fee', payableIn: '4', particulars: '<img src=x onerror="window.injected=true">' }] };
    await command('Page.navigate', { url });
    await waitFor("document.readyState === 'complete' && document.getElementById('payBtn') !== null");
    await evaluate("window.alerts=[]; window.alert=function(message){window.alerts.push(message);};");
    await search(); await waitFor("document.querySelector('.bill-row') !== null");
    assert.ok((await evaluate("document.getElementById('fOtherCharges').textContent")).endsWith('250.00'));
    assert.ok((await evaluate("document.querySelector('.bill-row').children[6].textContent")).endsWith('647.00'));
    await openLedger();
    await ledgerCheck('Collections opens one canonical dialog', "document.querySelectorAll('#accountLedger').length", 1);
    await ledgerCheck('Ledger click never opens obsolete Coming Soon', "!document.querySelector('.cs-overlay')?.classList.contains('show')", true);
    assert.equal(state.ledgerReads, 1, 'Ledger click fetched twice.');
    await ledgerCheck('three shared tabs', "[...document.querySelectorAll('[data-ledger-tab]')].map(x=>x.textContent)", ['Bills', 'Other Charges', 'History']);
    await ledgerCheck('authoritative name replaces client header', "document.querySelector('[data-header=accountName]').textContent", 'Authoritative Customer');
    await ledgerCheck('authoritative account and address', "[document.querySelector('[data-header=accountNumber]').textContent,document.querySelector('[data-header=serviceAddress]').textContent]", ['01-010015', 'Authoritative Street']);
    await ledgerCheck('unavailable rate is not fabricated', "document.querySelector('[data-header=rateCode]').textContent", '—');
    await ledgerCheck('647 live total reconciles', "document.querySelector('[data-ledger-balance]').textContent.endsWith('647.00')", true);
    await ledgerCheck('water row keeps persisted reference', "document.querySelector('[data-ledger-bills] tr').children[2].textContent", state.bill.billingReferenceId);
    await ledgerCheck('default Bills tab', "document.querySelector('[data-ledger-tab=bills]').getAttribute('aria-selected')", 'true');
    await evaluate("document.querySelector('[data-ledger-tab=otherCharges]').click()");
    await ledgerCheck('Other Charges tab switches', "document.getElementById('al-panel-otherCharges').hidden", false);
    await ledgerCheck('parent contract differs from collectible child', "document.querySelector('.account-ledger__charge').textContent.includes('1,000.00') && document.querySelector('[data-ledger-collectible]').textContent.endsWith('250.00')", true);
    await ledgerCheck('ordered four installments', "[...document.querySelectorAll('.account-ledger__charge tbody tr')].map(x=>x.children[0].textContent)", ['1','2','3','4']);
    await ledgerCheck('Draft children labeled Future', "[...document.querySelectorAll('.account-ledger__charge tbody .account-ledger__badge')].map(x=>x.textContent)", ['Unpaid','Future','Future','Future']);
    await ledgerCheck('untrusted particulars stay text', "document.querySelector('.account-ledger__charge img') === null && !window.injected && document.querySelector('.account-ledger__charge').textContent.includes('<img')", true);
    await evaluate("document.querySelector('[data-ledger-tab=history]').click()");
    await ledgerCheck('History tab shows persisted event', "!document.getElementById('al-panel-history').hidden && document.querySelector('[data-ledger-history]').textContent.includes('Water Bill Posted')", true);
    await evaluate("document.querySelector('[data-ledger-tab=bills]').focus()");
    await command('Input.dispatchKeyEvent', { type: 'keyDown', key: 'ArrowRight', code: 'ArrowRight' });
    await ledgerCheck('keyboard tab navigation', "document.activeElement.dataset.ledgerTab", 'otherCharges');
    await command('Emulation.setDeviceMetricsOverride', { width: 600, height: 800, deviceScaleFactor: 1, mobile: false });
    await ledgerCheck('narrow viewport keeps readable scrollable tables', "document.querySelector('#al-panel-otherCharges .account-ledger__table-wrap').scrollWidth > document.querySelector('#al-panel-otherCharges .account-ledger__table-wrap').clientWidth && document.getElementById('accountLedger').getBoundingClientRect().width <= 600", true);
    await command('Emulation.clearDeviceMetricsOverride');
    collectionsLedgerStructure = await evaluate("[...document.querySelectorAll('#accountLedger thead')].map(x=>x.textContent)");
    await closeLedger(); await ledgerCheck('close exits modal', "document.getElementById('accountLedger').open", false);
    await evaluate("window.openAccountLedger('MISSING')");
    await waitFor("document.querySelector('[data-ledger-message]').textContent.includes('Account not found')");
    await ledgerCheck('failed load hides stale money', "document.querySelector('[data-ledger-content]').hidden", true);
    await closeLedger();
    await evaluate("window.openAccountLedger('EMPTY')");
    await waitFor("!document.querySelector('[data-ledger-content]').hidden");
    await ledgerCheck('empty account retains header and zero balance', "document.querySelector('[data-header=accountNumber]').textContent === 'EMPTY' && document.querySelector('[data-ledger-balance]').textContent.endsWith('0.00')", true);
    await ledgerCheck('empty Bills Other Charges and History have clear states', "document.querySelector('[data-ledger-bills]').textContent.includes('No bills found for this account.') && document.querySelector('[data-ledger-charges]').textContent.includes('No Other Charges found for this account.') && document.querySelector('[data-ledger-history]').textContent.includes('No transaction history found for this account.')", true);
    await evaluate("document.querySelector('#accountLedger form button').click(); window.openAccountLedger('FAILED')");
    await waitFor("document.querySelector('[data-ledger-message]').textContent.includes('Use Refresh')");
    await ledgerCheck('genuine HTTP failure hides stale balance and offers retry', "document.querySelector('[data-ledger-content]').hidden && !document.querySelector('[data-ledger-message]').textContent.includes('Unexpected server failure')", true);
    await ledgerCheck('queued close does not abort a rapidly reopened ledger', "document.getElementById('accountLedger').open && document.getElementById('accountLedger').getAttribute('aria-busy') === 'false'", true);
    await closeLedger();
    await evaluate("document.getElementById('ocInfoBtn').click();");
    await waitFor("document.getElementById('oceChargeType').options.length > 1 && document.getElementById('oceTotalDue').textContent.includes('250.00')");
    assert.equal(await evaluate("document.getElementById('ocePayableIn').type"), 'number');
    assert.equal(await evaluate("document.getElementById('ocePayableIn').value"), '');
    assert.equal(await evaluate("document.getElementById('oceTableBody').querySelector('img')"), null, 'Untrusted particulars were interpreted as HTML.');
    for (const value of ['', '0', '-1', '1.5', 'Cash']) {
      await evaluate(`document.getElementById('oceChargeType').value='1'; document.getElementById('oceAmount').value='1000'; document.getElementById('ocePayableIn').value=${JSON.stringify(value)}; document.getElementById('oceAddBtn').click();`);
      assert.equal(state.drafts.length, 0, 'Invalid count saved a draft.');
    }
    assert.equal(await evaluate('window.alerts.length'), 5);
    await evaluate("document.getElementById('ocePayableIn').value='4'; document.getElementById('oceAddBtn').click();");
    await waitFor("!document.getElementById('oceAddBtn').disabled && window.alerts.length === 6");
    assert.equal(state.drafts.length, 1); assert.equal(state.drafts[0].payableIn, '4');
    assert.ok((await evaluate("document.getElementById('oceTotalDue').textContent")).includes('250.00'), 'Draft entered collectible total.');
    await evaluate("document.getElementById('oceClose').click(); document.querySelector('.link-chip[data-modal=\"others\"]').click();");
    await waitFor("document.getElementById('ocOthTotalDue').textContent.includes('250.00')");
    await evaluate("document.getElementById('ocOthTendered').value='250'; document.getElementById('ocOthTendered').dispatchEvent(new Event('input')); document.getElementById('ocOthPayBtn').click();");
    await waitFor("document.getElementById('ocOverlay').style.display === 'none'");
    assert.equal(state.chargePayments.length, 1);
    assert.equal(state.bill.remainingAmount, 397);
    await openLedger();
    await ledgerCheck('payment reload reduces live balance', "document.querySelector('[data-ledger-balance]').textContent.endsWith('397.00')", true);
    await ledgerCheck('paid child retains exact receipt in History', "document.querySelector('.account-ledger__charge tbody tr').textContent.includes('Paid') && document.querySelector('[data-ledger-history]').textContent.includes('CR009001') && document.querySelector('[data-ledger-history]').textContent.includes('installment #1')", true);
    await closeLedger();
    await evaluate("window.confirm=function(){return true;}; document.getElementById('cancelInvoiceBtn').click(); document.getElementById('ciInvoiceInput').value='CR009001'; document.getElementById('ciReasonSelect').value='Wrong amount'; document.getElementById('ciSubmitBtn').click();");
    await waitFor("document.getElementById('ciOverlay').style.display === 'none'");
    assert.equal(state.cancelled, true);
    assert.equal(state.bill.remainingAmount, 397);
    assert.equal(state.otherCharges.length, 1);
    await openLedger();
    await ledgerCheck('cancellation reload restores 647', "document.querySelector('[data-ledger-balance]').textContent.endsWith('647.00')", true);
    await ledgerCheck('original cancelled payment and reversal visible', "document.querySelector('[data-ledger-history]').textContent.includes('Payment Cancellation') && document.querySelector('[data-ledger-history]').textContent.includes('Cancelled') && document.querySelector('[data-ledger-history]').querySelectorAll('tr').length === 3", true);
    await closeLedger();
    console.log('PASS: actual Other Charges browser blank numeric count, validation, safe text, draft exclusion, 397 + 250 = 647, and payment by installment ID.');
    console.log('PASS: actual payment cancellation browser posts the receipt reference and restores its installment balance.');
    await command('Page.navigate', { url: url + 'dashboard' });
    await waitFor("location.pathname === '/dashboard' && document.readyState === 'complete' && document.querySelector('[data-service-category=charging]') !== null");
    await evaluate("document.querySelector('[data-service-category=charging]').click()");
    await waitFor("document.querySelector('[data-application-index]') !== null");
    await evaluate("document.querySelector('[data-application-index]').click()");
    await waitFor("document.getElementById('dashboardChargeTotal').textContent.includes('250.00')");
    assert.equal(await evaluate("document.getElementById('dashboardPayableIn').value"), '');
    assert.equal(await evaluate("document.getElementById('dashboardChargeRows').textContent.includes('1,000.00')"), true,
      'Dashboard lost the total contractual amount while displaying the child-only collectible amount.');
    console.log('PASS: actual Dashboard Charging uses the server collectible installment total; excludes draft/future contractual amounts.');
    await evaluate("document.getElementById('chargeAccountLedger').click()");
    await waitFor("document.getElementById('accountLedger').open && !document.querySelector('[data-ledger-content]').hidden");
    await ledgerCheck('Dashboard starts on Other Charges', "document.querySelector('[data-ledger-tab=otherCharges]').getAttribute('aria-selected')", 'true');
    await ledgerCheck('Dashboard and Collections use identical inner tables', "[...document.querySelectorAll('#accountLedger thead')].map(x=>x.textContent)", collectionsLedgerStructure);
    await ledgerCheck('Dashboard, Collections and ledger collectibles reconcile', "document.querySelector('[data-ledger-collectible]').textContent.endsWith('250.00') && document.getElementById('dashboardChargeTotal').textContent.includes('250.00')", true);
    await command('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
    await command('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
    await waitFor("!document.getElementById('accountLedger').open");
    await ledgerCheck('Escape closes ledger and preserves Charging modal', "document.getElementById('chargeModal').classList.contains('is-open')", true);
    console.log(`PASS: ${ledgerChecks} Account Ledger browser checks across actual Collections and Dashboard entry points.`);
    for (const [width, height] of [[1440, 900], [768, 1024], [390, 844]]) {
      for (const theme of ['light', 'dark']) {
        await command('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
        await command('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: theme }] });
        await command('Page.navigate', { url });
        await waitFor("location.pathname === '/' && document.readyState === 'complete' && document.getElementById('searchInput') !== null");
        await evaluate(`document.documentElement.setAttribute('data-theme', '${theme}'); document.documentElement.classList.toggle('dark', ${theme === 'dark'})`);
        await search();
        await openLedger();
        for (const [accountStatus, paymentStatus, accountClass, billClass, color] of [
          ['Active', 2, 'active', 'paid', 'rgb(22, 101, 52)'],
          ['Disconnected', 1, 'danger', 'danger', 'rgb(153, 27, 27)'],
          ['  aCtIvE  ', ' pAiD ', 'active', 'paid', 'rgb(22, 101, 52)'],
          ['<img src=x onerror="window.injected=true">', 'Unknown', 'default', 'default', 'rgb(71, 85, 105)']
        ]) {
          state.ledgerAccountStatus = accountStatus; state.ledgerPaymentStatus = paymentStatus;
          await evaluate("window.openAccountLedger('01-010015')");
          await waitFor(`!document.querySelector('[data-ledger-content]').hidden && document.querySelector('[data-header=status]').textContent === ${JSON.stringify(accountStatus)}`);
          assert.equal(await evaluate(`(() => {
            const header = document.querySelector('[data-header=status]');
            const bill = document.querySelector('[data-ledger-bills] .account-ledger__badge');
            const luminance = rgb => rgb.map(v => { v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4; })
              .reduce((sum, v, i) => sum + v * [.2126, .7152, .0722][i], 0);
            return header.classList.contains('account-ledger__status--${accountClass}')
              && bill.classList.contains('account-ledger__status--${billClass}')
              && bill.textContent === ${JSON.stringify(typeof paymentStatus === 'number' ? ['Draft','Unpaid','Paid'][paymentStatus] : paymentStatus)}
              && !header.querySelector('img') && !window.injected
              && [header, bill].every(node => {
                const style = getComputedStyle(node);
                if (style.color !== ${JSON.stringify(color)}) return false;
                if ('${accountClass}' === 'default') return true;
                const foreground = luminance(style.color.match(/[\\d.]+/g).slice(0, 3).map(Number));
                const background = luminance(style.backgroundColor.match(/[\\d.]+/g).slice(0, 3).map(Number));
                return (Math.max(foreground, background) + .05) / (Math.min(foreground, background) + .05) >= 4.5;
              });
          })()`), true, `Safe ${accountClass}/${billClass} badges and contrast at ${width}x${height} ${theme}`);
        }
        delete state.ledgerAccountStatus; delete state.ledgerPaymentStatus;
        assert.equal(await evaluate(`(() => {
          const dialog = document.getElementById('accountLedger').getBoundingClientRect();
          return dialog.left >= -1 && dialog.right <= ${width + 1} && dialog.top >= -1
            && dialog.bottom <= ${height + 1} && [...document.querySelectorAll('#accountLedger [role=tab]')]
              .every(tab => { const r = tab.getBoundingClientRect(); return r.width > 0 && r.left >= -1 && r.right <= ${width + 1}; });
        })()`), true, `Ledger controls fit ${width}x${height} ${theme}`);
        await closeLedger();
        console.log(`PASS: synthetic Collections/Ledger viewport ${width}x${height} ${theme}; dialog and tabs usable.`);
      }
    }
    assert.deepEqual(errors, [], 'Collections page raised browser exceptions.');
  } finally {
    if (socket?.readyState === WebSocket.OPEN) await command('Browser.close').catch(() => {});
    socket?.close(); browser?.kill(); server.close();
  }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
