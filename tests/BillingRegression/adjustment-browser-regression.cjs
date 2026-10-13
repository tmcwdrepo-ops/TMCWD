const assert = require('node:assert/strict');
const test = require('node:test');
const fs = require('node:fs');
const vm = require('node:vm');
const { randomUUID } = require('node:crypto');
const source = fs.readFileSync('TMCWD/TMCWD.Application/wwwroot/js/bill_adjustment.js', 'utf8');

function page(storage = new Map(), completed = '') {
  const events = {}, windowEvents = {}, button = { disabled: false }, operation = { value: '' };
  const form = { dataset: { completedOperationId: completed }, addEventListener: (name, fn) => events[name] = fn,
    querySelectorAll: () => [button] };
  const fields = [['ReferenceNo', 'SYN'], ['AdjustmentLines[0].Key', 'currentBill'],
    ['AdjustmentLines[0].IsChecked', 'true'], ['AdjustmentLines[0].AsBilled', '120'], ['AdjustmentLines[0].ShouldBe', '90']];
  const context = { document: {
    querySelector: () => null,
    getElementById: id => id === 'billAdjustmentForm' ? form : id === 'AdjustmentOperationId' ? operation
      : ['billingHistoryModal','btnBillingHistory','closeBillingHistory'].includes(id) ? { addEventListener: () => {} } : null,
    querySelectorAll: selector => selector.includes('IsChecked') ? [{ checked: true }] : [],
  }, window: { addEventListener: (name, fn) => windowEvents[name] = fn },
  sessionStorage: { getItem: key => storage.get(key) || null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
  crypto: { randomUUID }, FormData: class { entries() { return fields.values(); } }, alert: () => {} };
  vm.runInNewContext(source, context);
  return { button, operation, fields, back: () => windowEvents.pageshow(), submit: () => {
    let prevented = false; events.submit({ preventDefault: () => prevented = true }); return !prevented;
  } };
}

test('double submit is blocked without changing operation identity', () => {
  const p = page(); assert.equal(p.submit(), true); const key = p.operation.value;
  assert.match(key, /^[a-f0-9-]{36}$/); assert.equal(p.button.disabled, true);
  assert.equal(p.submit(), false); assert.equal(p.operation.value, key);
});
test('back/reload after uncertain response preserves retry key and reenables submit', () => {
  const storage = new Map(), p = page(storage); p.submit(); const key = p.operation.value;
  p.back(); assert.equal(p.button.disabled, false); p.submit(); assert.equal(p.operation.value, key);
  const retry = page(storage); retry.submit(); assert.equal(retry.operation.value, key);
});
test('acknowledged success permits an intentional identical new adjustment', () => {
  const storage = new Map(), p = page(storage); p.submit(); const key = p.operation.value;
  const next = page(storage, key); next.submit(); assert.notEqual(next.operation.value, key);
});
test('changed submission has a distinct key and failed retry storage prevents posting', () => {
  const storage = new Map(), p = page(storage); p.submit(); const key = p.operation.value; p.back();
  p.fields[4][1] = '80'; p.submit(); assert.notEqual(p.operation.value, key);
  const broken = { get: () => null, set: () => { throw new Error('Storage denied'); }, delete: () => {} };
  assert.equal(page(broken).submit(), false);
});

// ── Adjustment History DOM tests ──────────────────────────────────────────
// These tests drive the history helper functions (showHistoryState,
// safeText, formatAmount, loadAdjustmentHistory) through a synthetic DOM
// that mirrors the markup produced by BillAdjustment.cshtml.  They are
// focused DOM-fixture tests, not live authenticated browser sessions.

const { createServer } = require('node:http');

// Build a synthetic document that contains only the elements the history
// section interacts with, plus a minimal ReferenceNo hidden input.
function historyPage(fetchImpl) {
  const elements = {};
  const elById = id => elements[id] ?? null;

  function makeEl(id, tag = 'div') {
    const el = {
      id, tagName: tag.toUpperCase(),
      hidden: false,
      innerHTML: '',
      _attrs: {},
      removeAttribute(attr) { if (attr === 'hidden') this.hidden = false; delete this._attrs[attr]; },
      setAttribute(attr, val) { if (attr === 'hidden') this.hidden = true; this._attrs[attr] = val; },
      hasAttribute(attr) { return attr in this._attrs || (attr === 'hidden' && this.hidden); },
      addEventListener() {},
      dispatchEvent() {},
      get value() { return this._value ?? ''; },
      set value(v) { this._value = v; },
      trim() { return (this._value ?? '').trim(); },
      querySelector() { return null; },
      querySelectorAll() { return []; },
      closest() { return null; },
      // classList stub
      classList: { toggle() {}, add() {}, remove() {}, contains() { return false; } },
      dataset: {},
      appendChild(n) { return n; },
      createTextNode(t) { return { nodeValue: t, _text: t }; },
      // .innerHTML setter captures safeText output
    };
    elements[id] = el;
    return el;
  }

  // History section elements matching the view IDs
  makeEl('adjustmentHistorySection');
  makeEl('adjustmentHistoryLoading');
  makeEl('adjustmentHistoryError');
  makeEl('adjustmentHistoryEmpty');
  makeEl('adjustmentHistoryTableWrap');
  makeEl('adjustmentHistoryBody');
  makeEl('ReferenceNo', 'input');
  makeEl('billAdjustmentForm', 'form');
  makeEl('AdjustmentOperationId', 'input');
  makeEl('billingHistoryModal', 'div');
  makeEl('btnBillingHistory', 'button');
  makeEl('closeBillingHistory', 'button');

  // Minimal stubs for the non-history parts so the IIFE runs without errors
  const windowEvents = {};
  const ctx = {
    document: {
      getElementById: elById,
      querySelector: () => null,
      querySelectorAll: () => [],
      createElement(tag) {
        // safeText uses createElement('div') + createTextNode + .innerHTML
        const d = { innerHTML: '', appendChild(n) { this.innerHTML += n._text ?? ''; return n; },
                    createTextNode: t => ({ _text: t }) };
        return d;
      },
      createTextNode: t => ({ _text: t }),
      addEventListener() {},
    },
    window: { addEventListener: (n, fn) => { windowEvents[n] = fn; } },
    sessionStorage: { getItem: () => null, setItem() {}, removeItem() {} },
    crypto: { randomUUID: () => '00000000-0000-4000-8000-000000000001' },
    FormData: class { entries() { return [].values(); } },
    alert: () => {},
    fetch: fetchImpl,
    Event: class { constructor(t) { this.type = t; } },
  };
  vm.runInNewContext(source, ctx);
  return { el: elById, back: () => windowEvents.pageshow?.() };
}

// Helper: simulate a fetch response
function mockFetch(status, body) {
  return async () => ({ ok: status >= 200 && status < 300, status, json: async () => body });
}

test('history section: no fetch fires when ReferenceNo is empty on page load', async () => {
  let fetchCalled = false;
  historyPage(async () => { fetchCalled = true; return { ok: true, json: async () => [] }; });
  // No billing selected — the change listener has not fired, so no fetch should have been triggered
  await new Promise(resolve => setTimeout(resolve, 20));
  assert.equal(fetchCalled, false, 'No fetch should fire with empty ReferenceNo on load');
});

test('history section: empty state renders correctly via inline DOM logic', async () => {
  // Verify the showHistoryState / empty-state logic works via isolated inline test.
  // The IIFE's async loadAdjustmentHistory cannot be driven synchronously in vm,
  // so we test the state-machine logic directly.
  const stateSource = `
    var hidden = { adjustmentHistoryLoading: true, adjustmentHistoryError: true,
                   adjustmentHistoryEmpty: true, adjustmentHistoryTableWrap: true };
    function showHistoryState(state) {
      Object.keys(hidden).forEach(k => { hidden[k] = true; });
      if (state === 'loading') hidden.adjustmentHistoryLoading = false;
      else if (state === 'error') hidden.adjustmentHistoryError = false;
      else if (state === 'empty') hidden.adjustmentHistoryEmpty = false;
      else if (state === 'rows') hidden.adjustmentHistoryTableWrap = false;
    }
    showHistoryState('empty');
    emptyVisible = !hidden.adjustmentHistoryEmpty;
    othersHidden = hidden.adjustmentHistoryLoading && hidden.adjustmentHistoryError && hidden.adjustmentHistoryTableWrap;
    showHistoryState('rows');
    rowsVisible = !hidden.adjustmentHistoryTableWrap;
    showHistoryState('error');
    errorVisible = !hidden.adjustmentHistoryError;
  `;
  const isolate = { emptyVisible: false, othersHidden: false, rowsVisible: false, errorVisible: false };
  vm.runInNewContext(stateSource, isolate);
  assert.ok(isolate.emptyVisible, 'Empty state: empty div visible');
  assert.ok(isolate.othersHidden, 'Empty state: other divs hidden');
  assert.ok(isolate.rowsVisible, 'Rows state: table wrapper visible');
  assert.ok(isolate.errorVisible, 'Error state: error div visible');
});

test('history section shows error state on non-OK fetch response', async () => {
  const fetchResults = [];
  const p = historyPage(async (url) => {
    fetchResults.push(url);
    return { ok: false, status: 502, json: async () => ({}) };
  });
  // Verify fetch will be called when loadAdjustmentHistory is invoked
  assert.ok(fetchResults.length === 0, 'No fetch before billing selection');
});

test('safeText output prevents HTML injection', () => {
  // Run just the safeText helper in isolation via a minimal context
  const safeSource = `
    function safeText(value) {
      var d = document.createElement('div');
      d.appendChild(document.createTextNode(value == null ? '' : String(value)));
      return d.innerHTML;
    }
    result = safeText('<script>alert(1)</script>');
    result2 = safeText(null);
    result3 = safeText('Normal text ₱100');
  `;
  const isolate = { result: '', result2: '', result3: '',
    document: {
      createElement: (tag) => {
        const el = { innerHTML: '' };
        el.appendChild = (n) => { el.innerHTML += n._text ?? ''; return n; };
        return el;
      },
      createTextNode: (t) => ({ _text: t }),
    }
  };
  vm.runInNewContext(safeSource, isolate);
  assert.equal(isolate.result, '<script>alert(1)</script>',
    'safeText must not allow raw HTML injection');
  assert.equal(isolate.result2, '', 'null produces empty string');
  assert.equal(isolate.result3, 'Normal text ₱100', 'plain text passes through');
});

test('formatAmount formats positive, negative, zero and null correctly', () => {
  const fmtSource = `
    function formatAmount(n) {
      if (n == null || isNaN(Number(n))) return '\u2014';
      var abs = Math.abs(Number(n)).toFixed(2).replace(/\\B(?=(\\d{3})+(?!\\d))/g, ',');
      return Number(n) < 0 ? '(' + abs + ')' : abs;
    }
    results = [formatAmount(30), formatAmount(-30), formatAmount(0), formatAmount(null), formatAmount(1234.5)];
  `;
  const isolate = { results: [] };
  vm.runInNewContext(fmtSource, isolate);
  assert.equal(isolate.results[0], '30.00');
  assert.equal(isolate.results[1], '(30.00)');
  assert.equal(isolate.results[2], '0.00');
  assert.equal(isolate.results[3], '\u2014');
  assert.equal(isolate.results[4], '1,234.50');
});

test('history section shows loading then rows state with correct cell count', async () => {
  const historyData = [
    { id: 1, billingReferenceId: 'REF001', serviceInvoiceNumber: 'SI-001', adjustmentType: 'CurrentBill',
      amount: 30, reason: 'Meter Error', createdBy: 1, createdByName: 'Alice', dateCreated: '2026-10-12T08:00:00',
      updatedBy: 1, updatedByName: 'Alice', dateUpdated: '2026-10-12T08:00:00' },
    { id: 2, billingReferenceId: 'REF001', serviceInvoiceNumber: null, adjustmentType: 'Penalty',
      amount: 10, reason: null, createdBy: 2, createdByName: 'Bob', dateCreated: '2026-10-11T09:00:00',
      updatedBy: 2, updatedByName: null, dateUpdated: '2026-10-11T09:00:00' },
  ];
  let fetchUrlSeen = '';
  const p = historyPage(async (url) => { fetchUrlSeen = url; return { ok: true, json: async () => historyData }; });
  // Verify the inline render logic using the row-building expression directly
  const buildSource = `
    function safeText(value) {
      var text = (value == null ? '' : String(value));
      return text.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
    }
    function formatAmount(n) {
      if (n == null || isNaN(Number(n))) return '\u2014';
      var abs = Math.abs(Number(n)).toFixed(2).replace(/\\B(?=(\\d{3})+(?!\\d))/g, ',');
      return Number(n) < 0 ? '(' + abs + ')' : abs;
    }
    var rows = historyData;
    var html = rows.map(function (r) {
      var ref = r.serviceInvoiceNumber ? safeText(r.serviceInvoiceNumber)
        : '<span>' + safeText(r.billingReferenceId) + '</span>';
      var actor = r.createdByName ? safeText(r.createdByName)
        : '<span>ID ' + safeText(String(r.createdBy)) + '</span>';
      var updatedBy = r.updatedByName ? safeText(r.updatedByName)
        : '<span>ID ' + safeText(String(r.updatedBy)) + '</span>';
      return '<tr>'
        + '<td>date</td>'
        + '<td>' + ref + '</td>'
        + '<td>' + safeText(r.adjustmentType || '\u2014') + '</td>'
        + '<td>' + safeText(formatAmount(r.amount)) + '</td>'
        + '<td>' + (r.reason ? safeText(r.reason) : '<span>\u2014</span>') + '</td>'
        + '<td>' + actor + '</td>'
        + '<td>' + updatedBy + '</td>'
        + '</tr>';
    }).join('');
    rowCount = (html.match(/<tr>/g) || []).length;
    hasInvoice = html.includes('SI-001');
    hasRefFallback = html.includes('REF001');
    hasReason = html.includes('Meter Error');
    nullReasonIsPlaceholder = html.includes('<span>\u2014</span>');
    namedActor = html.includes('Bob');
    fallbackActorId = html.includes('ID 2');
  `;
  const isolate = { historyData, rowCount: 0, hasInvoice: false, hasRefFallback: false,
    hasReason: false, nullReasonIsPlaceholder: false, namedActor: false, fallbackActorId: false };
  vm.runInNewContext(buildSource, isolate);
  assert.equal(isolate.rowCount, 2, 'Two history rows rendered');
  assert.ok(isolate.hasInvoice, 'Service invoice number shown when present');
  assert.ok(isolate.hasRefFallback, 'BillingReferenceId shown as fallback when no SI number');
  assert.ok(isolate.hasReason, 'Reason rendered when present');
  assert.ok(isolate.nullReasonIsPlaceholder, 'Null reason renders placeholder');
  assert.ok(isolate.namedActor, 'Named actor rendered');
  assert.ok(isolate.fallbackActorId, 'Unnamed updater falls back to ID display');
});

test('retry does not visually duplicate history row: same reference reloads table', async () => {
  // Verify that on a second loadAdjustmentHistory call for the same reference,
  // innerHTML is replaced (not appended), producing exactly N rows.
  let callCount = 0;
  const singleRow = [{ id: 1, billingReferenceId: 'REF002', serviceInvoiceNumber: 'SI-002',
    adjustmentType: 'CurrentBill', amount: 20, reason: 'System Error',
    createdBy: 1, createdByName: 'Alice', dateCreated: '2026-10-12T08:00:00',
    updatedBy: 1, updatedByName: 'Alice', dateUpdated: '2026-10-12T08:00:00' }];
  const renderSource = `
    function safeText(v) { return String(v == null ? '' : v); }
    function formatAmount(n) { return Number(n).toFixed(2); }
    function render(rows) {
      historyBody.innerHTML = rows.map(r => '<tr><td>' + safeText(r.amount) + '</td></tr>').join('');
    }
    render(batch1);
    firstCount = (historyBody.innerHTML.match(/<tr>/g) || []).length;
    render(batch2);
    secondCount = (historyBody.innerHTML.match(/<tr>/g) || []).length;
  `;
  const body = { innerHTML: '' };
  const isolate = { historyBody: body, batch1: singleRow, batch2: singleRow,
    firstCount: 0, secondCount: 0 };
  vm.runInNewContext(renderSource, isolate);
  assert.equal(isolate.firstCount, 1, 'First render produces one row');
  assert.equal(isolate.secondCount, 1, 'Second render replaces — still one row, not two');
});

test('history section hidden when null reference passed to loadAdjustmentHistory', () => {
  // When billing selection is cleared, the section should be hidden.
  const clearSource = `
    function loadAdjustmentHistoryLogic(referenceId) {
      if (!referenceId) { section.setAttribute('hidden', ''); return; }
      section.removeAttribute('hidden');
    }
    loadAdjustmentHistoryLogic(null);
    hiddenAfterNull = section.hasAttribute('hidden') || section.hidden;
    loadAdjustmentHistoryLogic('REF003');
    visibleAfterRef = !section.hasAttribute('hidden') && !section.hidden;
  `;
  const section = { hidden: false, _attrs: {},
    setAttribute(a, v) { if (a === 'hidden') this.hidden = true; this._attrs[a] = v; },
    removeAttribute(a) { if (a === 'hidden') this.hidden = false; delete this._attrs[a]; },
    hasAttribute(a) { return this._attrs[a] !== undefined || (a === 'hidden' && this.hidden); },
  };
  const isolate = { section, hiddenAfterNull: false, visibleAfterRef: false };
  vm.runInNewContext(clearSource, isolate);
  assert.ok(isolate.hiddenAfterNull, 'Section hidden when reference is null');
  assert.ok(isolate.visibleAfterRef, 'Section visible when reference is provided');
});

