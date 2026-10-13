const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.resolve(__dirname, '../../TMCWD/TMCWD.Application/wwwroot/js/penalty-charging.js'), 'utf8');
const settle = () => new Promise(r => setImmediate(r));
const entry = (ref, account = 'ACCOUNT-1') => ({ billingReferenceId: ref, accountNo: account, name: 'Test customer', billAmount: 1000, penalty: 100, discount: 0, usage: 10, dueDate: '2026-01-01T00:00:00' });
function page(fetch) {
    const nodes = new Map();
    function node(id) {
        if (!nodes.has(id)) nodes.set(id, {
            value: '', textContent: '', innerHTML: '', disabled: false, hidden: true,
            style: {}, dataset: {}, handlers: {}, checked: false,
            classList: { add() {}, remove() {}, toggle() {} },
            addEventListener(type, handler) { this.handlers[type] = handler; },
            querySelectorAll() { return []; }, focus() {}, setAttribute() {}, insertBefore() {},
        });
        return nodes.get(id);
    }
    let ready;
    const context = vm.createContext({ fetch, Date, URLSearchParams, console, setTimeout: () => 1, clearTimeout() {},
        document: { getElementById: node, querySelectorAll: () => [], querySelector: () => ({ value: 'TOKEN' }),
            addEventListener(type, fn) { if (type === 'DOMContentLoaded') ready = fn; }, createElement: () => node('new') } });
    vm.runInContext(source, context);
    ready();
    return { node, selectAll() { node('pcSelectAll').checked = true; node('pcSelectAll').handlers.change(); } };
}
test('Loads all overdue bills by default and posts billing references, not account numbers', async () => {
    let request;
    const p = page(async (url, options) => {
        if (!options) {
            const requestUrl = new URL(url, 'http://localhost');
            assert.equal(requestUrl.pathname, '/Billing/GetPenaltyChargeEntries');
            assert.equal(requestUrl.searchParams.get('billingPeriod'), null);
            assert.match(requestUrl.searchParams.get('postingDate'), /^\d{4}-\d{2}-\d{2}$/);
            return { ok: true, json: async () => [entry('BILL-1'), entry('BILL-2')] };
        }
        request = options;
        return { ok: true, json: async () => ({ postedCount: 2, totalPenalty: 200, postingDate: '2026-09-22T00:00:00' }) };
    });
    await settle();
    p.selectAll();
    await p.node('pcConfirmOk').handlers.click();
    assert.deepEqual(JSON.parse(request.body).billingReferenceIds, ['BILL-1', 'BILL-2']);
    assert.equal(JSON.parse(request.body).billingPeriod, null);
    assert.equal(request.headers.RequestVerificationToken, 'TOKEN');
    assert.equal(p.node('pcSuccessModal').hidden, false);
    assert.equal(p.node('pcSuccessCount').textContent, 2);
    assert.equal(p.node('pcPostBtn').disabled, true);
});
test('Posting failure retains entries and never opens success modal', async () => {
    const p = page(async (_, options) => options
        ? { ok: false, text: async () => 'Bill is already paid; reload.' }
        : { ok: true, json: async () => [entry('BILL-1')] });
    await settle();
    p.selectAll();
    await p.node('pcConfirmOk').handlers.click();
    assert.equal(p.node('pcSuccessModal').hidden, true);
    assert.match(p.node('pcTableBody').innerHTML, /BILL-1/);
    assert.match(p.node('pcToast').textContent, /already paid/);
});
test('Loading failure does not create demo entries or permit posting', async () => {
    const p = page(async () => { throw new Error('offline'); });
    await settle();
    assert.equal(p.node('pcPostBtn').disabled, true);
    assert.match(p.node('pcTableBody').innerHTML, /Unable to load/);
    assert.equal(p.node('pcSuccessModal').hidden, true);
});
test('Changing billing date loads that exact date and clears the previous selection', async () => {
    const urls = [];
    const p = page(async url => { urls.push(url); return { ok: true, json: async () => [entry('BILL-1')] }; });
    await settle();
    p.selectAll();
    p.node('pcBillingPeriod').value = '2026-07-15';
    await p.node('pcBillingPeriod').handlers.change();
    assert.match(urls[1], /billingPeriod=2026-07-15/);
    assert.equal(p.node('pcPostBtn').disabled, true);
});
test('Double clicking post sends only one request', async () => {
    let calls = 0, resolve;
    const p = page(async (_, options) => {
        if (!options) return { ok: true, json: async () => [entry('BILL-1')] };
        calls++;
        return new Promise(r => resolve = r);
    });
    await settle();
    p.selectAll();
    const pending = p.node('pcConfirmOk').handlers.click();
    await p.node('pcConfirmOk').handlers.click();
    assert.equal(calls, 1);
    resolve({ ok: true, json: async () => ({ postedCount: 1, totalPenalty: 100, postingDate: '2026-09-22' }) });
    await pending;
});
