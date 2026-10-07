const fs = require('fs');
const vm = require('vm');
const assert = require('assert/strict');
const source = fs.readFileSync('TMCWD/TMCWD.Application/wwwroot/js/dashboard.js', 'utf8');
const start = source.indexOf('    let serviceRequestPdfUrl = null;');
const end = source.indexOf('    async function saveServiceRequestDetail', start);
const nodes = {};
let requestedUrl, table, fail = false;
const printed = [], boxes = [];
const pending = [];
const context = {
  selectedServiceRequest: { accountNumber: '01-001', accountName: 'First customer', particulars: 'Long request details' },
  document: { getElementById: id => nodes[id] ||= { innerHTML: '', textContent: '' }, querySelector: () => null },
  window: { addEventListener() {}, jspdf: { jsPDF: class {
    constructor(options) {
      assert.equal(options.orientation, 'portrait');
      this.internal = { pageSize: { getWidth: () => 595.28, getHeight: () => 841.89 } };
    }
    setFontSize() {} setFont() {} setDrawColor() {} setTextColor() {} setLineWidth() {} line() {} addPage() {}
    rect(...args) { boxes.push(args); }
    text(content) { printed.push(...(Array.isArray(content) ? content : [content])); }
    splitTextToSize(value, width) { return String(value).match(new RegExp('.{1,' + Math.max(1, Math.floor(width / 5)) + '}', 'g')) || []; }
    setProperties() {} autoTable(options) { table = options; } output() { return {}; }
  } } },
  URL: { createObjectURL: () => 'blob:request-pdf', revokeObjectURL() {} },
  srAccountLedgerModal: {}, openModal() {}, scheduleModalFit() {},
  textValue(id, value) { context.document.getElementById(id).textContent = value; },
  dateText: value => value || '—', money: value => Number(value).toFixed(2),
  html: value => String(value).replaceAll('<', '&lt;').replaceAll('>', '&gt;'),
  fetch: url => { requestedUrl = url; return new Promise(resolve => pending.push(resolve)); }
};
vm.createContext(context);
vm.runInContext(source.slice(start, end), context);
const response = entries => ({ ok: true, json: async () => entries });
(async () => {
  const first = context.openServiceRequestLedger();
  assert.equal(requestedUrl, '/Billing/History?accountNumber=01-001');
  context.selectedServiceRequest = { accountNumber: '02-002', accountName: 'Second customer', particulars: 'All details' };
  const second = context.openServiceRequestLedger();
  pending[1](response([{ referenceNo: '<script>', billingDate: '2026-10-02', debit: 150, credit: 50 }]));
  await second;
  assert.equal(nodes.srAccountLedgerBalance.textContent, '₱100.00');
  assert.ok(nodes.srAccountLedgerRows.innerHTML.includes('&lt;script&gt;'));
  pending[0](response([{ debit: 9999 }]));
  await first;
  assert.equal(nodes.srAccountLedgerBalance.textContent, '₱100.00');
  const empty = context.openServiceRequestLedger();
  pending[2](response([]));
  await empty;
  assert.equal(nodes.srAccountLedgerBalance.textContent, '₱0.00');
  assert.match(nodes.srAccountLedgerMessage.textContent, /No billing entries/);
  const event = { currentTarget: {}, preventDefault: () => { fail = true; } };
  context.viewServiceRequestPdf(event);
  assert.equal(fail, false);
  assert.equal(event.currentTarget.href, 'blob:request-pdf');
  assert.ok(printed.includes('02-002'));
  assert.ok(printed.includes('All details'));
  assert.ok(printed.includes('ACCOMPLISHED BY'));
  assert.ok(printed.includes('RESULT / CUSTOMER COMMENT'));
  assert.ok(printed.includes('Signature of Customer'));
  const [formX, formY, formWidth, formHeight] = boxes[0];
  assert.ok(formX >= 18);
  assert.equal(formY, 18);
  assert.ok(formX + formWidth <= 595.28 - 18 + 0.001);
  assert.ok(formY + formHeight <= 841.89 / 2 - 18);
  assert.ok(Math.abs(formWidth / formHeight - 814 / 537) < 0.001);
  context.selectedServiceRequest.particulars = 'Long full details '.repeat(200);
  context.viewServiceRequestPdf(event);
  assert.ok(table.body.some(row => row[0] === 'Particulars' && row[1] === context.selectedServiceRequest.particulars));
  const broken = context.openServiceRequestLedger();
  pending[3]({ ok: false });
  await broken;
  assert.match(nodes.srAccountLedgerMessage.textContent, /Unable to load/);
  assert.equal(nodes.srAccountLedgerBalance.textContent, '—');
  context.selectedServiceRequest = { accountName: 'Non-customer' };
  await context.openServiceRequestLedger();
  assert.equal(pending.length, 4);
  context.window.jspdf = null;
  context.viewServiceRequestPdf(event);
  assert.equal(fail, true);
  assert.match(nodes.srDetailMessage.textContent, /PDF component is unavailable/);
  console.log('Passed: account-specific ledger, stale responses, escaping, empty and failed ledger, non-customer guard, portrait PDF in upper half with preserved proportions, account details, signature sections, long-content continuation, missing PDF component.');
})().catch(error => { console.error(error); process.exitCode = 1; });
