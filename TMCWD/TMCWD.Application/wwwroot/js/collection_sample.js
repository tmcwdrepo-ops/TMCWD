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
      otherChargesBalance: 125.50,
      otherCharges: [
        { chargeType: "Service Connection Fee", payableIn: "1 Month",  particulars: "New connection",      amount:  75.00 },
        { chargeType: "Meter Deposit",          payableIn: "Lump Sum", particulars: "Standard meter",      amount:  50.50 }
      ]
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
      otherChargesBalance: 0,
      otherCharges: []
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

// ── Cancel Invoice: cancellation reasons ─────────────────────────────────────
// TODO: replace with fetch('/api/Billing/CancelReasons').then(r => r.json())
// Used by initCancelInvoiceModal() in collections.js.
const SAMPLE_CANCEL_INVOICE_REASONS = [
  'Incorrect Amount',
  'Duplicate Invoice',
  'Wrong Account',
  'Customer Request',
  'Data Entry Error',
  'Payment Already Posted',
  'Meter Reading Error',
  'Other'
];

// ── Other Charges: Charge Type list ──────────────────────────────────────────
// TODO: replace with fetch('/api/Billing/OtherChargeTypes').then(r => r.json())
// Expected shape per item: { id, label, defaultAmount }
// defaultAmount is pre-filled into the Amount input when the type is selected.
const OC_CHARGE_TYPES = [
  { id: 'SCF',  label: 'Service Connection Fee',    defaultAmount:  500.00 },
  { id: 'MD',   label: 'Meter Deposit',              defaultAmount:  300.00 },
  { id: 'RF',   label: 'Reconnection Fee',           defaultAmount:  250.00 },
  { id: 'MF',   label: 'Meter/Fitting',              defaultAmount:  150.00 },
  { id: 'PEN',  label: 'Penalty',                    defaultAmount:    0.00 },
  { id: 'AD',   label: 'Account Deposit',            defaultAmount: 1000.00 },
  { id: 'TP',   label: 'Tap/Pipe Fee',               defaultAmount:  200.00 },
  { id: 'OTH',  label: 'Other',                      defaultAmount:    0.00 }
];

// ── Other Charges: Account lookup records ─────────────────────────────────────
// Extended version of COLLECTION_ACCOUNTS for the OC modal lookup.
// Each entry adds a TIN field alongside the existing name / address.
// TODO: replace with fetch('/api/Billing/AccountLookup?q=<term>').then(r => r.json())
// Expected shape per record:
//   { accountNumber, name, tin, address }
const OC_ACCOUNT_LOOKUP = [
  {
    accountNumber: '2026-001234',
    name:    'JUAN DELA CRUZ',
    tin:     '123-456-789-000',
    address: '123 SAMPLE STREET, BARANGAY EXAMPLE, MARIKINA CITY',
    otherCharges: [
      { chargeType: 'Service Connection Fee', payableIn: 'Lump Sum',  particulars: 'New service connection',      amount:  500.00 },
      { chargeType: 'Meter Deposit',          payableIn: 'Lump Sum',  particulars: 'Meter deposit - 1/2 inch',    amount:  300.00 }
    ]
  },
  {
    accountNumber: '2026-005678',
    name:    'MARIA SANTOS',
    tin:     '987-654-321-000',
    address: '456 MAIN AVENUE, BARANGAY CENTRO, QUEZON CITY',
    otherCharges: [
      { chargeType: 'Reconnection Fee', payableIn: 'Lump Sum',  particulars: 'Account disconnected Aug 26',  amount:  250.00 },
      { chargeType: 'Penalty',          payableIn: '1 Month',   particulars: 'Late payment penalty',         amount:   85.00 }
    ]
  },
  {
    accountNumber: '2026-009101',
    name:    'PEDRO REYES',
    tin:     '111-222-333-000',
    address: '789 RIZAL BOULEVARD, BARANGAY BAGONG ILOG, PASIG CITY',
    otherCharges: [
      { chargeType: 'Tap/Pipe Fee',    payableIn: '3 Months', particulars: 'Tap installation - PVC',    amount:  200.00 },
      { chargeType: 'Meter/Fitting',   payableIn: 'Lump Sum', particulars: 'Replaced damaged meter',    amount:  150.00 },
      { chargeType: 'Account Deposit', payableIn: '6 Months', particulars: 'Initial account deposit',   amount: 1000.00 }
    ]
  },
  {
    accountNumber: '2026-011213',
    name:    'ANA GARCIA',
    tin:     '444-555-666-000',
    address: '321 COMMONWEALTH AVE, BARANGAY HOLY SPIRIT, QUEZON CITY',
    otherCharges: [
      { chargeType: 'Penalty', payableIn: '1 Month',  particulars: 'Late payment - July',  amount:  50.00 },
      { chargeType: 'Other',   payableIn: 'Lump Sum', particulars: 'Miscellaneous charge', amount:  75.00 }
    ]
  },
  {
    accountNumber: '2026-014151',
    name:    'JOSE PANGILINAN',
    tin:     '777-888-999-000',
    address: '654 ORTIGAS AVE, BARANGAY KAPITOLYO, PASIG CITY',
    otherCharges: [
      { chargeType: 'Service Connection Fee', payableIn: 'Lump Sum',  particulars: 'Upgraded connection',      amount:  500.00 },
      { chargeType: 'Reconnection Fee',       payableIn: 'Lump Sum',  particulars: 'Reconnected after cutoff', amount:  250.00 },
      { chargeType: 'Meter Deposit',          payableIn: '3 Months',  particulars: 'New meter installation',   amount:  300.00 }
    ]
  }
];

// ── Teller / Collector list ───────────────────────────────────────────────────
// TODO: replace with fetch('/api/Billing/Tellers').then(r => r.json())
const SAMPLE_TELLERS = [
  'Elmer Pangilinan',
  'Maria Santos',
  'Jose Reyes',
  'Ana Garcia',
  'Pedro Cruz',
  'ONLINE PAYMENT SERVICE'
];

// ── NSC Payment: Service Applications list ────────────────────────────────────
// TODO: replace with fetch('/api/Billing/NscPayments').then(r => r.json())
// Shape per record: { orNumber, applicationRef, name, date, label }
const NSC_PAYMENT_APPLICATIONS = [
  { orNumber: '20260009395', applicationRef: '17-011444', name: 'SANTOS, MILDRED D.',       date: 'Jul 23, 2026', label: 'For NSC Payment' },
  { orNumber: '20260009318', applicationRef: '15-034185', name: 'SANTOS, ROLLIE ROMAN S.',  date: 'Jul 22, 2026', label: 'For NSC Payment' },
  { orNumber: '20260009335', applicationRef: '13-090379', name: 'CORPUZ, JOHN MICHAEL G.',  date: 'Jul 22, 2026', label: 'For NSC Payment' },
  { orNumber: '20260009215', applicationRef: '11-090369', name: 'POLEROS, VINCENT',         date: 'Jul 21, 2026', label: 'For NSC Payment' },
  { orNumber: '20260009185', applicationRef: '01-011253', name: 'PANGANIBAN, WERLYN',       date: 'Jul 20, 2026', label: 'For NSC Payment' },
  { orNumber: '20260008670', applicationRef: '08-050559', name: 'PANGANDAMAN, AL-SADDAM R.',date: 'Jul 14, 2026', label: 'For NSC Payment' },
  { orNumber: '20260008541', applicationRef: '12-070221', name: 'REYES, JOSE ANTONIO C.',   date: 'Jul 12, 2026', label: 'For NSC Payment' },
  { orNumber: '20260008399', applicationRef: '09-031102', name: 'DELA CRUZ, MARIA FE T.',   date: 'Jul 10, 2026', label: 'For NSC Payment' },
  { orNumber: '20260008210', applicationRef: '16-044567', name: 'GARCIA, ROBERTO JR. L.',   date: 'Jul 8, 2026',  label: 'For NSC Payment' },
  { orNumber: '20260007985', applicationRef: '05-019834', name: 'VILLANUEVA, AGNES P.',     date: 'Jul 5, 2026',  label: 'For NSC Payment' }
];

// ── Bank list (used by Check Payment fields) ─────────────────────────────────
// TODO: replace with fetch('/api/Billing/Banks').then(r => r.json())
const SAMPLE_BANKS = [
  'BDO', 'BPI', 'Metrobank', 'PNB', 'Landbank',
  'DBP', 'RCBC', 'Unionbank', 'China Bank', 'Security Bank'
];

// Collectors list (used by Remittance modal collector dropdown)
// TODO: replace with fetch('/api/Billing/Collectors').then(r => r.json())
const SAMPLE_COLLECTORS = [
  'Elmer Pangilinan',
  'Maria Santos',
  'Jose Reyes',
  'Ana Garcia',
  'Pedro Cruz'
];

// ── Payable In options (Other Charges entry modal) ────────────────────────────
// TODO: replace with fetch('/api/Billing/PayableInOptions').then(r => r.json())
const SAMPLE_PAYABLE_IN = [
  'Lump Sum',
  '1 Month',
  '2 Months',
  '3 Months',
  '6 Months',
  '12 Months'
];

// ── Other Charges Entry: mock charge records ──────────────────────────────────
// Used to pre-populate the oce-* table when an account is selected.
// TODO: replace with fetch('/api/Billing/OtherCharges?accountNo=' + accountNumber)
// Shape per record: { chargeType, payableIn, particulars, amount }
const SAMPLE_OTHER_CHARGES = [
  { chargeType: 'Service Connection Fee', payableIn: 'Lump Sum',  particulars: 'New service connection',     amount:  500.00 },
  { chargeType: 'Meter Deposit',          payableIn: 'Lump Sum',  particulars: 'Meter deposit - 1/2 inch',   amount:  300.00 },
  { chargeType: 'Reconnection Fee',       payableIn: 'Lump Sum',  particulars: 'Account disconnected Aug 26', amount: 250.00 },
  { chargeType: 'Penalty',               payableIn: '1 Month',   particulars: 'Late payment penalty',        amount:   85.00 },
  { chargeType: 'Tap/Pipe Fee',           payableIn: '3 Months',  particulars: 'Tap installation - PVC',      amount:  200.00 },
  { chargeType: 'Account Deposit',        payableIn: '6 Months',  particulars: 'Initial account deposit',     amount: 1000.00 },
  { chargeType: 'Meter/Fitting',          payableIn: 'Lump Sum',  particulars: 'Replaced damaged meter',      amount:  150.00 },
  { chargeType: 'Other',                  payableIn: '12 Months', particulars: 'Miscellaneous charge',        amount:   75.00 }
];

// ── Ledger: account ledger data ───────────────────────────────────────────────
// TODO: replace with fetch('/api/Billing/Ledger?accountNo=' + accountNo)
//
// Shape per record:
//   accountNo, accountName, address, rateCode, meterNo, status, totalBalance
//   bills:        [{ refNo, timestamp, particulars, prev, pres, usage,
//                    debit, credit, balance, processedBy }]
//   otherCharges: same row shape, populated in a later phase
//
// status values: 'ACTIVE' | 'INACTIVE' | 'DISCONNECTED'
// prev/pres/usage are null for non-consumption rows (adjustments, payments, OC)
//
// getLedgerByAccountNo(accountNo) — helper to look up a record by account number

const LEDGER_DATA = [

  // ── Account 1: CHUA, LEVIS — active, zero balance, mix of bill/adj/payment rows
  {
    accountNo:    '12-090005',
    accountName:  'CHUA, LEVIS',
    address:      '12-090005 PACIFIC TOWN EXECUTIVE HOMES',
    rateCode:     '012',
    meterNo:      '004357-25',
    status:       'ACTIVE',
    totalBalance: 0.00,
    bills: [
      {
        refNo:        'CR0014723',
        timestamp:    '7-25-26 10:07:00',
        particulars:  'Power Cost Adjustment (Jul 2026) 598C75BF42BE',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        27.00,
        credit:       0.00,
        balance:      27.00,
        processedBy:  'Michelle Patanindagat - ECPay'
      },
      {
        refNo:        'CR0014723',
        timestamp:    '7-25-26 10:07:00',
        particulars:  'Meter Maintenance Fee (Jul 2026) 598C75BF42BE',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        15.00,
        credit:       0.00,
        balance:      42.00,
        processedBy:  'Michelle Patanindagat - ECPay'
      },
      {
        refNo:        'CR0014723',
        timestamp:    '7-25-26 10:07:00',
        particulars:  'Payment (Jul 2026) 598C75BF42BE',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        0.00,
        credit:       42.00,
        balance:      0.00,
        processedBy:  'Michelle Patanindagat - ECPay'
      },
      {
        refNo:        'CR0014501',
        timestamp:    '6-25-26 09:15:00',
        particulars:  'Water Bill (Jun 2026)',
        prev:         1204,
        pres:         1231,
        usage:        27,
        debit:        312.50,
        credit:       0.00,
        balance:      312.50,
        processedBy:  'Elmer Pangilinan'
      },
      {
        refNo:        'CR0014502',
        timestamp:    '6-30-26 14:22:00',
        particulars:  'Payment (Jun 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        0.00,
        credit:       312.50,
        balance:      0.00,
        processedBy:  'Elmer Pangilinan'
      },
      {
        refNo:        'CR0014210',
        timestamp:    '5-25-26 08:45:00',
        particulars:  'Water Bill (May 2026)',
        prev:         1178,
        pres:         1204,
        usage:        26,
        debit:        298.75,
        credit:       0.00,
        balance:      298.75,
        processedBy:  'Elmer Pangilinan'
      },
      {
        refNo:        'CR0014211',
        timestamp:    '5-30-26 11:05:00',
        particulars:  'Payment (May 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        0.00,
        credit:       298.75,
        balance:      0.00,
        processedBy:  'Elmer Pangilinan'
      }
    ],
    otherCharges: []
  },

  // ── Account 2: DELA CRUZ, MARIA FE T. — active, with outstanding balance
  {
    accountNo:    '09-031102',
    accountName:  'DELA CRUZ, MARIA FE T.',
    address:      '09-031102 BRGY. SAN JOSE, VALENZUELA CITY',
    rateCode:     '009',
    meterNo:      '007821-23',
    status:       'ACTIVE',
    totalBalance: 1245.75,
    bills: [
      {
        refNo:        'CR0014800',
        timestamp:    '7-15-26 08:30:00',
        particulars:  'Water Bill (Jul 2026)',
        prev:         3410,
        pres:         3455,
        usage:        45,
        debit:        485.50,
        credit:       0.00,
        balance:      1245.75,
        processedBy:  'Maria Santos'
      },
      {
        refNo:        'CR0014801',
        timestamp:    '7-15-26 08:30:00',
        particulars:  'Power Cost Adjustment (Jul 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        38.25,
        credit:       0.00,
        balance:      760.25,
        processedBy:  'Maria Santos'
      },
      {
        refNo:        'CR0014650',
        timestamp:    '6-20-26 10:10:00',
        particulars:  'Payment (Jun 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        0.00,
        credit:       522.00,
        balance:      722.00,
        processedBy:  'Elmer Pangilinan'
      },
      {
        refNo:        'CR0014620',
        timestamp:    '6-15-26 09:00:00',
        particulars:  'Water Bill (Jun 2026)',
        prev:         3365,
        pres:         3410,
        usage:        45,
        debit:        468.75,
        credit:       0.00,
        balance:      1244.00,
        processedBy:  'Maria Santos'
      },
      {
        refNo:        'CR0014621',
        timestamp:    '6-15-26 09:00:00',
        particulars:  'Meter Maintenance Fee (Jun 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        15.00,
        credit:       0.00,
        balance:      775.25,
        processedBy:  'Maria Santos'
      },
      {
        refNo:        'CR0014400',
        timestamp:    '5-18-26 13:45:00',
        particulars:  'Payment (May 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        0.00,
        credit:       490.00,
        balance:      760.25,
        processedBy:  'Elmer Pangilinan'
      },
      {
        refNo:        'CR0014370',
        timestamp:    '5-15-26 08:20:00',
        particulars:  'Water Bill (May 2026)',
        prev:         3322,
        pres:         3365,
        usage:        43,
        debit:        451.25,
        credit:       0.00,
        balance:      1250.25,
        processedBy:  'Maria Santos'
      },
      {
        refNo:        'CR0014100',
        timestamp:    '4-22-26 11:30:00',
        particulars:  'Payment (Apr 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        0.00,
        credit:       455.75,
        balance:      799.00,
        processedBy:  'Elmer Pangilinan'
      }
    ],
    otherCharges: []
  },

  // ── Account 3: REYES, JOSE ANTONIO C. — disconnected, high balance
  {
    accountNo:    '12-070221',
    accountName:  'REYES, JOSE ANTONIO C.',
    address:      '12-070221 BRGY. BAGONG ILOG, PASIG CITY',
    rateCode:     '012',
    meterNo:      '003145-22',
    status:       'DISCONNECTED',
    totalBalance: 4820.50,
    bills: [
      {
        refNo:        'CR0013980',
        timestamp:    '4-15-26 09:00:00',
        particulars:  'Water Bill (Apr 2026)',
        prev:         8801,
        pres:         8862,
        usage:        61,
        debit:        720.25,
        credit:       0.00,
        balance:      4820.50,
        processedBy:  'Maria Santos'
      },
      {
        refNo:        'CR0013981',
        timestamp:    '4-15-26 09:00:00',
        particulars:  'Penalty (Apr 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        144.05,
        credit:       0.00,
        balance:      4100.25,
        processedBy:  'Maria Santos'
      },
      {
        refNo:        'CR0013700',
        timestamp:    '3-15-26 08:45:00',
        particulars:  'Water Bill (Mar 2026)',
        prev:         8740,
        pres:         8801,
        usage:        61,
        debit:        710.50,
        credit:       0.00,
        balance:      3956.20,
        processedBy:  'Maria Santos'
      },
      {
        refNo:        'CR0013701',
        timestamp:    '3-15-26 08:45:00',
        particulars:  'Penalty (Mar 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        142.10,
        credit:       0.00,
        balance:      3245.70,
        processedBy:  'Maria Santos'
      },
      {
        refNo:        'CR0013420',
        timestamp:    '2-15-26 10:00:00',
        particulars:  'Water Bill (Feb 2026)',
        prev:         8678,
        pres:         8740,
        usage:        62,
        debit:        695.75,
        credit:       0.00,
        balance:      3103.60,
        processedBy:  'Jose Reyes'
      },
      {
        refNo:        'CR0013421',
        timestamp:    '2-15-26 10:00:00',
        particulars:  'Penalty (Feb 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        139.15,
        credit:       0.00,
        balance:      2407.85,
        processedBy:  'Jose Reyes'
      },
      {
        refNo:        'CR0013100',
        timestamp:    '1-15-26 09:30:00',
        particulars:  'Water Bill (Jan 2026)',
        prev:         8615,
        pres:         8678,
        usage:        63,
        debit:        680.00,
        credit:       0.00,
        balance:      2268.70,
        processedBy:  'Jose Reyes'
      },
      {
        refNo:        'CR0013101',
        timestamp:    '1-15-26 09:30:00',
        particulars:  'Penalty (Jan 2026)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        136.00,
        credit:       0.00,
        balance:      1588.70,
        processedBy:  'Jose Reyes'
      },
      {
        refNo:        'CR0013050',
        timestamp:    '1-08-26 14:00:00',
        particulars:  'Last Payment (Dec 2025)',
        prev:         null,
        pres:         null,
        usage:        null,
        debit:        0.00,
        credit:       652.00,
        balance:      1452.70,
        processedBy:  'Jose Reyes'
      }
    ],
    otherCharges: []
  },

  // ── Account 4: VILLANUEVA, AGNES P. — inactive, zero balance (empty-state test)
  {
    accountNo:    '05-019834',
    accountName:  'VILLANUEVA, AGNES P.',
    address:      '05-019834 BRGY. BATASAN HILLS, QUEZON CITY',
    rateCode:     '005',
    meterNo:      '009012-24',
    status:       'INACTIVE',
    totalBalance: 0.00,
    bills:        [],
    otherCharges: []
  },

  // ── Account 5: JUAN DELA CRUZ — matches COLLECTION_ACCOUNTS[0] (accountNumber 2026-001234)
  {
    accountNo:    '2026-001234',
    accountName:  'JUAN DELA CRUZ',
    address:      '123 SAMPLE STREET, BARANGAY EXAMPLE, MARIKINA CITY',
    rateCode:     '012',
    meterNo:      'MT-987654',
    status:       'ACTIVE',
    totalBalance: 1239.50,
    bills: [
      {
        refNo:       'CR007830',
        timestamp:   '4-15-26 09:00:00',
        particulars: 'Water Bill (Apr 2026)',
        prev:        1380,
        pres:        1412,
        usage:       32,
        debit:       995.50,
        credit:      0.00,
        balance:     1239.50,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007830',
        timestamp:   '4-15-26 09:00:00',
        particulars: 'Power Cost Adjustment (Apr 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       46.00,
        credit:      0.00,
        balance:     244.00,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007821',
        timestamp:   '3-12-26 10:30:00',
        particulars: 'Payment (Mar 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       0.00,
        credit:      1245.50,
        balance:     198.00,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007815',
        timestamp:   '3-15-26 08:45:00',
        particulars: 'Water Bill (Mar 2026)',
        prev:        1332,
        pres:        1380,
        usage:       48,
        debit:       1125.25,
        credit:      0.00,
        balance:     1443.50,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007815',
        timestamp:   '3-15-26 08:45:00',
        particulars: 'Power Cost Adjustment (Mar 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       48.50,
        credit:      0.00,
        balance:     318.25,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007815',
        timestamp:   '3-15-26 08:45:00',
        particulars: 'Meter Maintenance Fee (Mar 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       15.00,
        credit:      0.00,
        balance:     269.75,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007815',
        timestamp:   '3-15-26 08:45:00',
        particulars: 'Penalty (Mar 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       56.25,
        credit:      0.00,
        balance:     254.75,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007805',
        timestamp:   '2-10-26 11:00:00',
        particulars: 'Payment (Feb 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       0.00,
        credit:      978.25,
        balance:     198.50,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007800',
        timestamp:   '2-15-26 09:00:00',
        particulars: 'Water Bill (Feb 2026)',
        prev:        1291,
        pres:        1332,
        usage:       41,
        debit:       920.75,
        credit:      0.00,
        balance:     1176.75,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007800',
        timestamp:   '2-15-26 09:00:00',
        particulars: 'Meter Maintenance Fee (Feb 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       15.00,
        credit:      0.00,
        balance:     256.00,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007800',
        timestamp:   '2-15-26 09:00:00',
        particulars: 'Penalty (Feb 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       42.50,
        credit:      0.00,
        balance:     241.00,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007790',
        timestamp:   '1-12-26 10:15:00',
        particulars: 'Payment (Jan 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       0.00,
        credit:      895.25,
        balance:     198.50,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007785',
        timestamp:   '1-15-26 08:30:00',
        particulars: 'Water Bill (Jan 2026)',
        prev:        1246,
        pres:        1291,
        usage:       45,
        debit:       850.00,
        credit:      0.00,
        balance:     1093.75,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007785',
        timestamp:   '1-15-26 08:30:00',
        particulars: 'Power Cost Adjustment (Jan 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       45.25,
        credit:      0.00,
        balance:     243.75,
        processedBy: 'Elmer Pangilinan'
      }
    ],
    otherCharges: [
      {
        refNo:       'OC-2026-001',
        timestamp:   '1-05-26 14:00:00',
        particulars: 'Service Connection Fee',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       75.00,
        credit:      0.00,
        balance:     125.50,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'OC-2026-002',
        timestamp:   '1-05-26 14:00:00',
        particulars: 'Meter Deposit',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       50.50,
        credit:      0.00,
        balance:     50.50,
        processedBy: 'Elmer Pangilinan'
      }
    ]
  },

  // ── Account 6: MARIA SANTOS — matches COLLECTION_ACCOUNTS[1] (accountNumber 2026-005678)
  {
    accountNo:    '2026-005678',
    accountName:  'MARIA SANTOS',
    address:      '456 MAIN AVENUE, BARANGAY CENTRO, QUEZON CITY',
    rateCode:     '009',
    meterNo:      'MT-123456',
    status:       'ACTIVE',
    totalBalance: 810.25,
    bills: [
      {
        refNo:       'CR007831',
        timestamp:   '4-15-26 09:30:00',
        particulars: 'Water Bill (Apr 2026)',
        prev:        2580,
        pres:        2614,
        usage:       34,
        debit:       810.25,
        credit:      0.00,
        balance:     810.25,
        processedBy: 'Maria Santos'
      },
      {
        refNo:       'CR007831',
        timestamp:   '4-15-26 09:30:00',
        particulars: 'Meter Maintenance Fee (Apr 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       15.00,
        credit:      0.00,
        balance:     0.00,
        processedBy: 'Maria Santos'
      },
      {
        refNo:       'CR007820',
        timestamp:   '3-20-26 11:45:00',
        particulars: 'Payment (Mar 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       0.00,
        credit:      790.00,
        balance:     0.00,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007816',
        timestamp:   '3-15-26 09:00:00',
        particulars: 'Water Bill (Mar 2026)',
        prev:        2543,
        pres:        2580,
        usage:       37,
        debit:       720.50,
        credit:      0.00,
        balance:     790.00,
        processedBy: 'Maria Santos'
      },
      {
        refNo:       'CR007816',
        timestamp:   '3-15-26 09:00:00',
        particulars: 'Power Cost Adjustment (Mar 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       36.75,
        credit:      0.00,
        balance:     69.50,
        processedBy: 'Maria Santos'
      },
      {
        refNo:       'CR007816',
        timestamp:   '3-15-26 09:00:00',
        particulars: 'Meter Maintenance Fee (Mar 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       15.00,
        credit:      0.00,
        balance:     32.75,
        processedBy: 'Maria Santos'
      },
      {
        refNo:       'CR007816',
        timestamp:   '3-15-26 09:00:00',
        particulars: 'Penalty (Mar 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       33.75,
        credit:      0.00,
        balance:     17.75,
        processedBy: 'Maria Santos'
      },
      {
        refNo:       'CR007802',
        timestamp:   '2-18-26 10:00:00',
        particulars: 'Payment (Feb 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       0.00,
        credit:      690.00,
        balance:     0.00,
        processedBy: 'Elmer Pangilinan'
      },
      {
        refNo:       'CR007798',
        timestamp:   '2-15-26 08:50:00',
        particulars: 'Water Bill (Feb 2026)',
        prev:        2507,
        pres:        2543,
        usage:       36,
        debit:       675.00,
        credit:      0.00,
        balance:     690.00,
        processedBy: 'Maria Santos'
      },
      {
        refNo:       'CR007798',
        timestamp:   '2-15-26 08:50:00',
        particulars: 'Meter Maintenance Fee (Feb 2026)',
        prev:        null,
        pres:        null,
        usage:       null,
        debit:       15.00,
        credit:      0.00,
        balance:     15.00,
        processedBy: 'Maria Santos'
      }
    ],
    otherCharges: []
  }

];



// ── Ledger: pagination stress-test account (50 rows) ─────────────────────────
// Account 7: GARCIA, ROBERTO JR. L. — 50 generated rows to exercise pagination.
// Do NOT remove — used by Phase 5 testing checklist.
(function () {
  var months = [
    'Jan','Feb','Mar','Apr','May','Jun',
    'Jul','Aug','Sep','Oct','Nov','Dec'
  ];
  var entries = [];
  var runBal  = 0;
  var prevRead = 5000;

  for (var i = 0; i < 25; i++) {
    var year  = i < 13 ? 2025 : 2026;
    var month = months[(24 - i) % 12];
    var usage = 28 + (i % 7);
    var bill  = parseFloat((usage * 11.25 + 85).toFixed(2));
    var pca   = parseFloat((bill * 0.04).toFixed(2));
    var pres  = prevRead + usage;

    // Bill row
    runBal = parseFloat((runBal + bill).toFixed(2));
    entries.push({
      refNo:       'CR' + String(9000 + i * 2).padStart(7, '0'),
      timestamp:   '15-' + (((24 - i) % 12) + 1) + '-' + String(year).slice(2) + ' 08:30:00',
      particulars: 'Water Bill (' + month + ' ' + year + ')',
      prev:        prevRead,
      pres:        pres,
      usage:       usage,
      debit:       bill,
      credit:      0.00,
      balance:     runBal,
      processedBy: 'Maria Santos'
    });

    // PCA row
    runBal = parseFloat((runBal + pca).toFixed(2));
    entries.push({
      refNo:       'CR' + String(9000 + i * 2).padStart(7, '0'),
      timestamp:   '15-' + (((24 - i) % 12) + 1) + '-' + String(year).slice(2) + ' 08:30:00',
      particulars: 'Power Cost Adjustment (' + month + ' ' + year + ')',
      prev:        null,
      pres:        null,
      usage:       null,
      debit:       pca,
      credit:      0.00,
      balance:     runBal,
      processedBy: 'Maria Santos'
    });

    prevRead = pres;
  }

  LEDGER_DATA.push({
    accountNo:    '16-044567',
    accountName:  'GARCIA, ROBERTO JR. L.',
    address:      '16-044567 BRGY. KAPITOLYO, PASIG CITY',
    rateCode:     '016',
    meterNo:      '002218-21',
    status:       'ACTIVE',
    totalBalance: runBal,
    bills:        entries,
    otherCharges: []
  });
}());
