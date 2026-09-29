(function () {
  'use strict';
  const overlay = document.getElementById('ciOverlay');
  const openBtn = document.getElementById('cancelInvoiceBtn');
  const closeBtn = document.getElementById('ciCloseBtn');
  const invoiceInput = document.getElementById('ciInvoiceInput');
  const reasonSelect = document.getElementById('ciReasonSelect');
  const otherField = document.getElementById('ciOtherReasonField');
  const otherInput = document.getElementById('ciOtherReasonInput');
  const submitBtn = document.getElementById('ciSubmitBtn');
  if (!overlay || !openBtn || !closeBtn || !invoiceInput || !reasonSelect || !submitBtn) return;

  const reasons = ['Duplicate payment', 'Wrong account', 'Wrong amount', 'Incorrect payment method', 'Other'];
  reasons.forEach(reason => {
    const option = document.createElement('option');
    option.value = reason;
    option.textContent = reason;
    reasonSelect.appendChild(option);
  });

  function toast(message, danger) {
    const card = document.getElementById('toast');
    const backdrop = document.getElementById('toastBackdrop');
    const title = document.getElementById('toastTitle');
    const body = document.getElementById('toastMsg');
    const ok = document.getElementById('toastIconSuccess');
    const error = document.getElementById('toastIconDanger');
    if (!card || !backdrop) return;
    title.textContent = danger ? 'Error' : 'Success';
    body.textContent = message;
    ok.style.display = danger ? 'none' : 'block';
    error.style.display = danger ? 'block' : 'none';
    card.className = 'toast-card show' + (danger ? ' danger' : '');
    backdrop.className = 'toast-backdrop show';
    setTimeout(() => { card.className = 'toast-card'; backdrop.className = 'toast-backdrop'; }, 3500);
  }

  function fieldError(field, message) {
    let element = field.parentElement.querySelector('.ci-field-error');
    if (!element) {
      element = document.createElement('span');
      element.className = 'ci-field-error';
      field.parentElement.appendChild(element);
    }
    element.textContent = message;
    element.style.display = 'block';
  }
  function clearErrors() {
    overlay.querySelectorAll('.ci-field-error').forEach(element => { element.style.display = 'none'; });
  }
  function close() {
    overlay.style.display = 'none';
    invoiceInput.value = '';
    reasonSelect.value = '';
    if (otherInput) otherInput.value = '';
    if (otherField) otherField.style.display = 'none';
    clearErrors();
  }

  reasonSelect.addEventListener('change', () => {
    if (otherField) otherField.style.display = reasonSelect.value === 'Other' ? 'block' : 'none';
  });
  openBtn.addEventListener('click', event => {
    event.preventDefault();
    overlay.style.display = 'flex';
    invoiceInput.focus();
  });
  closeBtn.addEventListener('click', close);
  overlay.addEventListener('click', event => { if (event.target === overlay) close(); });

  submitBtn.addEventListener('click', async () => {
    clearErrors();
    const invoiceNo = invoiceInput.value.trim();
    const selectedReason = reasonSelect.value;
    const reason = selectedReason === 'Other' ? (otherInput?.value.trim() || '') : selectedReason;
    let valid = true;
    if (!invoiceNo) { fieldError(invoiceInput, 'Invoice number is required.'); valid = false; }
    if (!selectedReason) { fieldError(reasonSelect, 'Select a cancellation reason.'); valid = false; }
    if (selectedReason === 'Other' && reason.length < 3) {
      fieldError(otherInput, 'Enter at least 3 characters.'); valid = false;
    }
    if (!valid) return;
    if (!window.confirm(`Cancel invoice ${invoiceNo}? This will reopen its bill and reverse the payment.`)) return;

    submitBtn.disabled = true;
    submitBtn.textContent = 'Cancelling...';
    try {
      const response = await fetch('/Collections/CancelInvoice', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
        },
        body: JSON.stringify({ invoiceNo, reason })
      });
      const result = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(result.message || 'Invoice could not be cancelled.');
      close();
      toast(`Invoice ${invoiceNo} was cancelled and its bill was reopened.`, false);
    } catch (error) {
      toast(error.message || 'Invoice could not be cancelled.', true);
    } finally {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Submit';
    }
  });
}());
