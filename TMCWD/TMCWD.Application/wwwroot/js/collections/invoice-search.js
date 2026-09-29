(function () {
  'use strict';
  const overlay = document.getElementById('isOverlay');
  const openBtn = document.getElementById('invoiceSearchBtn');
  const closeBtn = document.getElementById('isCloseBtn');
  const input = document.getElementById('invoiceSearchInput');
  const searchBtn = document.getElementById('invoiceSearchBtn2');
  const results = document.getElementById('invoiceSearchResults');
  if (!overlay || !openBtn || !closeBtn || !input || !searchBtn || !results) return;

  const money = value => '\u20B1' + Number(value).toLocaleString('en-PH', {
    minimumFractionDigits: 2, maximumFractionDigits: 2
  });

  function render(rows, message) {
    results.replaceChildren();
    if (!rows.length) {
      const empty = document.createElement('div');
      empty.className = 'is-empty-state';
      empty.textContent = message || 'No invoices found';
      results.appendChild(empty);
      return;
    }
    const table = document.createElement('table');
    table.className = 'is-results-table';
    table.innerHTML = '<thead><tr><th>Invoice No.</th><th>Account</th><th style="text-align:right">Total Amount</th><th>Transaction Type</th><th>Date Issued</th><th>Status</th></tr></thead>';
    const body = document.createElement('tbody');
    rows.forEach(invoice => {
      const row = document.createElement('tr');
      const values = [invoice.invoiceNo, invoice.account || '\u2014', money(invoice.totalAmount),
        invoice.transactionType, new Date(invoice.dateIssued).toLocaleString('en-PH'), invoice.status];
      values.forEach((value, index) => {
        const cell = document.createElement('td');
        cell.textContent = value;
        if (index === 0) cell.className = 'is-cell-invoice';
        if (index === 2) cell.className = 'is-cell-amount';
        if (index === 5) { cell.className = 'is-cell-status'; cell.title = invoice.cancellationReason || ''; }
        row.appendChild(cell);
      });
      body.appendChild(row);
    });
    table.appendChild(body);
    results.appendChild(table);
  }

  async function search() {
    const query = input.value.trim();
    if (!query) { render([], 'Enter an invoice number.'); return; }
    searchBtn.disabled = true;
    try {
      const response = await fetch('/Collections/SearchInvoices?q=' + encodeURIComponent(query));
      if (!response.ok) throw new Error('Invoice search failed.');
      render(await response.json());
    } catch (error) {
      render([], error.message || 'Invoice search failed.');
    } finally {
      searchBtn.disabled = false;
    }
  }

  function close() { overlay.style.display = 'none'; results.replaceChildren(); input.value = ''; }
  openBtn.addEventListener('click', event => { event.preventDefault(); overlay.style.display = 'flex'; input.focus(); });
  closeBtn.addEventListener('click', close);
  searchBtn.addEventListener('click', search);
  input.addEventListener('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); search(); } });
  overlay.addEventListener('click', event => { if (event.target === overlay) close(); });
}());
