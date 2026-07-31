/* ===========================================================
   other-charges-sample.js
   Sample data for the Other Charges page.
   Used as a local fallback when the API is not yet connected.

   Structure mirrors the API response shape:
     { id, description, payableIn, particulars, amount }

   SAMPLE_ACCOUNTS  — account lookup stub
   SAMPLE_CHARGES   — charges keyed by account number
   =========================================================== */

var OC_SAMPLE_ACCOUNTS = {
    "02-090015": { accountName: "Juan dela Cruz" },
    "02-090016": { accountName: "Maria Santos" },
    "02-090017": { accountName: "Roberto Reyes" },
    "02-090018": { accountName: "Ana Lim" },
    "02-090019": { accountName: "Grace Aquino" }
};

var OC_SAMPLE_CHARGES = {
    "02-090015": [
        { id: 1,  description: "MSR - Recon. Fee",       payableIn: "Cash",   particulars: "July reconnection",         amount: 350.00 },
        { id: 2,  description: "Penalty",                payableIn: "Cash",   particulars: "Late payment penalty",      amount: 125.50 },
        { id: 3,  description: "Meter Testing Fee",      payableIn: "Check",  particulars: "Annual meter calibration",  amount: 500.00 },
        { id: 4,  description: "Reconnection Fee",       payableIn: "Cash",   particulars: "Service restoration",       amount: 250.00 },
        { id: 5,  description: "Miscellaneous Charge",   payableIn: "Cash",   particulars: "Admin fee",                 amount:  75.00 },
        { id: 6,  description: "Service Connection Fee", payableIn: "Check",  particulars: "New connection June 2025",  amount: 1200.00 },
        { id: 7,  description: "Penalty",                payableIn: "Cash",   particulars: "June late payment",         amount:  98.75 },
        { id: 8,  description: "MSR - Recon. Fee",       payableIn: "Cash",   particulars: "June reconnection",         amount: 350.00 },
        { id: 9,  description: "Meter Testing Fee",      payableIn: "GCash",  particulars: "Special meter test",        amount: 450.00 },
        { id: 10, description: "Miscellaneous Charge",   payableIn: "Cash",   particulars: "Sticker replacement",       amount:  50.00 },
        { id: 11, description: "Reconnection Fee",       payableIn: "Cash",   particulars: "Emergency restoration",     amount: 250.00 },
        { id: 12, description: "Penalty",                payableIn: "Cash",   particulars: "May late payment",          amount: 112.00 }
    ],
    "02-090016": [
        { id: 13, description: "Reconnection Fee",       payableIn: "Cash",   particulars: "Disconnection reversal",    amount: 250.00 },
        { id: 14, description: "Penalty",                payableIn: "Cash",   particulars: "Overdue balance",           amount:  87.25 },
        { id: 15, description: "Miscellaneous Charge",   payableIn: "GCash",  particulars: "Document processing",       amount:  60.00 }
    ],
    "02-090017": [
        { id: 16, description: "Service Connection Fee", payableIn: "Check",  particulars: "New meter installation",    amount: 1500.00 },
        { id: 17, description: "Meter Testing Fee",      payableIn: "Cash",   particulars: "Meter accuracy check",      amount:  500.00 },
        { id: 18, description: "MSR - Recon. Fee",       payableIn: "Cash",   particulars: "Account reconciliation",    amount:  350.00 }
    ],
    "02-090018": [
        { id: 19, description: "Penalty",                payableIn: "Cash",   particulars: "Repeated late payment",     amount: 225.00 },
        { id: 20, description: "Reconnection Fee",       payableIn: "Cash",   particulars: "Water service restored",    amount: 250.00 }
    ],
    "02-090019": [
        { id: 21, description: "Miscellaneous Charge",   payableIn: "Cash",   particulars: "Field visit fee",           amount:  150.00 },
        { id: 22, description: "Penalty",                payableIn: "Cash",   particulars: "Late payment July",         amount:   75.50 },
        { id: 23, description: "Service Connection Fee", payableIn: "Check",  particulars: "Meter upgrade",             amount: 1800.00 },
        { id: 24, description: "MSR - Recon. Fee",       payableIn: "GCash",  particulars: "Balance reconciliation",    amount:  350.00 },
        { id: 25, description: "Meter Testing Fee",      payableIn: "Cash",   particulars: "Bi-annual test",            amount:  500.00 }
    ]
};
