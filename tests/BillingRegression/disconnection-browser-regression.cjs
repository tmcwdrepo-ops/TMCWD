// Actual monitoring Razor markup/scripts; synthetic HTTP only, headless Edge.
const assert = require('node:assert/strict'), fs = require('node:fs'), path = require('node:path');
const http = require('node:http'), { spawn } = require('node:child_process');
const root = path.resolve(__dirname, '../..'), web = path.join(root, 'TMCWD/TMCWD.Application');
const view = fs.readFileSync(path.join(web, 'Views/CustomerSupport/DisconnectionMonitoring.cshtml'), 'utf8');
const partial = fs.readFileSync(path.join(web, 'Views/Shared/_AccountLedger.cshtml'), 'utf8').replaceAll('="~/', '="/');
const body = view.slice(view.indexOf('<section'), view.indexOf('@section Scripts')).replace('<partial name="_AccountLedger" />', partial)
  .replace('@Html.AntiForgeryToken()', '<input name="__RequestVerificationToken" value="synthetic-csrf" type="hidden">')
  .replaceAll('@((AuthenticatedUser.User.Role == (int)UserRole.CustomerServiceOic || AuthenticatedUser.User.Role == (int)UserRole.SuperAdmin) ? "true" : "false")', '__CAN_DISCONNECT__');
const row = { accountNumber: '01-010010', accountName: '<img src=x onerror="window.injected=true">', serviceAddress: 'Synthetic street', customerId: 1,
  firstBillingReference: 'FIRST', firstBillingDate: '2026-10-06', secondBillingReference: 'SECOND', secondBillingDate: '2026-11-08',
  subjectDate: '2026-11-08', scheduledDisconnectionDate: '2026-11-13', daysRemaining: 0, status: 'FOR DISCONNECTION',
  outstandingWaterBalance: 397, activePenaltyBalance: 17, collectibleOtherCharges: 250, reconnectionFeeBalance: 350, totalCollectibleBalance: 1014 };
let rows = [row, { ...row, accountNumber: '01-010003', status: 'SUBJECT FOR DISCONNECTION', daysRemaining: 4 }], failed = false, latency = 0;
let canConfirm = false, uncertainOnce = false, confirmationStatus = 200, confirmationLatency = 150;
let holdConfirmation = false, releaseConfirmation;
const confirmations = [];
const reconnections = [];
const eligibleRow = { ...row, eventId: '11111111-1111-4111-8111-111111111111', status:'ELIGIBLE FOR RECONNECTION',
  operationalAccountStatus:'Suspended', physicalServiceStatus:'DISCONNECTED', actualDisconnectionDateUtc:'2026-11-13T02:00:00Z',
  outstandingWaterBalance:0,activePenaltyBalance:0,collectibleOtherCharges:0,reconnectionFeeBalance:0,totalCollectibleBalance:0 };
let browser, socket, nextId = 0;
const pending = new Map(), reads = [], errors = [], delay = ms => new Promise(r => setTimeout(r, ms));
const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://localhost');
  const json = (value, status = 200) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(value)); };
  if (url.pathname === '/') return res.end(`<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><link rel="stylesheet" href="/css/appshell.css"><link rel="stylesheet" href="/css/disconnection-monitoring.css"></head><body><main class="content"><div class="content__header"><h1 class="content__title" id="pageTitle">Disconnection Monitoring</h1></div><div class="content__body">${body.replaceAll('__CAN_DISCONNECT__', String(canConfirm))}</div></main><script src="/js/disconnection-monitoring.js"></script></body></html>`);
  if (url.pathname === '/CustomerSupport/DisconnectionMonitoringData') {
    reads.push(url.href);
    return setTimeout(() => failed ? json({}, 500) : json(rows), latency);
  }
  if (url.pathname === '/Billing/AccountLedgerData') {
    reads.push(url.pathname + url.search);
    const selected = rows.find(r => r.accountNumber === url.searchParams.get('accountNumber'));
    const disconnected = selected?.actualDisconnectionDateUtc && !selected?.actualReconnectionDateUtc;
    return json({ account: { accountNumber: url.searchParams.get('accountNumber'), status: disconnected ? 'Disconnected' : 'Active',
      operationalAccountStatus: disconnected ? 'Suspended' : 'Active' }, bills: [], otherCharges: [], history: [], totalBalance: 0, collectibleOtherCharges: 0 });
  }
  if (url.pathname === '/CustomerSupport/ConfirmDisconnection') {
    assert.equal(req.method, 'POST'); assert.equal(req.headers.requestverificationtoken, 'synthetic-csrf');
    assert.equal(url.searchParams.get('accountNumber'), '01-010010');
    let raw = ''; for await (const chunk of req) raw += chunk;
    const payload = JSON.parse(raw); assert.deepEqual(Object.keys(payload), ['operationId']);
    confirmations.push(payload);
    if (holdConfirmation) await new Promise(resolve => { releaseConfirmation = resolve; });
    await delay(confirmationLatency);
    if (uncertainOnce) { uncertainOnce = false; return json({}, 500); }
    if (confirmationStatus !== 200) return json({}, confirmationStatus);
    rows = [{ ...row, eventId: payload.operationId, status: 'BALANCE + RECONNECTION FEE PENDING', physicalServiceStatus: 'DISCONNECTED', actualDisconnectionDateUtc: '2026-11-13T02:00:00Z' }];
    return json({ eventId: payload.operationId });
  }
  if (url.pathname === '/CustomerSupport/ConfirmReconnection') {
    assert.equal(req.method,'POST'); assert.equal(req.headers.requestverificationtoken,'synthetic-csrf');
    assert.equal(url.searchParams.get('accountNumber'),eligibleRow.accountNumber);
    let raw=''; for await(const chunk of req) raw+=chunk;
    const payload=JSON.parse(raw); assert.deepEqual(Object.keys(payload),['operationId']); reconnections.push(payload);
    await delay(150);
    if(uncertainOnce){uncertainOnce=false; return json({},500);}
    rows=[{...rows[0],status:'RECONNECTED',operationalAccountStatus:'Active',physicalServiceStatus:'RECONNECTED',actualReconnectionDateUtc:'2026-11-14T02:00:00Z'}];
    return json({eventId:rows[0].eventId,actualReconnectionDateUtc:rows[0].actualReconnectionDateUtc});
  }
  if (/^\/(js|css)\/[a-z-]+\.(js|css)$/.test(url.pathname)) {
    res.setHeader('Content-Type', url.pathname.endsWith('.js') ? 'text/javascript' : 'text/css');
    return res.end(fs.readFileSync(path.join(web, 'wwwroot', url.pathname)));
  }
  res.writeHead(404); res.end();
});
function command(method, params = {}) {
  const id = ++nextId;
  return new Promise((resolve, reject) => { pending.set(id, { resolve, reject }); socket.send(JSON.stringify({ id, method, params })); });
}
async function evaluate(expression) {
  const r = await command('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text);
  return r.result.value;
}
async function waitFor(expression) {
  for (let i = 0; i < 100; ++i) { if (await evaluate(expression)) return; await delay(50); }
  throw new Error('Timeout: ' + expression);
}
async function pressKey(value, shift = false) {
  const params = { key: value, code: value, windowsVirtualKeyCode: value === 'Escape' ? 27 : 9, modifiers: shift ? 8 : 0 };
  await command('Input.dispatchKeyEvent', { type: 'keyDown', ...params });
  await command('Input.dispatchKeyEvent', { type: 'keyUp', ...params });
}
async function badgeContrast() {
  return evaluate(`(() => {
    const luminance = color => color.match(/[\\d.]+/g).slice(0, 3).map(Number).map(v => {
      v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4;
    }).reduce((sum, v, i) => sum + v * [.2126, .7152, .0722][i], 0);
    return [...document.querySelectorAll('.dm-status, .dm-button--danger, .dm-warning')].every(el => {
      const style = getComputedStyle(el), a = luminance(style.color), b = luminance(style.backgroundColor);
      return (Math.max(a, b) + .05) / (Math.min(a, b) + .05) >= 4.5;
    });
  })()`);
}
(async () => {
  try {
    const profile = fs.mkdtempSync(path.join(root, '.tmp/disconnection-browser-'));
    await new Promise(r => server.listen(0, '127.0.0.1', r));
    browser = spawn(process.env.TMCWD_BROWSER || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
      ['--headless=new', '--disable-gpu', '--no-first-run', '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'], { windowsHide: true, stdio: 'ignore' });
    let port;
    for (let i = 0; i < 200; ++i) { if (fs.existsSync(path.join(profile, 'DevToolsActivePort'))) { port = fs.readFileSync(path.join(profile, 'DevToolsActivePort'), 'utf8').split('\n')[0]; break; } await delay(50); }
    if (!port) throw new Error('Headless Edge did not start.');
    const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
    socket = new WebSocket(targets.find(t => t.type === 'page').webSocketDebuggerUrl);
    await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
    socket.onmessage = e => {
      const m = JSON.parse(e.data);
      if (m.method === 'Runtime.exceptionThrown') errors.push(m.params.exceptionDetails.text);
      const p = pending.get(m.id); if (p) { pending.delete(m.id); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
    };
    await command('Runtime.enable'); await command('Page.enable');
    const address = `http://127.0.0.1:${server.address().port}/`;
    for (const [width, height] of [[1440, 900], [768, 1024], [390, 844]]) for (const theme of ['light', 'dark']) {
      latency = 250;
      await command('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
      await command('Page.navigate', { url: address });
      await waitFor("document.getElementById('dmResults')?.getAttribute('aria-busy') === 'true'");
      assert.equal(await evaluate("!document.getElementById('dmLoading').hidden"), true, 'Missing loading indicator');
      await evaluate(`document.documentElement.setAttribute('data-theme','${theme}')`);
      await waitFor("document.querySelectorAll('#dmRows tr').length === 2");
      assert.equal(await evaluate("document.getElementById('dmResults').getAttribute('aria-busy')"), 'false');
      assert.match(await evaluate("document.getElementById('dmRows').textContent"), /Nov 13, 2026/);
      assert.equal(await evaluate("document.querySelector('#dmRows .dm-schedule').querySelectorAll('dd')[2].textContent"), '0');
      assert.equal(await evaluate("document.querySelectorAll('h1').length"), 1, 'Duplicate page heading');
      assert.equal(await evaluate("!document.getElementById('dmLoading').hidden"), false);
      assert.deepEqual(await evaluate("[...document.querySelectorAll('[data-dm-count]')].map(el => el.textContent)"), ['2','0','1','1','0','0']);
      assert.deepEqual(await evaluate("[...document.querySelector('#dmRows .dm-balance').querySelectorAll('dt')].map(el => el.textContent)"), ['Water','Penalty','Other Charges','Reconnection','Total']);
      assert.match(await evaluate("document.querySelector('#dmRows .dm-balance-total dd').textContent"), /1,014\.00/);
      assert.equal(await evaluate("document.querySelector('#dmRows .dm-reference').textContent"), 'Ref: FIRST');
      assert.equal(await evaluate("document.querySelector('#dmRows .dm-status').classList.contains('dm-status--danger')"), true);
      assert.equal(await evaluate("document.querySelectorAll('#dmRows .dm-status')[1].classList.contains('dm-status--subject')"), true);
      assert.equal(await badgeContrast(), true, 'Status contrast below WCAG AA');
      if (width === 390) {
        assert.equal(await evaluate("getComputedStyle(document.querySelector('#dmRows tr')).display"), 'flex', 'Mobile forced desktop table');
        assert.equal(await evaluate("document.querySelector('#dmRows .dm-status').getBoundingClientRect().top < document.querySelector('#dmRows .dm-schedule').getBoundingClientRect().top"), true, 'Mobile status is not near the account');
      }
      assert.equal(await evaluate("document.querySelectorAll('#dmRows img').length"), 0);
      assert.equal(await evaluate("!!window.injected"), false);
      assert.equal(await evaluate(`document.documentElement.scrollWidth <= ${width}`), true,
        'Page overflow: ' + JSON.stringify(await evaluate(`Array.from(document.querySelectorAll('section,form,div,label,select,input')).map(e=>({tag:e.tagName,id:e.id,cls:e.className,right:e.getBoundingClientRect().right,width:e.getBoundingClientRect().width})).filter(e=>e.right>${width})`)));
      assert.match(await evaluate("document.querySelector('#dmRows a').href"), /accountNumber=01-010010/);
      fs.writeFileSync(path.join(profile, `monitoring-${width}-${theme}.png`), Buffer.from((await command('Page.captureScreenshot')).data, 'base64'));
      await evaluate("document.querySelector('#dmRows button').click(); document.querySelector('#dmRows button').click()");
      await waitFor("document.getElementById('accountLedger').open && !document.querySelector('[data-ledger-content]').hidden");
      assert.equal(reads.at(-1), '/Billing/AccountLedgerData?accountNumber=01-010010');
      assert.match(await evaluate("document.querySelector('[data-ledger-content]').textContent"), /01-010010/);
      assert.equal(await evaluate("document.querySelectorAll('button[data-dc-action]').length"), 0, 'Quarantined mutation buttons exposed');
      assert.equal(await evaluate("document.querySelectorAll('[data-disconnection-confirm]').length"), 0, 'Non-CS OIC confirmation button exposed');
      await evaluate("document.getElementById('accountLedger').close(); document.querySelectorAll('#dmRows tr')[1].querySelector('button').click()");
      await waitFor("!document.querySelector('[data-ledger-content]').hidden");
      assert.equal(reads.at(-1), '/Billing/AccountLedgerData?accountNumber=01-010003');
      await evaluate("document.getElementById('accountLedger').close(); document.getElementById('dmStatus').value='FOR DISCONNECTION'; document.getElementById('dmStatus').dispatchEvent(new Event('change'))");
      assert.equal(await evaluate("document.querySelectorAll('#dmRows tr').length"), 1);
      console.log(`PASS: Phase 6J monitoring ${width}x${height} ${theme}: loading, dates, safe text, account/Ledger isolation, status filter, no physical buttons.`);
    }
    latency = 0; rows = [];
    await evaluate("document.getElementById('dmFilters').requestSubmit()");
    await waitFor("document.getElementById('dmMessage').textContent === 'No accounts match these filters.'");
    assert.equal(await evaluate("!document.getElementById('dmEmpty').hidden && document.getElementById('dmResults').hidden"), true);
    failed = true;
    await evaluate("document.getElementById('dmFilters').requestSubmit()");
    await waitFor("document.getElementById('dmMessage').textContent.includes('Unable to load')");
    assert.equal(await evaluate("!document.getElementById('dmError').hidden"), true);
    failed = false; rows = [row];
    await evaluate("document.getElementById('dmRetry').click()");
    await waitFor("document.querySelectorAll('#dmRows tr').length === 1");
    await evaluate("document.getElementById('dmQuery').value='01-010010'; document.getElementById('dmZone').value='1'; document.getElementById('dmBook').value='2'; document.getElementById('dmFilters').requestSubmit()");
    await waitFor("document.querySelectorAll('#dmRows tr').length === 1");
    const request = new URL(reads.at(-1));
    assert.equal(request.searchParams.get('q'), '01-010010'); assert.equal(request.searchParams.get('zone'), '1'); assert.equal(request.searchParams.get('book'), '2');
    await evaluate("document.querySelector('[data-dm-clear]').click()");
    await waitFor("document.getElementById('dmResults').getAttribute('aria-busy') === 'false'");
    assert.deepEqual(await evaluate("['dmQuery','dmZone','dmBook','dmStatus'].map(id=>document.getElementById(id).value)"), ['','','','']);
    assert.equal(new URL(reads.at(-1)).searchParams.has('zone'), false);
    console.log('PASS: Phase 6J empty/error/retry and account/zone/book filters.');
    const statusCases = [
      ['CURRENT','current'], ['FIRST BILL UNPAID','warning'], ['SUBJECT FOR DISCONNECTION','subject'],
      ['FOR DISCONNECTION','danger'], ['DISCONNECTED','danger'], ['BALANCE + RECONNECTION FEE PENDING','warning'],
      ['ELIGIBLE FOR RECONNECTION','success'], ['RECONNECTED','success'],
      ['FINANCIAL / SERVICE STATE REVIEW REQUIRED','review'], ['<svg onload="window.injected=true">','current']
    ];
    rows = statusCases.map(([status], i) => ({ ...row, status, accountNumber: 'STATUS-' + i,
      ...(status === 'DISCONNECTED' ? { physicalServiceStatus: 'DISCONNECTED', actualDisconnectionDateUtc: '2026-11-13T02:00:00Z' } : {}) }));
    for (const [width, height] of [[1440, 900], [768, 1024], [390, 844]]) for (const theme of ['light','dark']) {
      await command('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
      await command('Page.navigate', { url: address });
      await waitFor("document.querySelectorAll('#dmRows tr').length === 10");
      await evaluate(`document.documentElement.setAttribute('data-theme','${theme}')`);
      assert.deepEqual(await evaluate("[...document.querySelectorAll('#dmRows tr')].map(r=>[r.querySelector('.dm-status').textContent, [...r.querySelector('.dm-status').classList].find(c=>c.startsWith('dm-status--')).slice(11)])"), statusCases);
      assert.deepEqual(await evaluate("[...document.querySelectorAll('[data-dm-count]')].map(el => el.textContent)"), ['10','1','1','1','1','1']);
      assert.equal(await badgeContrast(), true, 'Full status hierarchy contrast below WCAG AA');
      assert.equal(await evaluate("document.querySelectorAll('#dmRows img, #dmRows svg').length"), 0);
      assert.equal(await evaluate("document.querySelector('#dmRows .dm-actions button').className"), 'dm-button');
      assert.equal(await evaluate(`document.documentElement.scrollWidth <= ${width}`), true);
      await evaluate("document.getElementById('dmStatus').value='DISCONNECTED'; document.getElementById('dmStatus').dispatchEvent(new Event('change'))");
      assert.equal(await evaluate("document.querySelectorAll('#dmRows tr').length"), 1);
      console.log(`PASS: full monitoring badge hierarchy, summary, balances, safe actions and AA contrast ${width}x${height} ${theme}.`);
    }
    for (const [role, allowed] of [['CS OIC', true], ['SuperAdmin', true], ['Cashier', false], ['Customer Representative', false], ['Meter Reader', false]]) {
      canConfirm = allowed;
      const roleStates = ['FOR DISCONNECTION', 'CURRENT', 'FIRST BILL UNPAID', 'SUBJECT FOR DISCONNECTION',
        'DISCONNECTED', 'ELIGIBLE FOR RECONNECTION', 'RECONNECTED', 'BALANCE + RECONNECTION FEE PENDING', 'FINANCIAL / SERVICE STATE REVIEW REQUIRED'];
      rows = roleStates.map((status, i) => ({ ...row, status, accountNumber: 'ROLE-' + i }));
      await command('Page.navigate', { url: address });
      await waitFor("document.querySelectorAll('#dmRows tr').length === 9");
      assert.deepEqual(await evaluate("Array.from(document.querySelectorAll('#dmRows tr')).map(r => r.querySelectorAll('[data-disconnection-confirm]').length)"), [allowed ? 1 : 0, ...Array(8).fill(0)]);
      assert.deepEqual(await evaluate("Array.from(document.querySelectorAll('#dmRows tr')).map(r => r.querySelectorAll('[data-reconnection-confirm]').length)"), [0,0,0,0,0,allowed?1:0,0,0,0]);
      console.log(`PASS: ${role} button visibility for FOR DISCONNECTION versus Current/First/Subject states.`);
    }
    canConfirm = true; rows = [row];
    for (const [width, height] of [[1440, 900], [768, 1024], [390, 844]]) for (const theme of ['light', 'dark']) {
      await command('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
      await command('Page.navigate', { url: address });
      await waitFor("document.querySelector('[data-disconnection-confirm]') !== null");
      await evaluate(`document.documentElement.setAttribute('data-theme','${theme}'); document.querySelector('[data-disconnection-confirm]').click()`);
      assert.equal(await evaluate("document.getElementById('dmConfirmation').open"), true);
      assert.equal(await evaluate("document.activeElement.id"), 'dmCancel');
      assert.equal(await evaluate(`(() => { const r=document.getElementById('dmConfirmation').getBoundingClientRect(); return r.left>=0 && r.right<=${width} && r.top>=0 && r.bottom<=${height}; })()`), true, 'Physical confirmation dialog does not fit viewport');
      assert.equal(await evaluate(`Math.abs(document.getElementById('dmConfirmation').getBoundingClientRect().left + document.getElementById('dmConfirmation').getBoundingClientRect().width / 2 - ${width / 2}) < 2`), true, 'Modal is not centered');
      assert.equal(await evaluate("getComputedStyle(document.getElementById('dmConfirmation'), '::backdrop').backgroundColor"), 'rgba(10, 18, 32, 0.7)');
      assert.equal(await evaluate("document.getElementById('dmConfirmationAccount').textContent"), row.accountNumber);
      assert.equal(await evaluate("document.getElementById('dmConfirmationCustomer').textContent"), row.accountName);
      assert.equal(await evaluate("document.querySelectorAll('#dmConfirmation img').length"), 0);
      assert.match(await evaluate("document.getElementById('dmConfirmationWarning').textContent"), /PHP 350 Reconnection Fee/);
      assert.match(await evaluate("document.getElementById('dmConfirmationWarning').textContent"), /does not mean the balance is paid/);
      assert.equal(await evaluate("document.getElementById('dmConfirm').classList.contains('dm-button--danger')"), true);
      assert.equal(await badgeContrast(), true, 'Modal/badge contrast below WCAG AA');
      fs.writeFileSync(path.join(profile, `confirmation-${width}-${theme}.png`), Buffer.from((await command('Page.captureScreenshot')).data, 'base64'));
      await pressKey('Tab'); assert.equal(await evaluate("document.activeElement.id"), 'dmConfirm');
      await pressKey('Tab'); assert.equal(await evaluate("document.activeElement.id"), 'dmCancel', 'Modal focus escaped');
      await pressKey('Tab', true); assert.equal(await evaluate("document.activeElement.id"), 'dmConfirm');
      await evaluate("document.getElementById('dmCancel').click()"); assert.equal(confirmations.length, 0, 'Opening/cancelling the dialog submitted physical state');
      await waitFor("document.activeElement.hasAttribute('data-disconnection-confirm')");
      await evaluate("document.querySelector('[data-disconnection-confirm]').click()");
      await pressKey('Escape'); await waitFor("!document.getElementById('dmConfirmation').open");
      await waitFor("document.activeElement.hasAttribute('data-disconnection-confirm')");
      console.log(`PASS: CS OIC confirmation dialog ${width}x${height} ${theme}: fits, Cancel focus, no automatic confirmation.`);
    }
    canConfirm = true; rows = [row]; uncertainOnce = true; holdConfirmation = true;
    await command('Page.navigate', { url: address });
    await waitFor("document.querySelector('[data-disconnection-confirm]') !== null");
    await evaluate("document.querySelector('[data-disconnection-confirm]').click(); document.getElementById('dmConfirm').click(); document.getElementById('dmConfirm').click()");
    assert.equal(await evaluate("document.getElementById('dmConfirm').disabled && document.getElementById('dmCancel').disabled && document.getElementById('dmConfirm').textContent === 'Confirming…'"), true);
    await pressKey('Escape');
    assert.equal(await evaluate("document.getElementById('dmConfirmation').open"), true, 'In-flight modal closed on Escape');
    for (let i = 0; !releaseConfirmation && i < 100; ++i) await delay(10);
    assert.ok(releaseConfirmation, 'Confirmation request did not reach held HTTP fixture');
    holdConfirmation = false; releaseConfirmation();
    await waitFor("document.getElementById('dmConfirmationMessage').textContent.includes('uncertain')");
    assert.equal(confirmations.length, 1, 'Double click posted multiple physical events');
    const key = confirmations[0].operationId;
    await command('Page.navigate', { url: address });
    await waitFor("document.querySelector('[data-disconnection-confirm]') !== null");
    await evaluate("document.querySelector('[data-disconnection-confirm]').click(); document.getElementById('dmConfirm').click()");
    await waitFor("document.getElementById('dmRows').textContent.includes('BALANCE + RECONNECTION FEE PENDING')");
    assert.equal(confirmations.length, 2); assert.equal(confirmations[1].operationId, key, 'Reloaded retry changed operation identity');
    assert.equal(await evaluate("sessionStorage.getItem('tmcwd-disconnection:01-010010')"), null);
    await waitFor("!document.getElementById('dmNotice').hidden");
    assert.match(await evaluate("document.getElementById('dmNotice').textContent"), /Physical disconnection confirmed.*350\.00.*Disconnected/);
    assert.equal(await evaluate("document.querySelectorAll('[data-disconnection-confirm]').length"), 0, 'Already disconnected account still offered confirmation');
    await evaluate("document.querySelector('#dmRows button').click()");
    await waitFor("document.getElementById('accountLedger').open && !document.querySelector('[data-ledger-content]').hidden");
    assert.match(await evaluate("document.querySelector('[data-ledger-content]').textContent"), /Disconnected/);
    await evaluate("document.getElementById('accountLedger').close()");
    console.log('PASS: CS OIC confirmation dialog, CSRF, double-submit guard, uncertain reload/retry identity and successful acknowledgement.');
    rows = [{ ...row, eventId: key, actualDisconnectionDateUtc: '2026-11-13T02:00:00Z', actualReconnectionDateUtc: '2026-11-14T02:00:00Z', physicalServiceStatus: 'RECONNECTED' }];
    await evaluate(`sessionStorage.setItem('tmcwd-disconnection:01-010010', '${key}')`);
    await command('Page.navigate', { url: address });
    await waitFor("document.querySelector('[data-disconnection-confirm]') !== null");
    assert.equal(await evaluate("sessionStorage.getItem('tmcwd-disconnection:01-010010')"), null, 'Known old event did not acknowledge its lost response');
    await evaluate("document.querySelector('[data-disconnection-confirm]').click(); document.getElementById('dmConfirm').click()");
    await waitFor("document.getElementById('dmRows').textContent.includes('BALANCE + RECONNECTION FEE PENDING')");
    assert.equal(confirmations.length, 3); assert.notEqual(confirmations[2].operationId, key, 'Later physical event reused old event identity');
    console.log('PASS: authoritative event acknowledges a lost response; a later cycle receives a new operation identity.');
    rows = [row]; await command('Page.navigate', { url: address });
    await waitFor("document.querySelector('[data-disconnection-confirm]') !== null");
    await evaluate("Storage.prototype.setItem = () => { throw new Error('Retry storage unavailable'); }; document.querySelector('[data-disconnection-confirm]').click(); document.getElementById('dmConfirm').click()");
    await waitFor("document.getElementById('dmConfirmationMessage').textContent.includes('unavailable')");
    assert.equal(confirmations.length, 3, 'Storage failure sent an unidentifiable confirmation');
    console.log('PASS: unavailable retry storage fails closed before physical confirmation.');
    for (const status of [409, 401, 403]) {
      confirmationStatus = status; rows = [row];
      await command('Page.navigate', { url: address });
      await waitFor("document.querySelector('[data-disconnection-confirm]') !== null");
      const previous = confirmations.length;
      await evaluate("document.querySelector('[data-disconnection-confirm]').click(); document.getElementById('dmConfirm').click()");
      await waitFor("!document.getElementById('dmConfirm').disabled");
      assert.equal(confirmations.length, previous + 1);
      assert.match(await evaluate("document.getElementById('dmConfirmationMessage').textContent"), status === 409 ? /no longer eligible.*already confirmed/ : /does not have permission/);
      assert.equal(await evaluate("document.getElementById('dmConfirmation').open"), true);
      assert.equal(await evaluate("sessionStorage.getItem('tmcwd-disconnection:01-010010')"), confirmations.at(-1).operationId);
      console.log(`PASS: ${status} confirmation error stays visible in dialog and retains retry identity.`);
    }
    for(const [width,height] of [[1440,900],[768,1024],[390,844]]) for(const theme of ['light','dark']) {
      canConfirm=true; rows=[eligibleRow];
      await command('Emulation.setDeviceMetricsOverride',{width,height,deviceScaleFactor:1,mobile:false});
      await command('Page.navigate',{url:address}); await waitFor("document.querySelector('[data-reconnection-confirm]') !== null");
      await evaluate(`document.documentElement.setAttribute('data-theme','${theme}'); document.querySelector('[data-reconnection-confirm]').click()`);
      assert.equal(await evaluate("document.activeElement.id"),'dmCancel');
      assert.equal(await evaluate("document.getElementById('dmConfirmationTitle').textContent"),'Confirm Physical Reconnection');
      assert.equal(await evaluate("document.getElementById('dmConfirmationBalance').textContent"),'₱0.00');
      assert.equal(await evaluate("document.getElementById('dmReconnectionFee').hidden"),false);
      assert.match(await evaluate("document.getElementById('dmConfirmationWarning').textContent"),/physically reconnected.*Restore the account to Active/s);
      assert.equal(await evaluate(`(()=>{const r=document.getElementById('dmConfirmation').getBoundingClientRect();return r.left>=0&&r.right<=${width}&&r.top>=0&&r.bottom<=${height};})()`),true);
      await pressKey('Tab'); await pressKey('Tab'); assert.equal(await evaluate("document.activeElement.id"),'dmCancel');
      await evaluate("document.getElementById('dmCancel').click()");
      assert.equal(reconnections.length,0);
      console.log(`PASS: physical reconnection dialog ${width}x${height} ${theme}: eligible-only action, zero balance, paid fee, field warning, Cancel focus/trap, no automatic submit.`);
    }
    rows=[eligibleRow]; uncertainOnce=true;
    await command('Page.navigate',{url:address}); await waitFor("document.querySelector('[data-reconnection-confirm]') !== null");
    await evaluate("document.querySelector('[data-reconnection-confirm]').click(); document.getElementById('dmConfirm').click(); document.getElementById('dmConfirm').click()");
    await waitFor("document.getElementById('dmConfirmationMessage').textContent.includes('uncertain')");
    assert.equal(reconnections.length,1); const reconnectKey=reconnections[0].operationId;
    await command('Page.navigate',{url:address}); await waitFor("document.querySelector('[data-reconnection-confirm]') !== null");
    await evaluate("document.querySelector('[data-reconnection-confirm]').click(); document.getElementById('dmConfirm').click()");
    await waitFor("document.getElementById('dmNotice').textContent.includes('Physical reconnection confirmed')");
    assert.equal(reconnections[1].operationId,reconnectKey);
    assert.equal(await evaluate("document.querySelectorAll('[data-reconnection-confirm]').length"),0);
    assert.equal(await evaluate(`sessionStorage.getItem('tmcwd-reconnection:${eligibleRow.accountNumber}:${eligibleRow.eventId}')`),null);
    await evaluate("document.querySelector('#dmRows button').click()");
    await waitFor("document.getElementById('accountLedger').open && !document.querySelector('[data-ledger-content]').hidden");
    assert.equal(await evaluate("document.querySelector('[data-header=status]').textContent"),'Active');
    console.log('PASS: physical reconnection double-submit, uncertain reload/retry key, success refresh and shared Ledger Active.');
    await evaluate(`sessionStorage.setItem('tmcwd-reconnection:${eligibleRow.accountNumber}:${eligibleRow.eventId}','${reconnectKey}')`);
    await command('Page.navigate',{url:address});
    await waitFor("document.getElementById('dmRows').textContent.includes('RECONNECTED')");
    assert.equal(await evaluate(`sessionStorage.getItem('tmcwd-reconnection:${eligibleRow.accountNumber}:${eligibleRow.eventId}')`),null);
    assert.equal(await evaluate("document.querySelectorAll('[data-reconnection-confirm]').length"),0);
    console.log('PASS: authoritative completed-event read acknowledges a lost reconnection response without another POST.');
    rows=[{...eligibleRow,eventId:'22222222-2222-4222-8222-222222222222'}];
    await command('Page.navigate',{url:address}); await waitFor("document.querySelector('[data-reconnection-confirm]') !== null");
    await evaluate("document.querySelector('[data-reconnection-confirm]').click(); document.getElementById('dmConfirm').click()");
    await waitFor("document.getElementById('dmNotice').textContent.includes('Physical reconnection confirmed')");
    assert.notEqual(reconnections[2].operationId,reconnectKey,'Later event reused reconnection identity');
    console.log('PASS: reconnection retry identity is scoped to the exact disconnection event; later cycle gets a new GUID.');
    assert.deepEqual(errors, []);
    console.log('PASS: monitoring redesign browser regression complete; screenshots: ' + profile);
  } finally {
    if (socket?.readyState === WebSocket.OPEN) await command('Browser.close').catch(() => {});
    socket?.close(); browser?.kill(); server.close();
  }
})().catch(e => { console.error(e); process.exitCode = 1; });
