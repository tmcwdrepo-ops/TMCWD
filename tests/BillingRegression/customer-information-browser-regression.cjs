// Real Customer Information script and shared Ledger in headless Edge; synthetic HTTP only.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { spawn } = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const web = path.join(root, 'TMCWD/TMCWD.Application');
const view = fs.readFileSync(path.join(web, 'Views/CustomerSupport/CustomerInformation.cshtml'), 'utf8');
const source = view.slice(view.indexOf('<script>') + 8, view.lastIndexOf('</script>'));
const partial = fs.readFileSync(path.join(web, 'Views/Shared/_AccountLedger.cshtml'), 'utf8').replaceAll('="~/', '="/');
const accounts = [{ id: 3, accountNumber: '01-010003', meterNumber: 'M3', zoneBookId: 20, status: 3, classification: 1, meterSize: .5, street: 'Account A street', initialReading: 0 },
  { id: 10, accountNumber: '01-010010', meterNumber: 'M10', zoneBookId: 25, status: 3, classification: 4, meterSize: .75, street: 'Account B street', initialReading: 10 }];
const zoneBooks = [{ id: 20, zone: 5, book: 4, area: 'RV2K P1, Inocencio', week: 1 }, { id: 25, zone: 6, book: 4, area: 'Different area', week: 2 }];
const customerData = { id: 1, fullName: 'Same Synthetic Customer', email: '', phoneNumber: '09123456789' };
const reads = [], statusReads = [], adjustmentReads = [], customerSaves = [], accountSaves = [], errors = [], pending = new Map();
const delay = ms => new Promise(r => setTimeout(r, ms));
let browser, socket, sequence = 0, failLoad = false, loadDelays = [], statusDelays = [], adjustmentDelays = [], statusOverrides = {}, failMonitoring = false, failFinancial = false;
const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://localhost');
  const json = value => { res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify(value)); };
  if (url.pathname === '/' || url.pathname === '/CustomerSupport/CustomerInformation') {
    const customer = Number(url.searchParams.get('customerId') || 1);
    const number = customer === 1 ? accounts[0].accountNumber : customer === 2 ? accounts[1].accountNumber : '';
    const script = source.replace('@(Model.Customer.Id)', String(customer)).replace('@(currentAcct?.Id ?? 0)', customer === 1 ? '3' : '10')
      .replace('@Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.CustomerIds))', '[1,2,3]')
      .replaceAll('@(currentAcct?.AccountNumber ?? string.Empty)', number)
      .replace(/const SEED = .*;/, () => 'const SEED = ' + JSON.stringify({ customer: { ...customerData, id: customer }, accounts,
        selectedAccount: accounts.find(a=>a.accountNumber===number) || null }).replaceAll('<', '\\u003c') + ';')
      .replace(/@Url.Action\("([^"]+)",\s*"([^"]+)"\)/g, '/$2/$1')
      // Exercise existing internal direct-selection/empty paths without production test hooks.
      .replace('})();', 'window.testSelection = { switchToAccount, populatePage }; })();');
    const elements = view.slice(view.indexOf('<div class="ci-page">'), view.indexOf('@section Styles'))
      .replace(/@\*[\s\S]*?\*@/g, '')
      .replace('@Html.AntiForgeryToken()', '<input type="hidden" name="__RequestVerificationToken" value="synthetic-csrf">')
      .replace('<partial name="_AccountLedger" />', partial)
      .replace(/@foreach \(var item in Model.AccountClassifications\) \{ <option[^}]+\}/, '<option value="1">Residential</option><option value="4">Commercial</option>')
      .replace(/@foreach \(var item in Model.AccountStatuses\) \{ <option[^}]+\}/, '<option value="1">Pending</option><option value="2">Suspended</option><option value="3">Active</option><option value="4">Closed</option>');
    return res.end(`<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><link rel="stylesheet" href="/css/appshell.css"><link rel="stylesheet" href="/css/customer-information.css"></head><body><main class="content"><div class="content__header"><h1>Customer Information</h1></div><div class="content__body">${elements}</div></main><script>${script}</script></body></html>`);
  }
  if (url.pathname === '/CustomerSupport/SearchByAccount') return json(accounts.filter(a => a.accountNumber.includes(url.searchParams.get('q')))
    .map(a => ({ accountId: a.id, accountNumber: a.accountNumber, customerId: 1, customerName: '<img src=x onerror="window.injected=true">' })));
  if (url.pathname === '/CustomerSupport/GetCustomerInformationData') {
    if (failLoad) { failLoad = false; res.writeHead(500); return res.end(); }
    return setTimeout(() => json({ customer: customerData, accounts, selectedAccount: accounts[0] }), loadDelays.shift() || 0);
  }
  if (url.pathname === '/Billing/AccountLedgerData') {
    reads.push(url.pathname + url.search);
    if (failFinancial) { res.writeHead(500); return res.end(); }
    const second = url.searchParams.get('accountNumber') === accounts[1].accountNumber;
    return json({ account: { accountNumber: url.searchParams.get('accountNumber'), status: 'Active' },
      bills: [{ billingPeriod: '2026-09-01', paymentStatus: second ? 2 : 1, collection: {} }], otherCharges: [], history: [], totalBalance: second ? 1014 : 397, collectibleOtherCharges: second ? 250 : 0 });
  }
  if (url.pathname === '/CustomerSupport/DisconnectionStatus') {
    const accountNumber = url.searchParams.get('accountNumber'); statusReads.push(accountNumber);
    if (failMonitoring) { res.writeHead(503); return res.end(); }
    const state = { accountNumber, status: accountNumber === accounts[0].accountNumber ? 'FIRST BILL UNPAID' : 'FOR DISCONNECTION', operationalAccountStatus: 'Active', physicalServiceStatus: 'NO CONFIRMED EVENT', scheduledDisconnectionDate: '2026-11-13',
      reconnectionFeeBalance: 0, totalCollectibleBalance: accountNumber === accounts[0].accountNumber ? 397 : 1014, activePenaltyBalance: 17, collectibleOtherCharges: 0, ...statusOverrides[accountNumber] };
    return setTimeout(() => json(state), statusDelays.shift() || 0);
  }
  if (url.pathname === '/ZoneBook/GetZones') return setTimeout(()=>json(zoneBooks), 200);
  if (['/Billing/GetDebitCreditMemosByAccount','/Billing/GetBillAdjustmentsByAccount'].includes(url.pathname)) {
    adjustmentReads.push(url.pathname + url.search);
    const number = url.searchParams.get('accountNumber');
    const data = url.pathname.includes('DebitCredit') ? [{ dcmType:'<img src=x onerror="window.injected=true">', memoType:'credit', amount:30, explanation:number, status:'Pending' }] :
      [{ billingReference:'REF-'+number, type:'<img src=x onerror="window.injected=true">', amount:30 }];
    return setTimeout(()=>json(data), adjustmentDelays.shift() || 0);
  }
  if (['/CustomerSupport/SaveCustomerInformation','/CustomerSupport/SaveAccountInformation'].includes(url.pathname)) {
    assert.equal(req.method, 'POST'); assert.equal(req.headers.requestverificationtoken, 'synthetic-csrf');
    let raw = ''; for await (const chunk of req) raw += chunk;
    const payload = JSON.parse(raw);
    if (url.pathname.endsWith('SaveCustomerInformation')) { customerSaves.push(payload); Object.assign(customerData, payload); }
    else { accountSaves.push(payload); Object.assign(accounts.find(a => a.id === payload.id), payload); }
    return json(payload);
  }
  if (/^\/(js|css)\/[a-z-]+\.(js|css)$/.test(url.pathname)) {
    res.setHeader('Content-Type', url.pathname.endsWith('.js') ? 'text/javascript' : 'text/css');
    return res.end(fs.readFileSync(path.join(web, 'wwwroot', url.pathname)));
  }
  res.writeHead(404); res.end();
});
function command(method, params = {}) {
  const id = ++sequence;
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
async function ledger(number) {
  assert.equal(await evaluate("document.getElementById('txtSearch').value"), number);
  await evaluate("document.getElementById('accountLedger').close(); document.getElementById('btnLedger').click()");
  await waitFor("document.getElementById('accountLedger').open && !document.querySelector('[data-ledger-content]').hidden");
  assert.equal(reads.at(-1), '/Billing/AccountLedgerData?accountNumber=' + number);
  assert.equal(statusReads.at(-1), number, 'Customer Information status used another account');
  assert.equal(await evaluate("document.getElementById('ciServiceReadState').dataset.accountNumber"), number);
  assert.ok((await evaluate("document.querySelector('[data-ledger-content]').textContent")).includes(number), 'Ledger header differs');
}
async function context(number, zone) {
  await waitFor(`document.getElementById('ciServiceReadState').dataset.accountNumber === '${number}' && !document.getElementById('ciServiceReadState').textContent.includes('Loading')`);
    await waitFor(`document.getElementById('fldZone').textContent === '${zone}'`);
  assert.deepEqual(await evaluate("['fldZone','fldBook'].map(id=>document.getElementById(id).textContent)"), [String(zone),'4']);
  assert.equal(await evaluate("document.getElementById('ciAccountNumber').textContent"), number);
  assert.equal(await evaluate("document.getElementById('ciProfileMeter').textContent"), number === accounts[0].accountNumber ? accounts[0].meterNumber : accounts[1].meterNumber);
  assert.equal(await evaluate("document.getElementById('ciProfileBalance').textContent"), number === accounts[0].accountNumber ? '₱397.00' : '₱1,014.00');
}
async function allActions(number) {
  await ledger(number);
  await evaluate("document.getElementById('accountLedger').close(); document.getElementById('btnBills').click()");
  await waitFor("document.getElementById('accountLedger').open && !document.querySelector('[data-ledger-content]').hidden");
  assert.equal(reads.at(-1), '/Billing/AccountLedgerData?accountNumber=' + number);
  assert.equal(await evaluate("document.querySelector('[data-ledger-tab=bills]').getAttribute('aria-selected')"), 'true');
  const previous = adjustmentReads.length;
  await evaluate("document.getElementById('accountLedger').close(); document.getElementById('btnAdjustments').click()");
  await waitFor("!document.getElementById('adjModal').classList.contains('adj-hidden')");
  for (let i = 0; i < 100 && adjustmentReads.length < previous + 2; ++i) await delay(10);
  assert.deepEqual(adjustmentReads.slice(-2).sort(), ['/Billing/GetBillAdjustmentsByAccount?accountNumber=' + number, '/Billing/GetDebitCreditMemosByAccount?accountNumber=' + number]);
  assert.match(await evaluate("document.getElementById('adjCustomerLabel').textContent"), new RegExp(number));
  await evaluate("document.getElementById('btnCloseAdj').click()");
}
async function badgeContrast() {
  return evaluate(`(() => {
    const luminance = color => color.match(/[\\d.]+/g).slice(0,3).map(Number).map(v => {
      v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4;
    }).reduce((sum,v,i) => sum + v * [.2126,.7152,.0722][i], 0);
    return [...document.querySelectorAll('.ci-status')].every(node => {
      const style = getComputedStyle(node), a = luminance(style.color), b = luminance(style.backgroundColor);
      return (Math.max(a,b)+.05)/(Math.min(a,b)+.05) >= 4.5;
    });
  })()`);
}
async function select(number) {
  await evaluate(`document.getElementById('txtSearch').value='${number}'; document.getElementById('txtSearch').dispatchEvent(new Event('input'))`);
  await waitFor("document.querySelector('#searchDropdown li[role=option]') !== null && !document.getElementById('searchDropdown').classList.contains('hidden')");
  await evaluate("document.querySelector('#searchDropdown li[role=option]').click()");
  await waitFor(`document.getElementById('fldMeterNumber').value === '${number === accounts[0].accountNumber ? 'M3' : 'M10'}'`);
}
(async () => {
  try {
    const profile = fs.mkdtempSync(path.join(root, '.tmp/customer-information-browser-'));
    await new Promise(r => server.listen(0, '127.0.0.1', r));
    browser = spawn(process.env.TMCWD_BROWSER || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
      ['--headless=new', '--disable-gpu', '--no-first-run', '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'], { windowsHide: true, stdio: 'ignore' });
    browser.on('error', e => errors.push(e.message));
    let port;
    for (let i = 0; i < 200; ++i) { if (fs.existsSync(path.join(profile, 'DevToolsActivePort'))) { port = fs.readFileSync(path.join(profile, 'DevToolsActivePort'), 'utf8').split('\n')[0]; break; } await delay(50); }
    if (!port) throw new Error('Headless Edge did not start: ' + errors.join('; '));
    const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
    socket = new WebSocket(targets.find(t => t.type === 'page').webSocketDebuggerUrl);
    await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
    socket.onmessage = e => {
      const m = JSON.parse(e.data);
      if (m.method === 'Runtime.exceptionThrown') errors.push(m.params.exceptionDetails.text);
      const p = pending.get(m.id); if (p) { pending.delete(m.id); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
    };
    await command('Runtime.enable'); await command('Page.enable');
    await command('Page.navigate', { url: `http://127.0.0.1:${server.address().port}/` });
    await waitFor('!!window.testSelection && !!window.openAccountLedger');
    await ledger('01-010003'); console.log('PASS A: initial account');
    await select('01-010010'); await ledger('01-010010'); console.log('PASS B/E: exact searched account, same customer and second account');
    await select('01-010003'); await ledger('01-010003');
    await select('01-010010'); await ledger('01-010010');
    await select('01-010003'); await ledger('01-010003'); console.log('PASS C: repeated selection');
    failLoad = true;
    await evaluate("document.getElementById('txtSearch').value='01-010010'; document.getElementById('txtSearch').dispatchEvent(new Event('input'))");
    await waitFor("!document.getElementById('searchDropdown').classList.contains('hidden')");
    await evaluate("document.querySelector('#searchDropdown li[role=option]').click()");
    await waitFor("document.getElementById('ciToast').textContent === 'Customer not found.'");
    await ledger('01-010003'); console.log('PASS: failed selection retains displayed account and Ledger context');
    loadDelays = [1200, 0];
    await evaluate("document.getElementById('txtSearch').value='01-010010'; document.getElementById('txtSearch').dispatchEvent(new Event('input'))");
    await waitFor("!document.getElementById('searchDropdown').classList.contains('hidden')");
    await evaluate("document.querySelector('#searchDropdown li[role=option]').click()");
    await select('01-010003'); await delay(1300); await ledger('01-010003'); console.log('PASS: older response cannot replace latest selection');
    await evaluate("document.getElementById('accountLedger').close(); document.getElementById('btnNext').click()");
    await waitFor("document.getElementById('txtSearch')?.value === '01-010010' && !!window.openAccountLedger"); await ledger('01-010010');
    await evaluate("document.getElementById('accountLedger').close(); document.getElementById('btnPrev').click()");
    await waitFor("document.getElementById('txtSearch')?.value === '01-010003' && !!window.openAccountLedger"); await ledger('01-010003'); console.log('PASS D: Previous/Next page navigation');
    await evaluate(`window.testSelection.switchToAccount(${JSON.stringify(accounts[1])})`); await ledger('01-010010'); console.log('PASS: direct account selection');
    await evaluate("document.getElementById('accountLedger').close(); window.testSelection.populatePage({customer:{id:3},accounts:[],selectedAccount:null})");
    const count = reads.length;
    assert.equal(await evaluate("document.getElementById('btnLedger').disabled"), true);
    await evaluate("document.getElementById('btnLedger').dispatchEvent(new Event('click'))"); await delay(100);
    assert.equal(reads.length, count); assert.equal(await evaluate("document.getElementById('accountLedger').open"), false);
    assert.equal(await evaluate("document.getElementById('txtSearch').value"), '');
    assert.equal(await evaluate("document.getElementById('ciToast').textContent"), 'No account selected.'); console.log('PASS F: empty selection clears prior context');
    assert.deepEqual(await evaluate("['fldZone','fldBook','ciAccountNumber','ciProfileMeter','ciProfileBalance'].map(id=>document.getElementById(id).textContent)"), ['—','—','—','—','—']);
    const address = `http://127.0.0.1:${server.address().port}/`;
    for (const [width,height] of [[1440,900],[768,1024],[390,844]]) for (const theme of ['light','dark']) {
      await command('Emulation.setDeviceMetricsOverride', { width,height,deviceScaleFactor:1,mobile:false });
      await command('Page.navigate', { url: address });
      await waitFor('!!window.testSelection');
      await evaluate(`document.documentElement.setAttribute('data-theme','${theme}')`);
      await context(accounts[0].accountNumber, 5);
      assert.equal(await evaluate("document.querySelectorAll('#ciServiceBanner, #ciServiceState, .ci-summary, #ciAccountStatus').length"),0,'Removed UI still present');
      assert.deepEqual(await evaluate("[...document.querySelector('.ci-body').children].map(card=>card.getAttribute('aria-labelledby'))"),['ciDetailsTitle','ciMeterTitle']);
      assert.equal(await evaluate("document.getElementById('ciMonitoringStatus').closest('section').getAttribute('aria-labelledby')"),'ciMeterTitle');
      assert.equal(await evaluate("getComputedStyle(document.querySelector('.ci-body')).gridTemplateColumns.split(' ').length"),width===390?1:2);

      assert.equal(await evaluate("document.getElementById('ciOperationalStatus').classList.contains('ci-status--success')"), true);
      assert.equal(await evaluate("document.getElementById('ciMonitoringStatus').classList.contains('ci-status--warning')"), true);
      assert.equal(await evaluate("document.querySelector('[data-ci-value=fldEmail]').textContent"), '—');
      assert.equal(await evaluate("getComputedStyle(document.getElementById('fldEmail')).display"), 'none', 'View mode shows disabled form controls');
      assert.equal(await evaluate("document.getElementById('ciMore').contains(document.getElementById('btnDelete'))"), true);
      assert.equal(await evaluate("document.getElementById('btnDelete').classList.contains('ci-btn--danger')"), true);
      assert.equal(await evaluate("document.querySelector('.ci-navigation').contains(document.getElementById('btnLedger'))"), false);
      await allActions(accounts[0].accountNumber);
      await select(accounts[1].accountNumber); await context(accounts[1].accountNumber, 6); await allActions(accounts[1].accountNumber);
      assert.equal(await evaluate("document.getElementById('ciMonitoringStatus').classList.contains('ci-status--danger')"), true);
      await evaluate(`document.getElementById('ciAccounts').value='${accounts[0].accountNumber}'; document.getElementById('ciAccounts').dispatchEvent(new Event('change'))`);
      await context(accounts[0].accountNumber, 5); await allActions(accounts[0].accountNumber);
      assert.equal(await badgeContrast(), true, 'Badge text contrast is below WCAG AA');
      assert.equal(await evaluate(`document.documentElement.scrollWidth <= ${width} && document.querySelector('.ci-page').getBoundingClientRect().right <= ${width}`), true, 'Customer Information overflows viewport');
      assert.equal(await evaluate("document.querySelector('.content').scrollWidth <= document.querySelector('.content').clientWidth"), true,
        'Content pane overflow: ' + JSON.stringify(await evaluate(`({pane:[document.querySelector('.content').scrollWidth,document.querySelector('.content').clientWidth,document.querySelector('.content').scrollLeft],nodes:Array.from(document.querySelectorAll('.content *')).map(el=>({id:el.id,cls:el.className,right:el.getBoundingClientRect().right,width:el.getBoundingClientRect().width,scroll:el.scrollWidth,client:el.clientWidth})).filter(el=>el.width && (el.scroll>el.client+1 || el.right+document.querySelector('.content').scrollLeft > ${width - 15}))})`)));
      assert.equal(await evaluate("[...document.querySelectorAll('.ci-account-actions .ci-btn')].every(button=>button.getBoundingClientRect().height>=44)"), true);
      assert.equal(await evaluate("document.querySelectorAll('#searchDropdown img').length"), 0);
      assert.equal(await evaluate("!!window.injected"), false);
      fs.writeFileSync(path.join(profile,`profile-${width}-${theme}.png`), Buffer.from((await command('Page.captureScreenshot')).data,'base64'));
      console.log(`PASS: profile, real Zone/Book 5/4 → 6/4 → 5/4, exact Ledger/Bills/Adjustments, read-only values, AA badges and layout ${width}x${height} ${theme}.`);
    }
    statusOverrides[accounts[1].accountNumber] = { status: 'BALANCE + RECONNECTION FEE PENDING', operationalAccountStatus: 'Suspended', physicalServiceStatus: 'DISCONNECTED', actualDisconnectionDateUtc: '2026-11-13T02:00:00Z', reconnectionFeeBalance:350 };
    await evaluate(`window.testSelection.switchToAccount(${JSON.stringify(accounts[1])})`);
    await context(accounts[1].accountNumber, 6);
    assert.equal(await evaluate("document.getElementById('ciMonitoringStatus').textContent"), 'BALANCE + RECONNECTION FEE PENDING');
    assert.equal(await evaluate("document.getElementById('ciPhysicalStatus').classList.contains('ci-status--danger')"), true);
    assert.equal(await evaluate("document.getElementById('ciPhysicalStatus').textContent"), 'DISCONNECTED');
    assert.equal(await evaluate("document.querySelector('[data-ci-value=fldMeterStatus]').textContent"), 'Suspended');
    assert.equal(await evaluate("document.getElementById('ciDisconnectionDate').textContent"), 'Nov 13, 2026');
    statusDelays = [800,0];
    await evaluate(`window.testSelection.switchToAccount(${JSON.stringify(accounts[1])}); window.testSelection.switchToAccount(${JSON.stringify(accounts[0])})`);
    await context(accounts[0].accountNumber,5); await delay(850); await context(accounts[0].accountNumber,5);
    assert.equal(await evaluate("document.getElementById('ciOperationalStatus').textContent"), 'Active');
    console.log('PASS: physical/operational service fields retain reconnection-fee status; slow old response cannot replace selected account.');
    failMonitoring = true; failFinancial = true;
    await evaluate("document.getElementById('ciRefreshService').click()");
    await waitFor("document.getElementById('ciServiceReadState').textContent.includes('unavailable')");
    assert.equal(await evaluate("document.getElementById('ciProfileBalance').textContent"), '—');
    assert.equal(await evaluate("document.getElementById('ciPhysicalStatus').textContent"), '—');
    failMonitoring = false; failFinancial = false;
    await evaluate("document.getElementById('ciRefreshService').click()"); await context(accounts[0].accountNumber,5);
    await evaluate(`window.testSelection.switchToAccount(${JSON.stringify({...accounts[0],zoneBookId:999})})`);
    assert.deepEqual(await evaluate("['fldZone','fldBook'].map(id=>document.getElementById(id).textContent)"), ['—','—']);
    await evaluate(`window.testSelection.switchToAccount(${JSON.stringify(accounts[0])})`); await context(accounts[0].accountNumber,5);
    console.log('PASS: unavailable financial/service reads clear stale values; missing ZoneBook never falls back to another row.');
    adjustmentDelays = [800,800,0,0];
    await evaluate("document.getElementById('btnAdjustments').click()");
    await delay(50);
    await evaluate(`document.getElementById('btnCloseAdj').click(); window.testSelection.switchToAccount(${JSON.stringify(accounts[1])}); document.getElementById('btnAdjustments').click(); document.getElementById('adjTabAdj').click()`);
    await waitFor("document.getElementById('adjAdjBody').textContent.includes('REF-01-010010')");
    await delay(850);
    assert.equal(await evaluate("document.getElementById('adjAdjBody').textContent.includes('REF-01-010003')"), false);
    assert.equal(await evaluate("document.querySelectorAll('#adjModal img').length"), 0);
    await evaluate("document.getElementById('btnCloseAdj').click()");
    await evaluate(`window.testSelection.switchToAccount(${JSON.stringify(accounts[0])})`); await context(accounts[0].accountNumber,5);
    console.log('PASS: delayed adjustment history cannot overwrite a newly opened account; history remains safely rendered text.');
    await evaluate("document.getElementById('btnEditCustomer').click(); document.getElementById('fldEmail').value='cancel@example.test'; document.getElementById('btnCancelCustomer').click()");
    assert.equal(await evaluate("document.getElementById('fldEmail').value"), '');
    await evaluate("document.getElementById('btnEditCustomer').click(); document.getElementById('fldEmail').value='saved@example.test'; document.getElementById('btnSaveCustomer').click()");
    await waitFor("document.getElementById('ciToast').textContent === 'Customer saved.'");
    assert.equal(customerSaves.at(-1).email,'saved@example.test');
    assert.equal(await evaluate("document.querySelector('[data-ci-value=fldEmail]').textContent"), 'saved@example.test');
    await select(accounts[1].accountNumber); await context(accounts[1].accountNumber,6);
    await evaluate("document.getElementById('btnEditAccount').click(); document.getElementById('fldMeterNumber').value='M10-UPDATED'; document.getElementById('btnSaveAccount').click()");
    await waitFor("document.getElementById('ciToast').textContent === 'Account saved.'");
    assert.equal(accountSaves.at(-1).id,10); assert.equal(accountSaves.at(-1).zoneBookId,25);
    assert.equal(accountSaves.at(-1).meterNumber,'M10-UPDATED');
    assert.equal(await evaluate("document.getElementById('ciProfileMeter').textContent"),'M10-UPDATED');
    await evaluate(`document.getElementById('ciAccounts').value='${accounts[0].accountNumber}'; document.getElementById('ciAccounts').dispatchEvent(new Event('change')); document.getElementById('ciAccounts').value='${accounts[1].accountNumber}'; document.getElementById('ciAccounts').dispatchEvent(new Event('change'))`);
    await context(accounts[1].accountNumber,6);
    assert.equal(await evaluate("document.getElementById('ciProfileMeter').textContent"),'M10-UPDATED', 'Cached account restored old meter after save');
    console.log('PASS: edit controls/cancel and unchanged CSRF save contracts preserve exact account and ZoneBookId.');
    await evaluate(`document.getElementById('txtSearch').value='${accounts[0].accountNumber}'; document.getElementById('txtSearch').dispatchEvent(new Event('input'))`);
    await waitFor("!document.getElementById('searchDropdown').classList.contains('hidden')");
    await evaluate("document.getElementById('txtSearch').focus()");
    for (const type of ['keyDown','keyUp']) await command('Input.dispatchKeyEvent',{type,key:'ArrowDown',code:'ArrowDown',windowsVirtualKeyCode:40});
    assert.equal(await evaluate("document.activeElement.getAttribute('role')"),'option');
    for (const type of ['keyDown','keyUp']) await command('Input.dispatchKeyEvent',{type,key:'Enter',code:'Enter',windowsVirtualKeyCode:13});
    await context(accounts[0].accountNumber,5);
    await evaluate("document.getElementById('btnEditCustomer').click()");
    assert.equal(await evaluate("document.activeElement.id"),'fldEmail');
    assert.equal(await evaluate("document.getElementById('fldEmail').labels[0].textContent"),'Email');
    await evaluate("document.getElementById('btnCancelCustomer').click()");
    console.log('PASS: keyboard search selection, edit focus and associated form labels.');
    for (const theme of ['light','dark']) {
      await evaluate(`document.documentElement.setAttribute('data-theme','${theme}')`);
      for (const [status,kind] of [['CURRENT','neutral'],['SUBJECT FOR DISCONNECTION','subject'],['FOR DISCONNECTION','danger'],['BALANCE + RECONNECTION FEE PENDING','warning'],['ELIGIBLE FOR RECONNECTION','success'],['RECONNECTED','success'],['FINANCIAL / SERVICE STATE REVIEW REQUIRED','review']]) {
        statusOverrides[accounts[0].accountNumber] = {status};
        await evaluate("document.getElementById('ciRefreshService').click()");
        await waitFor(`document.getElementById('ciMonitoringStatus').textContent === '${status}'`);
        assert.equal(await evaluate(`document.getElementById('ciMonitoringStatus').classList.contains('ci-status--${kind}')`),true);
        assert.equal(await badgeContrast(),true);
      }
      statusOverrides[accounts[0].accountNumber] = { status:'BALANCE + RECONNECTION FEE PENDING',operationalAccountStatus:'Suspended',physicalServiceStatus:'DISCONNECTED',actualDisconnectionDateUtc:'2026-11-13T02:00:00Z',reconnectionFeeBalance:350 };
      await evaluate("document.getElementById('ciRefreshService').click()");
      await waitFor("document.getElementById('ciPhysicalStatus').textContent === 'DISCONNECTED'");
      assert.equal(await badgeContrast(),true);
      assert.equal(await evaluate("document.getElementById('ciPhysicalStatus').classList.contains('ci-status--danger')"),true);
      statusOverrides[accounts[0].accountNumber] = {status:'CURRENT',operationalAccountStatus:'Suspended',physicalServiceStatus:'NO CONFIRMED EVENT'};
      await evaluate("document.getElementById('ciRefreshService').click()");
      await waitFor("document.getElementById('ciOperationalStatus').textContent === 'Suspended'");
      assert.equal(await evaluate("document.getElementById('ciPhysicalStatus').textContent"),'NO CONFIRMED EVENT','Suspended alone invented a physical disconnection');
      console.log(`PASS: all monitoring status badges, event-derived Disconnected and no status-only physical inference ${theme}.`);
    }
    assert.deepEqual(errors, []);
    statusOverrides[accounts[0].accountNumber] = {status:'RECONNECTED',operationalAccountStatus:'Active',physicalServiceStatus:'RECONNECTED',actualDisconnectionDateUtc:'2026-11-13T02:00:00Z',actualReconnectionDateUtc:'2026-11-14T02:00:00Z',reconnectionFeeBalance:0};
    await evaluate("document.getElementById('ciRefreshService').click()");
    await waitFor("document.getElementById('ciPhysicalStatus').textContent === 'RECONNECTED'");
    assert.equal(await evaluate("document.getElementById('ciOperationalStatus').textContent"),'Active');
    assert.equal(await evaluate("document.getElementById('ciMonitoringStatus').textContent"),'RECONNECTED');
    assert.equal(await evaluate("document.getElementById('ciReconnectionDate').textContent"),'Nov 14, 2026');
    console.log('PASS: actual reconnection read displays Active / RECONNECTED and server-confirmed date in Meter & Service.');
    console.log('PASS: Customer Information enhancement complete; screenshots: ' + profile);
  } catch (e) { console.error(e); process.exitCode = 1; }
  finally {
    if (socket?.readyState === WebSocket.OPEN) await command('Browser.close').catch(()=>{});
    socket?.close(); browser?.kill(); server.close();
  }
})();
