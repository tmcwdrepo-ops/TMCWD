// collection_sample.js
// MOCK DATA ONLY — all arrays in this file are stand-ins for real API responses.
// When backend endpoints are ready, remove the relevant const and replace each
// reference in collections.js with a fetch() call (TODO comments mark each spot).
// Do NOT add DOM logic, event listeners, or functions that manipulate the page here.

const COLLECTION_ACCOUNTS = [
  {
    invoiceNo: "CR007830",
    account: {
      accountNumber: "2026-001234",
      meterNumber: "MT-987654",
      name: "JUAN DELA CRUZ",
      address: "123 SAMPLE STREET, BARANGAY EXAMPLE, MARIKINA CITY",
      type: "RESIDENTIAL",
      status: "Connected",
      otherChargesBalance: 125.50
    },
    bills: [
      { month: "JAN 2026", dueDate: "Feb 15, 2026", amount: 850.00,  pca: 45.25, mmf: 0,     penalty: 0,     selected: false }, // PCA only
      { month: "FEB 2026", dueDate: "Mar 15, 2026", amount: 920.75,  pca: 0,     mmf: 15.00, penalty: 42.50, selected: false }, // MMF only
      { month: "MAR 2026", dueDate: "Apr 15, 2026", amount: 1125.25, pca: 48.50, mmf: 15.00, penalty: 56.25, selected: true  }, // Both
      { month: "APR 2026", dueDate: "May 15, 2026", amount: 995.50,  pca: 46.00, mmf: 0,     penalty: 0,     selected: false }  // PCA only
    ]
  },
  {
    invoiceNo: "CR007831",
    account: {
      accountNumber: "2026-005678",
      meterNumber: "MT-123456",
      name: "MARIA SANTOS",
      address: "456 MAIN AVENUE, BARANGAY CENTRO, QUEZON CITY",
      type: "RESIDENTIAL",
      status: "Connected",
      otherChargesBalance: 0
    },
    bills: [
      { month: "FEB 2026", dueDate: "Mar 15, 2026", amount: 675.00, pca: 0,     mmf: 15.00, penalty: 0,     selected: false }, // MMF only
      { month: "MAR 2026", dueDate: "Apr 15, 2026", amount: 720.50, pca: 36.75, mmf: 15.00, penalty: 33.75, selected: true  }, // Both
      { month: "APR 2026", dueDate: "May 15, 2026", amount: 810.25, pca: 0,     mmf: 15.00, penalty: 0,     selected: false }  // MMF only
    ]
  }
];

// ── Collection Report: Collector list ────────────────────────────────────────
// TODO: replace with fetch('/api/Billing/Collectors').then(r => r.json())
const SAMPLE_REPORT_COLLECTORS = [
  'Elmer Pangilinan',
  'Maria Santos',
  'Jose Reyes',
  'Ana Garcia',
  'Pedro Cruz'
];

// ── Collection Monitoring: rows ───────────────────────────────────────────────
// TODO: replace with fetch('/api/Billing/CollectionMonitoring').then(r => r.json())
// Expected shape: { collector, orFrom, orTo, cashOnHand }
const SAMPLE_COLLECTION_MONITORING = [
  { collector: 'Corina Castillo',  orFrom: 'CR0094308', orTo: 'CR0094331', cashOnHand: 91001.75  },
  { collector: 'Elmer Pangilinan', orFrom: 'CR0094201', orTo: 'CR0094250', cashOnHand: 108192.91 },
  { collector: 'Maria Santos',     orFrom: 'CR0094105', orTo: 'CR0094140', cashOnHand: 67500.50  }
];

// ── Batch Payments: upload log rows ──────────────────────────────────────────
// TODO: replace with fetch('/api/Billing/BatchPayments').then(r => r.json())
// Expected shape: { id, filename, channel, status, dateUploaded }
(function () {
  function daysAgo(n) {
    var d = new Date();
    d.setDate(d.getDate() - n);
    return d.toISOString().slice(0, 10);
  }
  var channels = ['ECPay','GCash','Maya'];
  var statuses = ['Processed','Pending','Processed'];
  var rows = [];
  for (var i = 0; i < 3; i++) {
    rows.push({
      id:           'TMCWD-RAWLOGS',
      filename:     daysAgo(i) + '.csv',
      channel:      channels[i],
      status:       statuses[i],
      dateUploaded: daysAgo(i)
    });
  }
  window.SAMPLE_BATCH_PAYMENTS = rows;
})();

// ── My Collection: sample rows ────────────────────────────────────────────────
// TODO: replace with fetch('/api/Billing/MyCollection?date=<date>').then(r => r.json())
// Expected shape: { invoiceNo, account, totalAmount, transactionType }
const SAMPLE_COLLECTION_LIST = [
  { invoiceNo: 'CR0094201', account: '2026-001234 — JUAN DELA CRUZ',          totalAmount: 1250.00, transactionType: 'Cash'   },
  { invoiceNo: 'CR0094202', account: '2026-005678 — MARIA SANTOS',             totalAmount:  875.50, transactionType: 'Cash'   },
  { invoiceNo: 'CR0094203', account: '2026-009101 — PEDRO REYES',              totalAmount: 2340.75, transactionType: 'Check'  },
  { invoiceNo: 'CR0094204', account: '2026-011213 — ANA GARCIA',               totalAmount:  560.00, transactionType: 'Online' },
  { invoiceNo: 'CR0094205', account: '2026-014151 — JOSE PANGILINAN',          totalAmount: 1875.25, transactionType: 'Cash'   }
];

// ── Invoice Search: sample invoice records ───────────────────────────────────
// TODO: replace with fetch('/api/Billing/InvoiceSearch?query=<term>').then(r => r.json())
// Expected shape: { invoiceNo, account, totalAmount, transactionType, dateIssued, status }
const SAMPLE_INVOICE_SEARCH = [
  { invoiceNo: 'INV-00123', account: 'JUAN DELA CRUZ',      totalAmount: 1250.00, transactionType: 'Cash',   dateIssued: '2026-07-30', status: 'Unpaid' },
  { invoiceNo: 'INV-00125', account: 'MARIA SANTOS',        totalAmount:  875.50, transactionType: 'Check',  dateIssued: '2026-07-29', status: 'Paid'   },
  { invoiceNo: 'INV-00127', account: 'PEDRO REYES',         totalAmount: 2340.75, transactionType: 'Online', dateIssued: '2026-07-28', status: 'Unpaid' },
  { invoiceNo: 'INV-00130', account: 'ANA GARCIA',          totalAmount:  560.00, transactionType: 'Cash',   dateIssued: '2026-07-27', status: 'Paid'   },
  { invoiceNo: 'INV-00132', account: 'JOSE PANGILINAN',     totalAmount: 1875.25, transactionType: 'Check',  dateIssued: '2026-07-26', status: 'Unpaid' },
  { invoiceNo: 'INV-00135', account: 'CORINA CASTILLO',     totalAmount: 3120.00, transactionType: 'Online', dateIssued: '2026-07-25', status: 'Paid'   },
  { invoiceNo: 'INV-00138', account: 'ELMER PANGILINAN',    totalAmount:  920.75, transactionType: 'Cash',   dateIssued: '2026-07-24', status: 'Unpaid' },
  { invoiceNo: 'INV-00140', account: 'RICARDO FERNANDEZ',   totalAmount: 1450.50, transactionType: 'Check',  dateIssued: '2026-07-23', status: 'Paid'   }
];


// ── Re-Print Invoice: sample records ─────────────────────────────────────────
// MOCK DATA ONLY — replace with a real API call in production, e.g.:
//   fetch('/api/Billing/Invoice?invoiceNo=' + encodeURIComponent(term))
//     .then(function (r) { if (!r.ok) throw new Error(r.statusText); return r.json(); })
//
// Invoice numbers are intentionally varied (different prefixes, lengths, gaps)
// so that exact-match lookup in Phase 3 can be tested against both valid entries
// (e.g. "CR0094201", "OR-2026-0047", "INV00830") and invalid ones (anything else).
//
// Expected shape per record:
//   { invoiceNo, account, totalAmount, transactionType,
//     dateIssued, status, items: [{ description, amount }] }
//
// NOTE: no `export` keyword — this file is loaded via a plain <script> tag
// (see Collections.cshtml @section Scripts), so the array is a browser global.
const reprintInvoiceSampleData = [
  // ── Valid entries ── (use these to test a successful lookup)
  {
    invoiceNo:       'CR0094201',          // standard CR-series receipt
    account:         'JUAN DELA CRUZ',
    totalAmount:     1250.00,
    transactionType: 'Cash',
    dateIssued:      '2026-07-30',
    status:          'Paid',
    items: [
      { description: 'Water Bill \u2013 July 2026',   amount: 1250.00 }
    ]
  },
  {
    invoiceNo:       'CR0094215',          // gap in sequence (tests non-sequential lookup)
    account:         'MARIA SANTOS',
    totalAmount:      875.50,
    transactionType: 'Cash',
    dateIssued:      '2026-07-30',
    status:          'Paid',
    items: [
      { description: 'Water Bill \u2013 July 2026',   amount:  860.50 },
      { description: 'Reconnection Fee',               amount:   15.00 }
    ]
  },
  {
    invoiceNo:       'OR-2026-0047',       // different prefix/format
    account:         'PEDRO REYES',
    totalAmount:     2340.75,
    transactionType: 'Check',
    dateIssued:      '2026-07-29',
    status:          'Paid',
    items: [
      { description: 'Water Bill \u2013 June 2026',   amount: 1120.00 },
      { description: 'Water Bill \u2013 July 2026',   amount: 1165.50 },
      { description: 'Penalty',                        amount:   55.25 }
    ]
  },
  {
    invoiceNo:       'INV00830',           // short alphanumeric, no separators
    account:         'ANA GARCIA',
    totalAmount:      560.00,
    transactionType: 'Online',
    dateIssued:      '2026-07-28',
    status:          'Paid',
    items: [
      { description: 'Water Bill \u2013 July 2026',   amount:  560.00 }
    ]
  },
  {
    invoiceNo:       'CR0094250',          // higher CR number
    account:         'JOSE PANGILINAN',
    totalAmount:     1875.25,
    transactionType: 'Cash',
    dateIssued:      '2026-07-27',
    status:          'Paid',
    items: [
      { description: 'Water Bill \u2013 May 2026',    amount:  620.00 },
      { description: 'Water Bill \u2013 June 2026',   amount:  635.75 },
      { description: 'Water Bill \u2013 July 2026',   amount:  580.00 },
      { description: 'Penalty',                        amount:   39.50 }
    ]
  },
  {
    invoiceNo:       'OR-2026-0112',       // second OR-series, later number
    account:         'CORINA CASTILLO',
    totalAmount:     3120.00,
    transactionType: 'Online',
    dateIssued:      '2026-07-26',
    status:          'Paid',
    items: [
      { description: 'Water Bill \u2013 July 2026',   amount: 2980.00 },
      { description: 'Meter Maintenance Fee',          amount:   15.00 },
      { description: 'Power Cost Adjustment',          amount:  125.00 }
    ]
  },
  {
    invoiceNo:       'CR009-4207',         // hyphen variant (tests normalisation later)
    account:         'ELMER PANGILINAN',
    totalAmount:      920.75,
    transactionType: 'Cash',
    dateIssued:      '2026-07-25',
    status:          'Paid',
    items: [
      { description: 'Water Bill \u2013 July 2026',   amount:  878.25 },
      { description: 'Penalty',                        amount:   42.50 }
    ]
  },
  {
    invoiceNo:       'REC-20260724-001',   // date-stamped receipt format
    account:         'RICARDO FERNANDEZ',
    totalAmount:     1450.50,
    transactionType: 'Check',
    dateIssued:      '2026-07-24',
    status:          'Paid',
    items: [
      { description: 'Water Bill \u2013 July 2026',   amount: 1435.50 },
      { description: 'Reconnection Fee',               amount:   15.00 }
    ]
  }
];
