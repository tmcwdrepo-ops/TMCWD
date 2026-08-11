// ============================================================
// ============================================================
(function initCancelInvoiceModal() {
  'use strict';

  //  DOM refs 
  var overlay         = document.getElementById('ciOverlay');
  var openBtnNav      = document.getElementById('cancelInvoiceBtn');
  var closeBtn        = document.getElementById('ciCloseBtn');
  var invoiceInput    = document.getElementById('ciInvoiceInput');
  var suggestionsEl   = document.getElementById('ciSuggestions');
  var reasonSelect    = document.getElementById('ciReasonSelect');
  var otherField      = document.getElementById('ciOtherReasonField');
  var otherInput      = document.getElementById('ciOtherReasonInput');
  var submitBtn       = document.getElementById('ciSubmitBtn');

  // Guard — nothing to wire if modal markup is absent.
  if (!overlay || !openBtnNav || !closeBtn) return;

  //  Module state 
  var _selectedSuggIdx = -1;

  //  Helpers 
  function showCiToast(msg, isDanger) {
    var card     = document.getElementById('toast');
    var backdrop = document.getElementById('toastBackdrop');
    var title    = document.getElementById('toastTitle');
    var msgEl    = document.getElementById('toastMsg');
    var iconOk   = document.getElementById('toastIconSuccess');
    var iconErr  = document.getElementById('toastIconDanger');
    if (!card) return;
    title.textContent     = isDanger ? 'Error' : 'Success';
    msgEl.textContent     = msg;
    iconOk.style.display  = isDanger ? 'none'  : 'block';
    iconErr.style.display = isDanger ? 'block' : 'none';
    card.className        = 'toast-card show' + (isDanger ? ' danger' : '');
    backdrop.className    = 'toast-backdrop show';
    clearTimeout(window._ciToastTimer);
    window._ciToastTimer = setTimeout(function () {
      card.className     = 'toast-card';
      backdrop.className = 'toast-backdrop';
    }, 3000);
  }

  //  Inline error helpers 
  function showFieldError(fieldEl, msg) {
    if (!fieldEl) return;
    var err = fieldEl.parentElement.querySelector('.ci-field-error');
    if (!err) {
      err = document.createElement('span');
      err.className = 'ci-field-error';
      fieldEl.parentElement.appendChild(err);
    }
    err.textContent   = msg;
    err.style.display = 'block';
    fieldEl.classList.add(
      fieldEl.tagName === 'SELECT' ? 'ci-select--error' : 'ci-input--error'
    );
  }

  function clearFieldError(fieldEl) {
    if (!fieldEl) return;
    var err = fieldEl.parentElement.querySelector('.ci-field-error');
    if (err) err.style.display = 'none';
    fieldEl.classList.remove('ci-input--error', 'ci-select--error');
  }

  function clearAllErrors() {
    [invoiceInput, reasonSelect, otherInput].forEach(clearFieldError);
  }

  //  Populate reason dropdown 
  function populateReasons() {
    if (!reasonSelect) return;
    // Remove all options except the placeholder
    while (reasonSelect.options.length > 1) reasonSelect.remove(1);

    var reasons = (typeof SAMPLE_CANCEL_INVOICE_REASONS !== 'undefined')
      ? SAMPLE_CANCEL_INVOICE_REASONS
      : ['Other'];

    reasons.forEach(function (r) {
      var opt = document.createElement('option');
      opt.value       = r;
      opt.textContent = r;
      reasonSelect.appendChild(opt);
    });
  }

  //  "Other" toggle 
  if (reasonSelect) {
    reasonSelect.addEventListener('change', function () {
      clearFieldError(reasonSelect);
      var isOther = this.value === 'Other';
      otherField.style.display = isOther ? 'block' : 'none';
      if (!isOther && otherInput) {
        otherInput.value = '';
        clearFieldError(otherInput);
      }
    });
  }

  //  Live-search suggestions 
  function hideSuggestions() {
    if (suggestionsEl) suggestionsEl.style.display = 'none';
    _selectedSuggIdx = -1;
  }

  function showSuggestions(term) {
    if (!suggestionsEl || term.length < 1) { hideSuggestions(); return; }

    // Search COLLECTION_ACCOUNTS by account number or account name only
    var data = (typeof COLLECTION_ACCOUNTS !== 'undefined')
      ? COLLECTION_ACCOUNTS
      : [];

    var lower   = term.toLowerCase();
    var matches = data.filter(function (entry) {
      var acc = entry.account;
      return acc.accountNumber.toLowerCase().indexOf(lower) !== -1 ||
             acc.name.toLowerCase().indexOf(lower)          !== -1;
    });

    if (matches.length === 0) {
      suggestionsEl.innerHTML =
        '<div class="ci-no-suggestions">No matching accounts</div>';
      suggestionsEl.style.display = 'block';
      _selectedSuggIdx = -1;
      return;
    }

    suggestionsEl.innerHTML = matches.map(function (entry, i) {
      var acc = entry.account;
      return '<div class="ci-suggestion-item" data-idx="' + i + '">' +
        '<div class="ci-suggestion-invoice">' + acc.accountNumber + '</div>' +
        '<div class="ci-suggestion-account">' + acc.name          + '</div>' +
        '<div class="ci-suggestion-meta">'    +
          acc.meterNumber + ' \u2022 ' + acc.type + ' \u2022 ' + acc.address +
        '</div>' +
      '</div>';
    }).join('');

    suggestionsEl.style.display = 'block';
    suggestionsEl._matches      = matches;
    _selectedSuggIdx = -1;

    suggestionsEl.querySelectorAll('.ci-suggestion-item').forEach(function (item) {
      item.addEventListener('mousedown', function (e) {
        e.preventDefault();
        var idx = parseInt(this.dataset.idx, 10);
        if (suggestionsEl._matches && suggestionsEl._matches[idx]) {
          var acc = suggestionsEl._matches[idx].account;
          // Fill with account number so staff knows which account to cancel for
          invoiceInput.value = acc.accountNumber;
        }
        hideSuggestions();
        clearFieldError(invoiceInput);
      });
    });
  }

  function navigateSuggestions(dir) {
    if (!suggestionsEl || suggestionsEl.style.display === 'none') return;
    var items = suggestionsEl.querySelectorAll('.ci-suggestion-item');
    if (!items.length) return;

    if (_selectedSuggIdx >= 0) {
      items[_selectedSuggIdx].classList.remove('ci-suggestion-highlighted');
    }
    if (dir === 'down') {
      _selectedSuggIdx = (_selectedSuggIdx + 1) % items.length;
    } else {
      _selectedSuggIdx = _selectedSuggIdx <= 0
        ? items.length - 1 : _selectedSuggIdx - 1;
    }
    items[_selectedSuggIdx].classList.add('ci-suggestion-highlighted');
    items[_selectedSuggIdx].scrollIntoView({ block: 'nearest' });
  }

  //  Invoice input events 
  if (invoiceInput) {
    invoiceInput.addEventListener('input', function () {
      clearFieldError(invoiceInput);
      showSuggestions(this.value.trim());
    });

    invoiceInput.addEventListener('keydown', function (e) {
      var visible = suggestionsEl && suggestionsEl.style.display !== 'none';

      if (e.key === 'ArrowDown') { e.preventDefault(); navigateSuggestions('down'); return; }
      if (e.key === 'ArrowUp')   { e.preventDefault(); navigateSuggestions('up');   return; }
      if (e.key === 'Escape')    { hideSuggestions(); return; }

      if (e.key === 'Enter') {
        e.preventDefault();
        if (visible && _selectedSuggIdx >= 0 &&
            suggestionsEl._matches && suggestionsEl._matches[_selectedSuggIdx]) {
          invoiceInput.value = suggestionsEl._matches[_selectedSuggIdx].invoiceNo;
          hideSuggestions();
        }
      }
    });

    invoiceInput.addEventListener('blur', function () {
      setTimeout(hideSuggestions, 150);
    });
  }

  //  Validation 
  function validate() {
    clearAllErrors();
    var ok = true;

    var inv = invoiceInput ? invoiceInput.value.trim() : '';
    if (!inv) {
      showFieldError(invoiceInput, 'Invoice number is required.');
      ok = false;
    }

    var reason = reasonSelect ? reasonSelect.value : '';
    if (!reason) {
      showFieldError(reasonSelect, 'Please select a reason.');
      ok = false;
    }

    if (reason === 'Other') {
      var other = otherInput ? otherInput.value.trim() : '';
      if (!other) {
        showFieldError(otherInput, 'Please specify the reason.');
        ok = false;
      }
    }

    return ok;
  }

  //  Submit 
  // TODO: replace the stub below with a real fetch/POST:
  //   return fetch('/api/Billing/CancelInvoice', {
  //     method: 'POST',
  //     headers: { 'Content-Type': 'application/json' },
  //     body: JSON.stringify({ invoiceNo, reason, otherReason })
  //   }).then(function (r) { if (!r.ok) throw new Error(r.statusText); });
  function submitCancellation(payload) {
    console.log('[CancelInvoice] payload:', payload);
    return new Promise(function (resolve) { setTimeout(resolve, 600); });
  }

  if (submitBtn) {
    submitBtn.addEventListener('click', function () {
      if (!validate()) return;

      var payload = {
        invoiceNo:   invoiceInput.value.trim(),
        reason:      reasonSelect.value,
        otherReason: reasonSelect.value === 'Other'
          ? (otherInput ? otherInput.value.trim() : '')
          : ''
      };

      submitBtn.disabled    = true;
      submitBtn.textContent = 'Submitting\u2026';

      submitCancellation(payload)
        .then(function () {
          closeModal();
          showCiToast(
            'Invoice ' + payload.invoiceNo + ' cancellation request submitted.'
          );
        })
        .catch(function (err) {
          console.error('[CancelInvoice] error:', err);
          showCiToast('Submission failed. Please try again.', true);
        })
        .finally(function () {
          submitBtn.disabled    = false;
          submitBtn.textContent = 'Submit';
        });
    });
  }

  //  Open 
  function openModal() {
    // Pre-fill invoice number from the currently loaded invoice (if any)
    var invoiceNo = '';
    var invoiceInputMain = document.getElementById('invoiceInput');
    if (invoiceInputMain && invoiceInputMain.value.trim()) {
      invoiceNo = 'CR00' + invoiceInputMain.value.trim();
    }

    if (invoiceInput) invoiceInput.value = invoiceNo;
    if (reasonSelect) reasonSelect.value = '';
    if (otherField)   otherField.style.display = 'none';
    if (otherInput)   otherInput.value  = '';
    clearAllErrors();
    hideSuggestions();

    submitBtn.disabled    = false;
    submitBtn.textContent = 'Submit';

    overlay.style.display        = 'flex';
    document.body.style.overflow = 'hidden';

    setTimeout(function () {
      if (invoiceInput) invoiceInput.focus();
    }, 200);
  }

  //  Close 
  function closeModal() {
    hideSuggestions();
    clearAllErrors();
    overlay.style.display        = 'none';
    document.body.style.overflow = '';
    openBtnNav.focus();
  }

  //  Event listeners 
  openBtnNav.addEventListener('click', function (e) {
    e.preventDefault();
    openModal();
  });

  closeBtn.addEventListener('click', closeModal);

  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeModal();
  });

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && overlay.style.display === 'flex') closeModal();
  });

  // Hide suggestions when clicking outside the invoice input area
  document.addEventListener('click', function (e) {
    if (!e.target.closest('#ciInvoiceWrap')) hideSuggestions();
  });

  //  Initial setup 
  populateReasons();

}());
