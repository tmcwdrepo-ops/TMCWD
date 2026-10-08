(function () {
  'use strict';
  const overlay = document.getElementById('riOverlay');
  const openBtn = document.getElementById('reprintInvoiceBtn');
  const closeBtn = document.getElementById('riCloseBtn');
  const input = document.getElementById('reprintInvoiceNumber');
  const printBtn = document.getElementById('printInvoiceBtn');
  if (!overlay || !openBtn || !closeBtn || !input || !printBtn) return;

  const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  }[c]));
  const money = value => '\u20B1' + Number(value).toLocaleString('en-PH', {
    minimumFractionDigits: 2, maximumFractionDigits: 2
  });
  function feedback(message, isError) {
    let element = overlay.querySelector('.ri-input-feedback');
    if (!element) {
      element = document.createElement('span');
      element.className = 'ri-input-feedback';
      input.closest('.ri-input-wrap').after(element);
    }
    element.textContent = message;
    element.style.color = isError ? '#b42318' : '#067647';
  }

  async function printInvoice() {
    const number = input.value.trim();
    if (!number) { feedback('Enter a payment reference.', true); return; }
    printBtn.disabled = true;
    try {
      const response = await fetch('/Collections/Invoice?invoiceNumber=' + encodeURIComponent(number));
      const data = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(data.message || 'Payment receipt was not found.');
      const popup = window.open('', '_blank', 'width=760,height=800');
      if (!popup) throw new Error('Allow pop-ups to print the payment receipt.');
      const lines = data.lines.map(line => `<tr><td>${escape(line.billingReference)}</td><td>${escape(line.accountNumber)}</td><td>${escape(line.accountHolder)}</td><td class="amount">${escape(money(line.amount))}</td></tr>`).join('');
      popup.document.write(`<!doctype html><html><head><title>Payment Receipt ${escape(data.invoiceNo)}</title><style>body{font:14px Arial;padding:32px;color:#172554}h1{font-size:22px}table{width:100%;border-collapse:collapse;margin-top:20px}th,td{padding:9px;border-bottom:1px solid #ccd5e5;text-align:left}.amount,.total{text-align:right}.total{font-size:18px;margin-top:20px}.cancelled{color:#b42318;font-weight:bold}@media print{button{display:none}}</style></head><body><h1>TMCWD Collection Receipt</h1><p><b>Payment Reference:</b> ${escape(data.invoiceNo)}<br><b>Date:</b> ${escape(new Date(data.dateIssued).toLocaleString('en-PH'))}<br><b>Cashier:</b> ${escape(data.cashierName)}<br><b>Method:</b> ${escape(data.transactionType)}${data.externalReference ? '<br><b>External Reference:</b> ' + escape(data.externalReference) : ''}<br><b>Status:</b> <span class="${data.status === 'Cancelled' ? 'cancelled' : ''}">${escape(data.status)}</span></p><table><thead><tr><th>Billing Reference</th><th>Account</th><th>Account Holder</th><th class="amount">Amount</th></tr></thead><tbody>${lines}</tbody></table><p class="total"><b>Total: ${escape(money(data.totalAmount))}</b></p><button onclick="window.print()">Print</button></body></html>`);
      popup.document.close();
      popup.focus();
      setTimeout(() => popup.print(), 250);
      feedback('Payment receipt opened for printing.', false);
    } catch (error) {
      feedback(error.message || 'Payment receipt could not be printed.', true);
    } finally {
      printBtn.disabled = false;
    }
  }

  function close() { overlay.style.display = 'none'; input.value = ''; }
  openBtn.addEventListener('click', event => { event.preventDefault(); overlay.style.display = 'flex'; input.focus(); });
  closeBtn.addEventListener('click', close);
  printBtn.addEventListener('click', printInvoice);
  input.addEventListener('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); printInvoice(); } });
  overlay.addEventListener('click', event => { if (event.target === overlay) close(); });
}());
