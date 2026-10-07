// Actual Collections Razor body and scripts in headless Edge; synthetic HTTP only, no application database.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { spawn } = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const web = path.join(root, 'TMCWD/TMCWD.Application');
const view = fs.readFileSync(path.join(web, 'Views/Collections/Collections.cshtml'), 'utf8');
const scripts = [...view.matchAll(/<script src="~([^"?]+)[^"]*"/g)].map(m => m[1]);
const body = view.slice(view.indexOf('@Html.AntiForgeryToken()')).replace('@Html.AntiForgeryToken()', '<input name="__RequestVerificationToken" value="synthetic-csrf" type="hidden">');
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
  if (scripts.includes(url.pathname) || url.pathname === '/css/collections.css') {
    res.setHeader('Content-Type', url.pathname.endsWith('.js') ? 'text/javascript' : 'text/css');
    return res.end(fs.readFileSync(path.join(web, 'wwwroot', url.pathname)));
  }
  if (url.pathname === '/Collections/CurrentCollector') return json({ id: 7, name: 'Synthetic Cashier' });
  if (url.pathname === '/Collections/SearchAccounts') return json([{
    accountId: 1, accountNumber: 'TEST-ACCOUNT', accountName: 'Synthetic Customer', address: 'Synthetic Street',
    unpaidCount: state.paid ? 0 : 1, otherCharges: [], bills: state.paid ? [] : [state.bill]
  }]);
  if (url.pathname === '/Collections/Pay') {
    let raw = ''; for await (const chunk of req) raw += chunk;
    const request = JSON.parse(raw); state.requests.push(request);
    assert.equal(req.headers.requestverificationtoken, 'synthetic-csrf');
    await delay(100);
    if (state.paid) return json({ success: false, message: 'This payment reference has already been used.' }, 400);
    if (request.bills[0].paidAmount !== state.bill.amountDue) return json({ success: false, message: 'The bill amount changed. Reload the account.' }, 400);
    state.paid = true; state.paymentReference = request.invoiceNumber;
    return json({ success: true });
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
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
  return result.result.value;
}
async function waitFor(expression) {
  for (let i = 0; i < 100; i++) { if (await evaluate(expression)) return; await delay(50); }
  throw new Error(`Browser timed out: ${expression}`);
}
async function search() {
  await evaluate("document.getElementById('searchInput').value='TEST'; document.getElementById('searchInput').dispatchEvent(new Event('input',{bubbles:true}));");
  await waitFor("document.querySelector('.suggestion-item') !== null");
  await evaluate("document.querySelector('.suggestion-item').click()");
}
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
      { name: 'adjusted balance', water: 170, maintenance: 20, remaining: 160, penalty: 0, adjustment: 30 },
      { name: 'historical balance', water: null, maintenance: null, remaining: 80, penalty: 0 }
    ];
    for (const [index, test] of cases.entries()) {
      state = { paid: false, requests: [], bill: {
        billingReferenceId: `SYN-${index}`, serviceInvoiceNumber: test.water === null ? null : 'TEST-0000001',
        billingPeriod: '2026-10', dueDate: test.penalty > 0 ? '2026-10-06' : '2026-10-21', billAmount: test.water, meterMaintenanceFee: test.maintenance,
        otherCharges: 0, billingAdjustment: test.adjustment || 0, remainingAmount: test.remaining,
        penalty: test.penalty, amountDue: test.remaining + test.penalty
      }};
      await command('Page.navigate', { url });
      await waitFor("document.readyState === 'complete' && document.getElementById('payBtn') !== null");
      await search(); await waitFor("document.querySelector('.bill-row') !== null");
      assert.match(await evaluate("document.querySelector('.bill-row').textContent"), test.water === null ? /Historical/ : /TEST-0000001/);
      assert.equal(await evaluate("document.querySelector('.bill-row').children[3].textContent.trim()"),
        test.water === null ? '—' : '₱' + money(test.water));
      assert.equal(await evaluate("document.querySelector('.bill-row').children[4].textContent.trim()"),
        test.maintenance === null ? '—' : '₱' + money(test.maintenance));
      await evaluate("if (!document.querySelector('.bill-row').classList.contains('selected')) document.querySelector('.bill-checkbox').click(); document.querySelector('.pay-opt[data-mode=\"cash\"]').click();");
      assert.equal(await evaluate("Number(document.getElementById('amountToPay').value)"), state.bill.amountDue);
      assert.ok((await evaluate("document.getElementById('sTotalDue').textContent")).endsWith(money(test.remaining)));
      assert.ok((await evaluate("document.getElementById('sPenalty').textContent")).endsWith(money(test.penalty)));
      await evaluate(`document.getElementById('invoiceInput').value='TEST${index}'; document.getElementById('amountTendered').value='${state.bill.amountDue}'; document.getElementById('payBtn').click();`);
      await waitFor("!document.getElementById('paymentSummaryOverlay').hidden");
      await evaluate("document.getElementById('psProceedBtn').click(); document.getElementById('psProceedBtn').click();");
      await waitFor("document.getElementById('psProceedBtn').disabled");
      await waitFor("document.getElementById('paymentSummaryOverlay').hidden && !document.getElementById('psProceedBtn').disabled");
      assert.equal(state.requests.length, 1, 'Duplicate button press submitted twice.');
      assert.equal(state.requests[0].bills[0].paidAmount, state.bill.amountDue);
      assert.equal(state.paymentReference, `CR00TEST${index}`);
      await command('Page.navigate', { url });
      await waitFor("document.readyState === 'complete' && document.getElementById('payBtn') !== null");
      await search();
      assert.equal(await evaluate("document.querySelectorAll('.bill-row').length"), 0, 'Reload lost settled state.');
      console.log(`PASS: actual Collections browser ${test.name}: search, identity, breakdown, pay, duplicate press and reload.`);
    }
    assert.deepEqual(errors, [], 'Collections page raised browser exceptions.');
  } finally {
    if (socket?.readyState === WebSocket.OPEN) await command('Browser.close').catch(() => {});
    socket?.close(); browser?.kill(); server.close();
  }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
