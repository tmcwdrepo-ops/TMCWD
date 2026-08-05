/**
 * Mock account details per reading sheet.
 * Key = reading sheet id, value = array of account rows.
 */
const READING_SHEET_DETAILS_DATA = {
  1: {
    billingDate: 'Jul 01, 2025', dueDate: 'Jul 15, 2025', disconDate: 'Aug 01, 2025',
    accounts: [
      { name: 'Maria Santos',     code: '01-010001', number: '250700001', prev: 1120, pres: 1198, usage: 78,  trend: 'up',     balance: 120.00, amount: 312.00, total: 432.00,  status: 'For Posting', category: 'normal'   },
      { name: 'Roberto Reyes',    code: '01-010002', number: '250700002', prev: 840,  pres: 895,  usage: 55,  trend: 'down',   balance: 0.00,   amount: 220.00, total: 220.00,  status: 'Posted',      category: 'normal'   },
      { name: 'Carlo Mendoza',    code: '01-010003', number: '250700003', prev: 560,  pres: 730,  usage: 170, trend: 'up',     balance: 480.00, amount: 680.00, total: 1160.00, status: 'Abnormal',    category: 'abnormal' },
      { name: 'Dante Villanueva', code: '01-010004', number: '250700004', prev: 2200, pres: 2265, usage: 65,  trend: 'normal', balance: 0.00,   amount: 260.00, total: 260.00,  status: 'Posted',      category: 'normal'   },
      { name: 'Noel Castillo',    code: '01-010005', number: '250700005', prev: 410,  pres: 475,  usage: 65,  trend: 'up',     balance: 95.00,  amount: 260.00, total: 355.00,  status: 'For Posting', category: 'normal'   },
      { name: 'Rachel Domingo',   code: '01-010006', number: '250700006', prev: 990,  pres: 1045, usage: 55,  trend: 'down',   balance: 0.00,   amount: 220.00, total: 220.00,  status: 'Posted',      category: 'remarks'  },
      { name: 'Samuel Ong',       code: '01-010007', number: '250700007', prev: 310,  pres: 500,  usage: 190, trend: 'up',     balance: 700.00, amount: 760.00, total: 1460.00, status: 'Abnormal',    category: 'abnormal' },
      { name: 'Teresa Padilla',   code: '01-010008', number: '250700008', prev: 1500, pres: 1558, usage: 58,  trend: 'normal', balance: 0.00,   amount: 232.00, total: 232.00,  status: 'Posted',      category: 'normal'   },
      { name: 'Eduardo Flores',   code: '01-010009', number: '250700009', prev: 720,  pres: 788,  usage: 68,  trend: 'up',     balance: 140.00, amount: 272.00, total: 412.00,  status: 'For Posting', category: 'remarks'  },
      { name: 'Ligaya Ramos',     code: '01-010010', number: '250700010', prev: 880,  pres: 910,  usage: 30,  trend: 'down',   balance: 0.00,   amount: 120.00, total: 120.00,  status: 'Posted',      category: 'normal'   }
    ]
  },
  2: {
    billingDate: 'Jul 02, 2025', dueDate: 'Jul 16, 2025', disconDate: 'Aug 02, 2025',
    accounts: [
      { name: 'Jose Bautista',    code: '04-020001', number: '250700011', prev: 3400, pres: 3465, usage: 65,  trend: 'normal', balance: 0.00,   amount: 260.00, total: 260.00,  status: 'Posted',      category: 'normal'   },
      { name: 'Luisa Aguilar',    code: '04-020002', number: '250700012', prev: 780,  pres: 960,  usage: 180, trend: 'up',     balance: 550.00, amount: 720.00, total: 1270.00, status: 'Abnormal',    category: 'abnormal' },
      { name: 'Marco Dela Cruz',  code: '04-020003', number: '250700013', prev: 1230, pres: 1295, usage: 65,  trend: 'up',     balance: 0.00,   amount: 260.00, total: 260.00,  status: 'For Posting', category: 'normal'   },
      { name: 'Nina Soriano',     code: '04-020004', number: '250700014', prev: 650,  pres: 700,  usage: 50,  trend: 'down',   balance: 80.00,  amount: 200.00, total: 280.00,  status: 'For Posting', category: 'normal'   },
      { name: 'Oscar Magtanggol', code: '04-020005', number: '250700015', prev: 2010, pres: 2080, usage: 70,  trend: 'up',     balance: 0.00,   amount: 280.00, total: 280.00,  status: 'Posted',      category: 'normal'   },
      { name: 'Petra Navarro',    code: '04-020006', number: '250700016', prev: 510,  pres: 570,  usage: 60,  trend: 'normal', balance: 200.00, amount: 240.00, total: 440.00,  status: 'For Posting', category: 'remarks'  },
      { name: 'Quirino Pascual',  code: '04-020007', number: '250700017', prev: 1760, pres: 1820, usage: 60,  trend: 'down',   balance: 0.00,   amount: 240.00, total: 240.00,  status: 'Posted',      category: 'normal'   }
    ]
  },
  3: {
    billingDate: 'Jul 03, 2025', dueDate: 'Jul 17, 2025', disconDate: 'Aug 03, 2025',
    accounts: [
      { name: 'Ramon Tan',        code: '05-030001', number: '250700021', prev: 900,  pres: 968,  usage: 68,  trend: 'up',     balance: 0.00,   amount: 272.00, total: 272.00,  status: 'For Posting', category: 'normal'   },
      { name: 'Susan Lim',        code: '05-030002', number: '250700022', prev: 440,  pres: 620,  usage: 180, trend: 'up',     balance: 610.00, amount: 720.00, total: 1330.00, status: 'Abnormal',    category: 'abnormal' },
      { name: 'Tomas Espiritu',   code: '05-030003', number: '250700023', prev: 1890, pres: 1950, usage: 60,  trend: 'normal', balance: 0.00,   amount: 240.00, total: 240.00,  status: 'Posted',      category: 'normal'   },
      { name: 'Ursula Cruz',      code: '05-030004', number: '250700024', prev: 670,  pres: 735,  usage: 65,  trend: 'down',   balance: 45.00,  amount: 260.00, total: 305.00,  status: 'For Posting', category: 'remarks'  },
      { name: 'Victor Salazar',   code: '05-030005', number: '250700025', prev: 3100, pres: 3165, usage: 65,  trend: 'up',     balance: 0.00,   amount: 260.00, total: 260.00,  status: 'Posted',      category: 'normal'   }
    ]
  },
  4: {
    billingDate: 'Jul 05, 2025', dueDate: 'Jul 19, 2025', disconDate: 'Aug 05, 2025',
    accounts: [
      { name: 'Wilma Coronado',   code: '06-040001', number: '250700031', prev: 1050, pres: 1120, usage: 70,  trend: 'up',     balance: 0.00,   amount: 280.00, total: 280.00,  status: 'For Posting', category: 'normal'   },
      { name: 'Xavier Gomez',     code: '06-040002', number: '250700032', prev: 620,  pres: 810,  usage: 190, trend: 'up',     balance: 730.00, amount: 760.00, total: 1490.00, status: 'Abnormal',    category: 'abnormal' },
      { name: 'Yolanda Fuentes',  code: '06-040003', number: '250700033', prev: 2300, pres: 2365, usage: 65,  trend: 'normal', balance: 0.00,   amount: 260.00, total: 260.00,  status: 'Posted',      category: 'normal'   },
      { name: 'Zach Villafuerte', code: '06-040004', number: '250700034', prev: 740,  pres: 800,  usage: 60,  trend: 'down',   balance: 110.00, amount: 240.00, total: 350.00,  status: 'For Posting', category: 'remarks'  },
      { name: 'Amy Hernandez',    code: '06-040005', number: '250700035', prev: 1400, pres: 1465, usage: 65,  trend: 'up',     balance: 0.00,   amount: 260.00, total: 260.00,  status: 'Posted',      category: 'normal'   },
      { name: 'Bryan Mercado',    code: '06-040006', number: '250700036', prev: 890,  pres: 950,  usage: 60,  trend: 'down',   balance: 0.00,   amount: 240.00, total: 240.00,  status: 'Posted',      category: 'normal'   },
      { name: 'Cathy Panganiban', code: '06-040007', number: '250700037', prev: 560,  pres: 630,  usage: 70,  trend: 'up',     balance: 155.00, amount: 280.00, total: 435.00,  status: 'For Posting', category: 'normal'   },
      { name: 'Diego Ramos',      code: '06-040008', number: '250700038', prev: 2100, pres: 2160, usage: 60,  trend: 'normal', balance: 0.00,   amount: 240.00, total: 240.00,  status: 'Posted',      category: 'normal'   }
    ]
  },
  5: {
    billingDate: 'Jul 06, 2025', dueDate: 'Jul 20, 2025', disconDate: 'Aug 06, 2025',
    accounts: [
      { name: 'Eva Torres',       code: '02-050001', number: '250700041', prev: 730,  pres: 798,  usage: 68,  trend: 'up',     balance: 0.00,   amount: 272.00, total: 272.00,  status: 'For Posting', category: 'normal'   },
      { name: 'Frank Aquino',     code: '02-050002', number: '250700042', prev: 1560, pres: 1625, usage: 65,  trend: 'normal', balance: 200.00, amount: 260.00, total: 460.00,  status: 'For Posting', category: 'remarks'  },
      { name: 'Grace Bautista',   code: '02-050003', number: '250700043', prev: 410,  pres: 595,  usage: 185, trend: 'up',     balance: 670.00, amount: 740.00, total: 1410.00, status: 'Abnormal',    category: 'abnormal' },
      { name: 'Henry dela Cruz',  code: '02-050004', number: '250700044', prev: 2800, pres: 2865, usage: 65,  trend: 'down',   balance: 0.00,   amount: 260.00, total: 260.00,  status: 'Posted',      category: 'normal'   },
      { name: 'Irene Pascual',    code: '02-050005', number: '250700045', prev: 680,  pres: 745,  usage: 65,  trend: 'up',     balance: 90.00,  amount: 260.00, total: 350.00,  status: 'For Posting', category: 'normal'   },
      { name: 'Julian Soriano',   code: '02-050006', number: '250700046', prev: 1100, pres: 1165, usage: 65,  trend: 'normal', balance: 0.00,   amount: 260.00, total: 260.00,  status: 'Posted',      category: 'normal'   }
    ]
  }
};

// For any sheet id not in the map above, generate generic mock accounts
function getReadingSheetDetails(sheetId) {
  if (READING_SHEET_DETAILS_DATA[sheetId]) return READING_SHEET_DETAILS_DATA[sheetId];

  // Generic fallback — 6 accounts seeded from the sheet id
  var seed = sheetId * 7;
  var accounts = [];
  var firstNames = ['Ana','Ben','Clara','Dennis','Elena','Felix','Gloria','Hector','Iris','Joel'];
  var lastNames  = ['Lim','Manalo','Cabrera','Dela Rosa','Espiritu','Navarro','Magtanggol','Flores','Varga','Cruz'];
  var statuses   = ['For Posting','Posted','For Posting','Posted','Abnormal','For Posting'];
  var categories = ['normal','normal','normal','normal','abnormal','remarks'];
  for (var i = 0; i < 6; i++) {
    var prev  = 500 + ((seed + i * 13) % 3000);
    var usage = 40  + ((seed + i * 7)  % 160);
    var pres  = prev + usage;
    var bal   = (i % 3 === 2) ? Math.round(usage * 2.5) : 0;
    var amt   = Math.round(usage * 4);
    accounts.push({
      name:     firstNames[(seed + i) % firstNames.length] + ' ' + lastNames[(seed + i * 3) % lastNames.length],
      code:     String(sheetId).padStart(2,'0') + '-' + String(i + 1).padStart(6,'0'),
      number:   '25' + String(2500000 + sheetId * 10 + i).padStart(7,'0'),
      prev:     prev,
      pres:     pres,
      usage:    usage,
      trend:    ['up','down','normal'][i % 3],
      balance:  bal,
      amount:   amt,
      total:    bal + amt,
      status:   statuses[i],
      category: categories[i]
    });
  }

  var base = new Date(2025, 6, 1 + (sheetId % 20));
  function fmt(d) {
    return d.toLocaleDateString('en-US', { month:'short', day:'2-digit', year:'numeric' });
  }
  var due   = new Date(base); due.setDate(due.getDate() + 14);
  var discon = new Date(due);  discon.setDate(discon.getDate() + 16);

  return { billingDate: fmt(base), dueDate: fmt(due), disconDate: fmt(discon), accounts: accounts };
}

const READING_SHEET_SAMPLE_DATA = [
  { id: 1,  meterReader: 'Juan dela Cruz',      billingDate: 'Jul 01, 2025', zone: 'ZN-01', forPosting: 3, status: 'In-Progress' },
  { id: 2,  meterReader: 'Ana Lim',             billingDate: 'Jul 02, 2025', zone: 'ZN-04', forPosting: 7, status: 'In-Progress' },
  { id: 3,  meterReader: 'Liza Torres',         billingDate: 'Jul 03, 2025', zone: 'ZN-05', forPosting: 1, status: 'In-Progress' },
  { id: 4,  meterReader: 'Grace Aquino',        billingDate: 'Jul 05, 2025', zone: 'ZN-06', forPosting: 5, status: 'In-Progress' },
  { id: 5,  meterReader: 'Ben Ramos',           billingDate: 'Jul 06, 2025', zone: 'ZN-02', forPosting: 9, status: 'In-Progress' },
  { id: 6,  meterReader: 'Clara Bautista',      billingDate: 'Jul 07, 2025', zone: 'ZN-03', forPosting: 2, status: 'In-Progress' },
  { id: 7,  meterReader: 'Dennis Pascual',      billingDate: 'Jul 08, 2025', zone: 'ZN-01', forPosting: 6, status: 'In-Progress' },
  { id: 8,  meterReader: 'Elena Varga',         billingDate: 'Jul 09, 2025', zone: 'ZN-07', forPosting: 4, status: 'In-Progress' },
  { id: 9,  meterReader: 'Felix Soriano',       billingDate: 'Jul 10, 2025', zone: 'ZN-04', forPosting: 8, status: 'In-Progress' },
  { id: 10, meterReader: 'Gloria Navarro',      billingDate: 'Jul 11, 2025', zone: 'ZN-05', forPosting: 0, status: 'In-Progress' },
  { id: 11, meterReader: 'Hector Flores',       billingDate: 'Jul 12, 2025', zone: 'ZN-02', forPosting: 3, status: 'In-Progress' },
  { id: 12, meterReader: 'Iris Magtanggol',     billingDate: 'Jul 13, 2025', zone: 'ZN-06', forPosting: 7, status: 'In-Progress' },
  { id: 13, meterReader: 'Joel Manalo',         billingDate: 'Jul 14, 2025', zone: 'ZN-03', forPosting: 2, status: 'In-Progress' },
  { id: 14, meterReader: 'Karen Dela Rosa',     billingDate: 'Jul 15, 2025', zone: 'ZN-07', forPosting: 5, status: 'In-Progress' },
  { id: 15, meterReader: 'Leo Cabrera',         billingDate: 'Jul 16, 2025', zone: 'ZN-01', forPosting: 9, status: 'In-Progress' },
  { id: 16, meterReader: 'Mona Espiritu',       billingDate: 'Jul 17, 2025', zone: 'ZN-04', forPosting: 1, status: 'In-Progress' },
  { id: 17, meterReader: 'Nathan Cruz',         billingDate: 'Jul 18, 2025', zone: 'ZN-05', forPosting: 6, status: 'In-Progress' },
  { id: 18, meterReader: 'Olivia Reyes',        billingDate: 'Jul 19, 2025', zone: 'ZN-02', forPosting: 4, status: 'In-Progress' },
  { id: 19, meterReader: 'Paulo Santos',        billingDate: 'Jul 20, 2025', zone: 'ZN-06', forPosting: 8, status: 'In-Progress' },
  { id: 20, meterReader: 'Queenie Aguilar',     billingDate: 'Jul 21, 2025', zone: 'ZN-03', forPosting: 0, status: 'In-Progress' },
  { id: 21, meterReader: 'Maria Santos',        billingDate: 'Jul 01, 2025', zone: 'ZN-02', forPosting: 5, status: 'Completed' },
  { id: 22, meterReader: 'Roberto Reyes',       billingDate: 'Jul 02, 2025', zone: 'ZN-03', forPosting: 2, status: 'Completed' },
  { id: 23, meterReader: 'Carlo Mendoza',       billingDate: 'Jul 03, 2025', zone: 'ZN-01', forPosting: 8, status: 'Completed' },
  { id: 24, meterReader: 'Dante Villanueva',    billingDate: 'Jul 04, 2025', zone: 'ZN-02', forPosting: 1, status: 'Completed' },
  { id: 25, meterReader: 'Noel Castillo',       billingDate: 'Jul 06, 2025', zone: 'ZN-03', forPosting: 7, status: 'Completed' },
  { id: 26, meterReader: 'Rachel Domingo',      billingDate: 'Jul 07, 2025', zone: 'ZN-07', forPosting: 4, status: 'Completed' },
  { id: 27, meterReader: 'Samuel Ong',          billingDate: 'Jul 08, 2025', zone: 'ZN-01', forPosting: 9, status: 'Completed' },
  { id: 28, meterReader: 'Teresa Padilla',      billingDate: 'Jul 09, 2025', zone: 'ZN-04', forPosting: 3, status: 'Completed' },
  { id: 29, meterReader: 'Ulysses Tan',         billingDate: 'Jul 10, 2025', zone: 'ZN-05', forPosting: 6, status: 'Completed' },
  { id: 30, meterReader: 'Vera Lim',            billingDate: 'Jul 11, 2025', zone: 'ZN-06', forPosting: 0, status: 'Completed' },
  { id: 31, meterReader: 'Walter Gomez',        billingDate: 'Jul 12, 2025', zone: 'ZN-02', forPosting: 5, status: 'Completed' },
  { id: 32, meterReader: 'Xena Fuentes',        billingDate: 'Jul 13, 2025', zone: 'ZN-07', forPosting: 8, status: 'Completed' },
  { id: 33, meterReader: 'Yolanda Hernandez',   billingDate: 'Jul 14, 2025', zone: 'ZN-03', forPosting: 2, status: 'Completed' },
  { id: 34, meterReader: 'Zach Villafuerte',    billingDate: 'Jul 15, 2025', zone: 'ZN-01', forPosting: 7, status: 'Completed' },
  { id: 35, meterReader: 'Amy Coronado',        billingDate: 'Jul 16, 2025', zone: 'ZN-04', forPosting: 4, status: 'Completed' },
  { id: 36, meterReader: 'Bryan Salazar',       billingDate: 'Jul 17, 2025', zone: 'ZN-05', forPosting: 9, status: 'Completed' },
  { id: 37, meterReader: 'Cathy Mendez',        billingDate: 'Jul 18, 2025', zone: 'ZN-06', forPosting: 1, status: 'Completed' },
  { id: 38, meterReader: 'Diego Ramos',         billingDate: 'Jul 19, 2025', zone: 'ZN-02', forPosting: 6, status: 'Completed' },
  { id: 39, meterReader: 'Eva Mercado',         billingDate: 'Jul 20, 2025', zone: 'ZN-07', forPosting: 3, status: 'Completed' },
  { id: 40, meterReader: 'Frank Panganiban',    billingDate: 'Jul 21, 2025', zone: 'ZN-03', forPosting: 0, status: 'Completed' }
];